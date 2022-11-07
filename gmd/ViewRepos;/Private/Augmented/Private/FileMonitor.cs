using gmd.Utils.GlobPatterns;
using Timer = System.Timers.Timer;

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IFileMonitor
{
    event EventHandler<FileEventArgs> FileChanged;
    event EventHandler<FileEventArgs> RepoChanged;

    void Monitor(string workingFolder);
}

public delegate bool Ignorer(string path);

internal class FileEventArgs : EventArgs
{
    public DateTime DateTime { get; }

    public FileEventArgs(DateTime dateTime)
    {
        DateTime = dateTime;
    }
}

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


    public FileMonitor()
    {
        fileChangedTimer = new Timer(StatusDelayTriggerTime.TotalMilliseconds);
        fileChangedTimer.Elapsed += (s, e) => OnFileChangedTimer();


        workFolderWatcher.Changed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Created += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Deleted += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);
        workFolderWatcher.Renamed += (s, e) => WorkingFolderChange(e.FullPath, e.Name, e.ChangeType);

        repoChangedTimer = new Timer(RepositoryDelayTriggerTime.TotalMilliseconds);
        repoChangedTimer.Elapsed += (s, e) => OnRepoTimer();

        refsWatcher.Changed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Created += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Deleted += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
        refsWatcher.Renamed += (s, e) => RepoChange(e.FullPath, e.Name, e.ChangeType);
    }


    public event EventHandler<FileEventArgs>? FileChanged;

    public event EventHandler<FileEventArgs>? RepoChanged;

    public void Monitor(string workingFolder)
    {
        string refsPath = Path.Combine(workingFolder, GitFolder, GitRefsFolder);
        if (!Directory.Exists(workingFolder) || !Directory.Exists(refsPath))
        {
            Log.Warn($"Selected folder '{workingFolder}' is not a root working folder.");
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

        statusChangeTime = DateTime.Now;
        repoChangeTime = DateTime.Now;

        workFolderWatcher.EnableRaisingEvents = true;
        refsWatcher.EnableRaisingEvents = true;
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
        Log.Info($"Status change '{fullPath}'");
        lock (syncRoot)
        {
            isFileChanged = true;
            statusChangeTime = DateTime.Now;

            if (!fileChangedTimer.Enabled)
            {
                fileChangedTimer.Enabled = true;
            }
        }
    }


    private void RepoChange(string fullPath, string? path, WatcherChangeTypes changeType)
    {
        Log.Info($"'{fullPath}'");

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
            repoChangeTime = DateTime.Now;

            if (!repoChangedTimer.Enabled)
            {
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

        FileChanged?.Invoke(this, new FileEventArgs(statusChangeTime));
    }


    private void OnRepoTimer()
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

        RepoChanged?.Invoke(this, new FileEventArgs(repoChangeTime));
    }
}

// func (h *monitor) Start(ctx context.Context) error {
// 	go func() {
// 		<-ctx.Done()
// 		h.watcher.Close()
// 	}()
// 	go h.monitorFolderRoutine(ctx)
// 	go h.addWatchFoldersRecursively(ctx, h.rootFolderPath)
// 	return nil
// }


// func (h *monitor) addWatchFoldersRecursively(ctx context.Context, path string) {
// 	filepath.Walk(path, func(path string, fi os.FileInfo, err error) error {
// 		select {
// 		case <-ctx.Done():
// 			return nil
// 		default:
// 		}
// 		if err != nil {
// 			return err
// 		}
// 		if fi.Mode().IsDir() {
// 			return h.watcher.Add(path)
// 		}
// 		return nil
// 	})
// }

// func (h *monitor) monitorFolderRoutine(ctx context.Context) {
// 	gitPath := filepath.Join(h.rootFolderPath, ".git")
// 	gitFolderPath := gitPath + "/"
// 	refsPath := filepath.Join(gitPath, "refs")
// 	headPath := filepath.Join(gitPath, "HEAD")
// 	objectPath := filepath.Join(gitPath, "objects")
// 	fetchHeadPath := filepath.Join(gitPath, "FETCH_HEAD")
// 	defer close(h.Changes)

// 	for event := range h.watcher.Events {
// 		// log.Infof("event %v", event)
// 		select {
// 		case <-ctx.Done():
// 			return
// 		default:
// 		}
// 		if h.isNewFolder(event, objectPath) {
// 			log.Infof("New folder detected: %q", event.Name)
// 			go h.addWatchFoldersRecursively(ctx, event.Name)
// 			continue
// 		}
// 		if h.isIgnored(event.Name) {
// 			// log.Infof("ignoring: %s", event.Name)
// 		} else if h.isRepoChange(event.Name, fetchHeadPath, headPath, refsPath) {
// 			// log.Infof("Repo change: %s", event.Name)
// 			select {
// 			case h.Changes <- repoChange:
// 			case <-ctx.Done():
// 				return
// 			}
// 		} else if h.isStatusChange(event.Name, gitFolderPath) {
// 			// log.Infof("Status change: %s", event.Name)
// 			select {
// 			case h.Changes <- statusChange:
// 			case <-ctx.Done():
// 				return
// 			}
// 		} else {
// 			// fmt.Printf("ignoring: %s\n", event.Name)
// 		}
// 	}
// }

// func (h *monitor) isIgnored(path string) bool {
// 	if utils.DirExists(path) {
// 		return true
// 	}
// 	if h.ignorer != nil && h.ignorer.IsIgnored(path) {
// 		return true
// 	}
// 	return false
// }

// func (h *monitor) isStatusChange(path, gitFolderPath string) bool {
// 	return !strings.HasPrefix(path, gitFolderPath)
// }

// func (h *monitor) isRepoChange(path, fetchHeadPath, headPath, refsPath string) bool {
// 	if strings.HasSuffix(path, ".lock") {
// 		return false
// 	}
// 	if path == fetchHeadPath {
// 		return false
// 	}
// 	if path == headPath {
// 		return true
// 	}
// 	if strings.HasPrefix(path, refsPath) {
// 		return true
// 	}
// 	return false
// }

// func (h *monitor) isNewFolder(event fsnotify.Event, objectPath string) bool {
// 	if strings.HasPrefix(event.Name, objectPath) {
// 		return false
// 	}
// 	if event.Op != fsnotify.Create {
// 		return false
// 	}
// 	if utils.DirExists(event.Name) {
// 		return true
// 	}
// 	return false
// }
