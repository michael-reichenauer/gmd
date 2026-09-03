using gmd.Utils.GlobPatterns;

namespace gmd.Server.Private.Augmented.Private;

interface IFileMonitor
{
    event Action<ChangeEvent> FileChanged;
    event Action<ChangeEvent> RepoChanged;

    void Monitor(string workingFolder);
    IDisposable Pause();
    void SetReadRepoTime(DateTime time);
    void SetReadStatusTime(DateTime time);
}

public delegate bool Ignorer(string path);

[SingleInstance]
class FileMonitor : IFileMonitor
{
    static readonly TimeSpan StatusDelayTriggerTime = TimeSpan.FromSeconds(1);
    static readonly TimeSpan RepositoryDelayTriggerTime = TimeSpan.FromSeconds(1);

    const string GitFolder = ".git";
    static readonly string GitFolderPath = ".git" + Path.DirectorySeparatorChar;
    const string GitRefsFolder = "refs";
    static readonly string GitHeadFile = Path.Combine(GitFolder, "HEAD");
    const NotifyFilters NotifyFilters =
        System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName;

    readonly FileSystemWatcher workFolderWatcher = new FileSystemWatcher();
    readonly FileSystemWatcher refsWatcher = new FileSystemWatcher();

    readonly IMainThread mainThread;

    IReadOnlyList<Glob> matchers = new List<Glob>();

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

        refsWatcher.Changed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Created += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Deleted += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Renamed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
    }

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

    public void Monitor(string workingFolder)
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
        string refsPath = Path.Combine(workingFolder, GitFolder, GitRefsFolder);
        if (!Directory.Exists(workingFolder) || !Directory.Exists(refsPath))
        {
            Log.Warn($"Selected folder '{workingFolder}' is not a root working folder.");
            return;
        }

        if (workingFolder == this.workingFolder)
        {
            // Already monitoring this folder
            return;
        }

        workFolderWatcher.EnableRaisingEvents = false;
        refsWatcher.EnableRaisingEvents = false;

        matchers = GetMatches(workingFolder);

        workFolderWatcher.Path = workingFolder;
        workFolderWatcher.NotifyFilter = NotifyFilters;
        workFolderWatcher.Filter = "*.*";
        workFolderWatcher.IncludeSubdirectories = true;

        refsWatcher.Path = refsPath;
        refsWatcher.NotifyFilter = NotifyFilters;
        refsWatcher.Filter = "*.*";
        refsWatcher.IncludeSubdirectories = true;

        workFolderWatcher.EnableRaisingEvents = true;
        refsWatcher.EnableRaisingEvents = true;

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

    void WorkingFolderChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        if (path == GitHeadFile)
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

            if (fullPath != null && !Directory.Exists(fullPath))
            {
                //Log.Debug($"Status change for '{fullPath}' {changeType}");.
                FileChange(fullPath);
            }
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
