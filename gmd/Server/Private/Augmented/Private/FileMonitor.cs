using gmd.Git;
using gmd.Utils.GlobPatterns;

namespace gmd.Server.Private.Augmented.Private;

interface IFileMonitor
{
    event Action<ChangeEvent> FileChanged;
    event Action<ChangeEvent> RepoChanged;

    // Watches a working folder. 'excludedFolders' are folders inside it that are working folders
    // of their own (linked worktrees nested in it), whose changes are not this folder's status.
    void Monitor(string workingFolder, IReadOnlyList<string> excludedFolders);
    IDisposable Pause();
    void SetReadRepoTime(DateTime time);
    void SetReadStatusTime(DateTime time);
}

public delegate bool Ignorer(string path);

// Turns file system events into two debounced events: FileChanged, i.e. the status may have
// changed, and RepoChanged, i.e. the commits or branches may have. Three watchers feed them, since
// a working folder's git state is not all in one place once worktrees are involved:
//
//   the working folder, recursively   — edits (FileChanged), and '.git/HEAD' (RepoChanged)
//   the common git dir, recursively   — the refs, shared by every worktree of the repository, and
//                                       'worktrees/', where the other worktrees keep their state
//   the worktree's own git dir        — only for a linked worktree, whose HEAD is not under the
//                                       working folder but in '<common>/worktrees/<name>/'
//
// For the main worktree the common dir *is* '.git', so the first two overlap; that is harmless,
// the events are debounced into one anyway.
[SingleInstance]
class FileMonitor : IFileMonitor
{
    static readonly TimeSpan StatusDelayTriggerTime = TimeSpan.FromSeconds(1);
    static readonly TimeSpan RepositoryDelayTriggerTime = TimeSpan.FromSeconds(1);

    const string GitFolder = ".git";
    static readonly string GitFolderPath = ".git" + Path.DirectorySeparatorChar;
    static readonly string GitRefsPath = "refs" + Path.DirectorySeparatorChar;
    static readonly string GitWorktreesPath = "worktrees" + Path.DirectorySeparatorChar;
    const string GitHeadFile = "HEAD";
    static readonly string GitHeadFilePath = Path.Combine(GitFolder, GitHeadFile);
    const NotifyFilters NotifyFilters =
        System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName;

    // What another worktree writes that says its checkout changed: which commit it is on, where
    // it is, and whether it is locked. Its index and logs are written by every 'git status' run
    // there, which is not a change of anything this repo shows.
    static readonly string[] WorktreeStateFiles = [GitHeadFile, "gitdir", "locked"];

    readonly FileSystemWatcher workFolderWatcher = new FileSystemWatcher();
    readonly FileSystemWatcher commonDirWatcher = new FileSystemWatcher();
    readonly FileSystemWatcher gitDirWatcher = new FileSystemWatcher();

    readonly IMainThread mainThread;

    IReadOnlyList<Glob> matchers = new List<Glob>();
    IReadOnlyList<string> excludedFolders = [];

    readonly object syncRoot = new object();

    private string workingFolder = "";
    bool isTimerStarted = false;
    ChangeEvent? fileChangedEvent = null;
    ChangeEvent? repoChangedEvent = null;

    bool isPaused = false;

    // The clock the trigger delays are measured against, so tests can drive them without waiting.
    internal Func<DateTime> Now = () => DateTime.UtcNow;

    public event Action<ChangeEvent>? FileChanged;

    public event Action<ChangeEvent>? RepoChanged;

    internal FileMonitor(IMainThread mainThread)
    {
        this.mainThread = mainThread;

        workFolderWatcher.Changed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Created += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Deleted += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Renamed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);

        commonDirWatcher.Changed += (s, e) => CommonDirChange(e.FullPath, e.Name, e.ChangeType);
        commonDirWatcher.Created += (s, e) => CommonDirChange(e.FullPath, e.Name, e.ChangeType);
        commonDirWatcher.Deleted += (s, e) => CommonDirChange(e.FullPath, e.Name, e.ChangeType);
        commonDirWatcher.Renamed += (s, e) => CommonDirChange(e.FullPath, e.Name, e.ChangeType);

