using gmd.Cui;
using gmd.Utils.GlobPatterns;
using Timer = System.Timers.Timer;

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IFileMonitor
{
    event EventHandler<ChangeEventArgs> FileChanged;
    event EventHandler<ChangeEventArgs> RepoChanged;

    void Monitor(string workingFolder);
}

public delegate bool Ignorer(string path);


[SingleInstance]
class FileMonitor : IFileMonitor
{
    static readonly TimeSpan StatusDelayTriggerTime = TimeSpan.FromSeconds(2);
    static readonly TimeSpan RepositoryDelayTriggerTime = TimeSpan.FromSeconds(1);

    const string GitFolder = ".git";
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

    private readonly Timer fileChangedTimer;
    private bool isFileChanged = false;
    private DateTime statusChangeTime;

    private readonly Timer repoChangedTimer;
    private bool isRepoChanged = false;
    private DateTime repoChangeTime;

    private string workingFolder = "";


    public FileMonitor()
    {
        fileChangedTimer = new Timer(StatusDelayTriggerTime.TotalMilliseconds);
        fileChangedTimer.Elapsed += (s, e) => OnFileChangedTimer();

        workFolderWatcher.Changed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Created += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Deleted += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Renamed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);

        repoChangedTimer = new Timer(RepositoryDelayTriggerTime.TotalMilliseconds);
        repoChangedTimer.Elapsed += (s, e) => OnRepoChangedTimer();

        refsWatcher.Changed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Created += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Deleted += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Renamed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
    }


    public event EventHandler<ChangeEventArgs>? FileChanged;

    public event EventHandler<ChangeEventArgs>? RepoChanged;

    public void Monitor(string workingFolder)
    {
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


    private void WorkingFolderChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        if (path == GitHeadFile)
        {
            RepoChange(fullPath, path, changeType);
            return;
        }

        // Log.Info($"'{path}', '{fullPath}'");

        if (path == null || !path.StartsWith(GitFolder))
        {
            if (path != null && IsIgnored(path))
            {
                Log.Info($"Ignored: '{fullPath}'");
                return;
            }

            if (fullPath != null && !Directory.Exists(fullPath))
            {
                // Log.Debug($"Status change for '{fullPath}' {changeType}");
                FileChange(fullPath);
            }
        }
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
                Log.Info("In progress ...");
                fileChangedTimer.Enabled = true;
            }
        }
    }


    private void RepoChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        // Log.Info($"'{fullPath}'");

        if (Path.GetExtension(fullPath) == ".lock")
        {
            return;
        }
        else if (Directory.Exists(fullPath))
        {
            return;
        }

        // Log.Debug($"Repo change for '{fullPath}' {changeType}");

        lock (syncRoot)
        {
            isRepoChanged = true;
            repoChangeTime = DateTime.UtcNow;

            if (!repoChangedTimer.Enabled)
            {
                Log.Info("In progress ...");
                repoChangedTimer.Enabled = true;
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
            catch (Exception e)
            {
                Log.Debug($"Failed to add pattern {pattern}, {e.Message}");
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
                // Log.Info($"Ignoring '{path}'");
                return true;
            }
        }

        // Log.Info($"Allow '{path}'");
        return false;
    }


    private void OnFileChangedTimer()
    {
        lock (syncRoot)
        {
            if (!isFileChanged)
            {
                fileChangedTimer.Enabled = false;
                return;
            }

            isFileChanged = false;
        }

        Log.Info("On File change");
        UI.Post(() => FileChanged?.Invoke(this, new ChangeEventArgs(statusChangeTime)));
    }


    private void OnRepoChangedTimer()
    {
        lock (syncRoot)
        {
            if (!isRepoChanged)
            {
                repoChangedTimer.Enabled = false;
                return;
            }

            isRepoChanged = false;
        }

        Log.Info("On Repo change");
        UI.Post(() => RepoChanged?.Invoke(this, new ChangeEventArgs(repoChangeTime)));
    }
}
