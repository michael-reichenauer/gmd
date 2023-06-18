using gmd.Utils.GlobPatterns;
using Terminal.Gui;
using Timer = System.Timers.Timer;

namespace gmd.Server.Private.Augmented.Private;

interface IFileMonitor
{
    event Action<ChangeEvent> FileChanged;
    event Action<ChangeEvent> RepoChanged;

    void Monitor(string workingFolder);
    IDisposable Pause();
}

public delegate bool Ignorer(string path);


[SingleInstance]
class FileMonitor : IFileMonitor
{
    static readonly TimeSpan StatusDelayTriggerTime = TimeSpan.FromSeconds(2);
    static readonly TimeSpan RepositoryDelayTriggerTime = TimeSpan.FromSeconds(1);

    const string GitFolder = ".git";
    static readonly string GitFolderPath = ".git" + Path.DirectorySeparatorChar;
    const string GitRefsFolder = "refs";
    static readonly string GitHeadFile = Path.Combine(GitFolder, "HEAD");
    const NotifyFilters NotifyFilters =
            System.IO.NotifyFilters.LastWrite
            | System.IO.NotifyFilters.FileName
            | System.IO.NotifyFilters.DirectoryName;

    readonly FileSystemWatcher workFolderWatcher = new FileSystemWatcher();
    readonly FileSystemWatcher refsWatcher = new FileSystemWatcher();

    IReadOnlyList<Glob> matchers = new List<Glob>();

    readonly object syncRoot = new object();
    readonly object pauseLock = new object();

    private readonly Timer fileChangedTimer;
    private bool isFileChanged = false;
    private DateTime statusChangeTime;

    private readonly Timer repoChangedTimer;
    private bool isRepoChanged = false;
    private DateTime repoChangeTime;

    private string workingFolder = "";
    object timer = null!;
    Queue<ChangeEvent> fileChangedEvents = new Queue<ChangeEvent>();
    Queue<ChangeEvent> repoChangedEvents = new Queue<ChangeEvent>();

    public FileMonitor()
    {
        fileChangedTimer = new Timer(StatusDelayTriggerTime.TotalMilliseconds);
        fileChangedTimer.Elapsed += (s, e) => OnFileChanged();

        workFolderWatcher.Changed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Created += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Deleted += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Renamed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);

        repoChangedTimer = new Timer(RepositoryDelayTriggerTime.TotalMilliseconds);
        repoChangedTimer.Elapsed += (s, e) => OnRepoChanged();

        refsWatcher.Changed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Created += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Deleted += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Renamed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
    }

    bool OnTimer(MainLoop loop)
    {
        //Log.Info("File monitor timer");
        List<ChangeEvent> fileEvents = new List<ChangeEvent>();
        List<ChangeEvent> repoEvents = new List<ChangeEvent>();
        lock (syncRoot)
        {
            // Copy FileChangedEvents and RepoChangedEvents to local lists
            while (fileChangedEvents.Count > 0)
            {
                var e = fileChangedEvents.Dequeue();
                Log.Info($"File changed event {e.TimeStamp.Iso()}");
                fileEvents.Add(e);
            }
            while (repoChangedEvents.Count > 0)
            {
                var e = repoChangedEvents.Dequeue();
                Log.Info($"Repo changed event {e.TimeStamp.Iso()}");
                repoEvents.Add(e);
            }
        }

        fileEvents.ForEach(e => FileChanged?.Invoke(e));
        repoEvents.ForEach(e => RepoChanged?.Invoke(e));

        //Log.Info("File monitor timer done");

        return true;
    }

    public event Action<ChangeEvent>? FileChanged;

    public event Action<ChangeEvent>? RepoChanged;

    public void Monitor(string workingFolder)
    {
        if (timer == null)
        {
            timer = Cui.Common.UI.AddTimeout(TimeSpan.FromSeconds(1), OnTimer);
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
        fileChangedTimer.Enabled = false;
        repoChangedTimer.Enabled = false;

        matchers = GetMatches(workingFolder);

        workFolderWatcher.Path = workingFolder;
        workFolderWatcher.NotifyFilter = NotifyFilters;
        workFolderWatcher.Filter = "*.*";
        workFolderWatcher.IncludeSubdirectories = true;

        refsWatcher.Path = refsPath;
        refsWatcher.NotifyFilter = NotifyFilters;
        refsWatcher.Filter = "*.*";
        refsWatcher.IncludeSubdirectories = true;

        statusChangeTime = DateTime.UtcNow;
        repoChangeTime = DateTime.UtcNow;

        workFolderWatcher.EnableRaisingEvents = true;
        refsWatcher.EnableRaisingEvents = true;

        this.workingFolder = workingFolder;
    }


    public IDisposable Pause()
    {
        System.Threading.Monitor.Enter(pauseLock);
        Log.Info("Pause file monitor ...");

        return new Disposable(() =>
        {
            System.Threading.Monitor.Exit(pauseLock);
            Log.Info("Resume file monitor");
        });
    }


    private void WorkingFolderChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        WaitIfPaused();

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

    private void RepoChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        WaitIfPaused();

        // Log.Debug($"'{fullPath}'");

        if (Path.GetExtension(fullPath) == ".lock" ||
            Directory.Exists(fullPath) ||
            fullPath.Contains("gmd-metadata-key-value"))
        {
            return;
        }

        // Log.Debug($"Repo change for '{fullPath}' {changeType}");.

        lock (syncRoot)
        {
            isRepoChanged = true;
            repoChangeTime = DateTime.UtcNow;

            if (!repoChangedTimer.Enabled)
            {
                Log.Info("Repo changing ...");
                repoChangedTimer.Enabled = true;
            }
        }
    }

    void WaitIfPaused()
    {
        System.Threading.Monitor.Enter(pauseLock);
        System.Threading.Monitor.Exit(pauseLock);
    }


    private void FileChange(string fullPath)
    {
        // Log.Info($"Status change '{fullPath}'");
        lock (syncRoot)
        {
            isFileChanged = true;
            statusChangeTime = DateTime.UtcNow;

            if (!fileChangedTimer.Enabled)
            {
                Log.Info($"File changing for '{fullPath}' ...");
                fileChangedTimer.Enabled = true;
            }
        }
    }


    private IReadOnlyList<Glob> GetMatches(string workingFolder)
    {
        List<Glob> patterns = new List<Glob>();
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


    private bool IsIgnored(string path)
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


    private void OnFileChanged()
    {
        lock (syncRoot)
        {
            if (!isFileChanged)
            {
                fileChangedTimer.Enabled = false;
                return;
            }

            isFileChanged = false;

            Log.Info("File changed");
            fileChangedEvents.Enqueue(new ChangeEvent(statusChangeTime));
        }

        // Log.Info("File changed");
        // Threading.PostOnMain(() => FileChanged?.Invoke(new ChangeEvent(statusChangeTime)));
    }


    private void OnRepoChanged()
    {
        lock (syncRoot)
        {
            if (!isRepoChanged)
            {
                repoChangedTimer.Enabled = false;
                return;
            }

            isRepoChanged = false;

            Log.Info("Repo changed");
            repoChangedEvents.Enqueue(new ChangeEvent(repoChangeTime));
        }

        // Log.Info("Repo changed");
        // Threading.PostOnMain(() => RepoChanged?.Invoke(new ChangeEvent(repoChangeTime)));
    }
}