        gitDirWatcher.Changed += (s, e) => GitDirChange(e.FullPath, e.Name, e.ChangeType);
        gitDirWatcher.Created += (s, e) => GitDirChange(e.FullPath, e.Name, e.ChangeType);
        gitDirWatcher.Deleted += (s, e) => GitDirChange(e.FullPath, e.Name, e.ChangeType);
        gitDirWatcher.Renamed += (s, e) => GitDirChange(e.FullPath, e.Name, e.ChangeType);
    }

    // The folders being watched, for tests
    internal IReadOnlyList<string> WatchedPaths =>
        new[] { workFolderWatcher, commonDirWatcher, gitDirWatcher }
            .Where(w => w.EnableRaisingEvents)
            .Select(w => w.Path)
            .ToList();

    public void SetReadRepoTime(DateTime time)
    {
        lock (syncRoot)
        {
            this.repoChangedEvent = null;
            this.fileChangedEvent = null;
        }
    }

    public void SetReadStatusTime(DateTime time)
    {
        lock (syncRoot)
        {
            this.fileChangedEvent = null;
        }
    }

    internal bool OnTimer()
    {
        lock (syncRoot)
        {
            if (isPaused)
                return true;
        }

        ChangeEvent? fileEvent = null;
        ChangeEvent? repoEvent = null;

        lock (syncRoot)
        {
            // Copy FileChangedEvents, RepoChangedEvents, read times
            var timeStamp = Now();

            if (fileChangedEvent != null && fileChangedEvent.TimeStamp + StatusDelayTriggerTime < timeStamp)
            {
                fileEvent = fileChangedEvent;
                fileChangedEvent = null;
            }

            if (repoChangedEvent != null && repoChangedEvent.TimeStamp + RepositoryDelayTriggerTime < timeStamp)
            {
                repoEvent = repoChangedEvent;
                repoChangedEvent = null;
            }
        }

        if (repoEvent != null)
        {
            Log.Info($"Repo changed event {repoEvent.TimeStamp.IsoMs()}");
            mainThread.Post(() => RepoChanged?.Invoke(repoEvent));
        }

        if (fileEvent != null && repoEvent == null) // no need to send status event if repo changed event
        {
            Log.Info($"File changed event {fileEvent.TimeStamp.IsoMs()}");
            mainThread.Post(() => FileChanged?.Invoke(fileEvent));
        }

        return true;
    }

    public void Monitor(string workingFolder, IReadOnlyList<string> excludedFolders)
    {
        if (!isTimerStarted)
        {
            // OnTimer always returns true, so this tick runs for the life of the process and is
            // never removed. That is deliberate — it is the debounce clock, and there is nothing to
            // stop it for. It does mean the main loop always has a timeout pending and so never
            // blocks indefinitely, which looks like a culprit when profiling idle CPU. It is not:
            // one wakeup a second is not a spin. See the Terminal.Gui 1.17.1 finding in
            // MODERNIZATION.md for what the real one was.
            mainThread.RunPeriodically(TimeSpan.FromSeconds(1), OnTimer);
            isTimerStarted = true;
        }

        if (
            !Directory.Exists(workingFolder)
            || !Try(out var gitDir, out var _, GitDir.Resolve(workingFolder))
            || !Directory.Exists(gitDir.CommonDirPath)
        )
        {
            Log.Warn($"Selected folder '{workingFolder}' is not a root working folder.");
            return;
        }

        // The worktrees nested inside this folder can come and go between two reads of the same
        // repo, so they are updated even when the watchers are not
        this.excludedFolders = excludedFolders.Select(NormalizedFolder).ToList();

        if (workingFolder == this.workingFolder)
        {
            // Already monitoring this folder
            return;
        }

        workFolderWatcher.EnableRaisingEvents = false;
        commonDirWatcher.EnableRaisingEvents = false;
        gitDirWatcher.EnableRaisingEvents = false;

        matchers = GetMatches(workingFolder);

        workFolderWatcher.Path = workingFolder;
        workFolderWatcher.NotifyFilter = NotifyFilters;
        workFolderWatcher.Filter = "*.*";
        workFolderWatcher.IncludeSubdirectories = true;

        commonDirWatcher.Path = gitDir.CommonDirPath;
        commonDirWatcher.NotifyFilter = NotifyFilters;
        commonDirWatcher.Filter = "*.*";
        commonDirWatcher.IncludeSubdirectories = true;

        workFolderWatcher.EnableRaisingEvents = true;
        commonDirWatcher.EnableRaisingEvents = true;

        if (gitDir.IsLinkedWorktree && Directory.Exists(gitDir.GitDirPath))
        {
            gitDirWatcher.Path = gitDir.GitDirPath;
            gitDirWatcher.NotifyFilter = NotifyFilters;
            gitDirWatcher.Filter = "*.*";
            gitDirWatcher.IncludeSubdirectories = false;
            gitDirWatcher.EnableRaisingEvents = true;
        }

        this.workingFolder = workingFolder;
    }

    public IDisposable Pause()
    {
        lock (syncRoot)
        {
            isPaused = true;
        }
        Log.Info("Pause file monitor ...");

        return new Disposable(() =>
        {
            lock (syncRoot)
            {
                isPaused = false;
            }
            Log.Info("Resume file monitor");
        });
    }

    internal void WorkingFolderChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        if (path == GitHeadFilePath)
        {
            RepoChange(fullPath, path, changeType);
            return;
        }

        // In a linked worktree '.git' is a file, which git rewrites when the worktree is moved or
        // repaired. In the main worktree it is a folder, whose own timestamp changes as git writes
        // into it, and that is nothing to act on.
        if (path == GitFolder && File.Exists(fullPath))
        {
            RepoChange(fullPath, path, changeType);
            return;
        }

        // Log.Info($"'{path}', '{fullPath}'");
        if (path == null || !path.StartsWith(GitFolderPath))
        {
            if (path != null && IsIgnored(path))
            {
                // Log.Info($"Ignored: '{fullPath}'");
                return;
            }

            if (IsExcluded(fullPath))
            {
                // A file in another worktree, i.e. someone else's uncommitted changes
                return;
            }

            if (fullPath != null && !Directory.Exists(fullPath))
            {
                //Log.Debug($"Status change for '{fullPath}' {changeType}");.
                FileChange(fullPath);
            }
        }
    }

    // A change in the common git dir, 'path' being relative to it. Refs are what commits, fetches
    // and checkouts write. Under 'worktrees/' only the files that say what another worktree has
    // checked out count, and a worktree folder appearing or going, i.e. added, removed or pruned.
    // Its own HEAD, in the main worktree, is where the common dir is '.git' itself.
    internal void CommonDirChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        if (path == null)
            return;

        if (path == GitHeadFile || path.StartsWith(GitRefsPath))
        {
            RepoChange(fullPath, path, changeType);
            return;
        }

        if (path.StartsWith(GitWorktreesPath))
        {
            var parts = path.Split(Path.DirectorySeparatorChar);
            var isWorktreeFolder = parts.Length == 2 && changeType is WatcherChangeTypes.Deleted;
            var isStateFile = parts.Length == 3 && WorktreeStateFiles.Contains(parts[2]);
            if (isWorktreeFolder || isStateFile)
            {
                RepoChange(fullPath, path, changeType);
            }
        }
    }

    // A change in a linked worktree's own git dir, which holds its HEAD
    internal void GitDirChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        if (path == GitHeadFile)
        {
            RepoChange(fullPath, path, changeType);
        }
    }

    internal void RepoChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        // Log.Debug($"'{fullPath}'");

        if (
            Path.GetExtension(fullPath) == ".lock"
            || Directory.Exists(fullPath)
            || fullPath.Contains("gmd-metadata-key-value")
        )
        {
            return;
        }

        // Log.Info($"Repo change for '{fullPath}' {changeType}");
        lock (syncRoot)
        {
            repoChangedEvent = new ChangeEvent(Now());
        }
    }

    internal void FileChange(string fullPath)
    {
        // Log.Info($"Status change '{fullPath}'");
        lock (syncRoot)
        {
            fileChangedEvent = new ChangeEvent(Now());
        }
    }

    bool IsExcluded(string? fullPath)
    {
        if (fullPath == null || excludedFolders.Count == 0)
            return false;

        var path = Path.GetFullPath(fullPath);
        return excludedFolders.Any(f => path.StartsWith(f, PathComparison));
    }

    static string NormalizedFolder(string folder) =>
        Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    IReadOnlyList<Glob> GetMatches(string workingFolder)
    {
        List<Glob> patterns = [];
        string gitIgnorePath = Path.Combine(workingFolder, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
            return patterns;
        }

        string[] gitIgnore = File.ReadAllLines(gitIgnorePath);
        foreach (string line in gitIgnore)
        {
            string pattern = line;

            int index = pattern.IndexOf("#");
            if (index > -1)
            {
                if (index == 0)
                {
                    continue;
                }

                pattern = pattern.Substring(0, index);
            }

            pattern = pattern.Trim();
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            if (pattern.EndsWith("/"))
            {
                pattern = pattern + "**/*";
                if (pattern.StartsWith("/"))
                {
                    pattern = pattern.Substring(1);
                }
                else
                {
                    pattern = "**/" + pattern;
                }
            }

            try
            {
                patterns.Add(new Glob(pattern));
            }
            catch (Exception)
            {
                // Log.Debug($"Failed to add pattern {pattern}, {e.Message}");
            }
        }

        return patterns;
    }

    bool IsIgnored(string path)
    {
        foreach (Glob matcher in matchers)
        {
            if (matcher.IsMatch(path))
            {
                // Log.Info($"Ignoring '{path}'");.
                return true;
            }
        }

        // Log.Info($"Allow '{path}'");
        return false;
    }
}
