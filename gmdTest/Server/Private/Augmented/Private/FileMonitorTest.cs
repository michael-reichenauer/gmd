using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// FileMonitor watches the working folder and turns a burst of file system events into at most one
// change event per second. Characterization tests: they pin what the debounce does today.
//
// Both halves used to need the Terminal.Gui main loop, so none of this was reachable without a
// driver. FakeMainThread now stands in for it, which is what lets these tests drive the timer tick
// by hand and read the events back. Time is driven the same way, through FileMonitor.Now, so the
// one second trigger delays cost no wall clock time.
[TestClass]
public class FileMonitorTest
{
    // Any path outside .git, i.e. what a user editing a file produces
    const string filePath = "/wd/file.txt";

    // A ref changing is what git writes on commit, fetch, checkout, ...
    const string refPath = "/wd/.git/refs/heads/main";

    static readonly DateTime startTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // Just past the one second trigger delay, which is compared with '<'
    static readonly TimeSpan pastDelay = TimeSpan.FromMilliseconds(1001);

    DateTime now = startTime;
    FakeMainThread mainThread = null!;
    FileMonitor monitor = null!;
    List<ChangeEvent> fileEvents = null!;
    List<ChangeEvent> repoEvents = null!;

    [TestInitialize]
    public void Setup()
    {
        now = startTime;
        mainThread = new FakeMainThread();
        monitor = new FileMonitor(mainThread) { Now = () => now };
        fileEvents = [];
        repoEvents = [];
        monitor.FileChanged += e => fileEvents.Add(e);
        monitor.RepoChanged += e => repoEvents.Add(e);

        // Starts the timer, so every test below ticks through the real registered callback. The
        // folder does not have to exist, see TestMonitorStartsTheTimerEvenIfTheFolderIsNotARepo.
        monitor.Monitor("/no/such/folder", []);
    }

    [TestMethod]
    public void TestMonitorStartsTheTimerEvenIfTheFolderIsNotARepo()
    {
        // Setup already called Monitor once with a folder that is not a repo
        Assert.IsNotNull(mainThread.Periodic);
        Assert.AreEqual(1, mainThread.PeriodicCount);
        Assert.AreEqual(TimeSpan.FromSeconds(1), mainThread.Interval);

        // Monitoring another folder reuses the one timer
        monitor.Monitor("/no/such/other/folder", []);
        Assert.AreEqual(1, mainThread.PeriodicCount);
    }

    [TestMethod]
    public void TestRepoChangeIsRaisedOnlyAfterTheTriggerDelay()
    {
        monitor.RepoChange(refPath, "refs/heads/main", WatcherChangeTypes.Changed);

        mainThread.Tick(); // Same instant, too fresh
        Assert.AreEqual(0, repoEvents.Count);

        now += TimeSpan.FromSeconds(1); // Exactly the delay is still not past it
        mainThread.Tick();
        Assert.AreEqual(0, repoEvents.Count);

        now += TimeSpan.FromMilliseconds(1);
        mainThread.Tick();
        Assert.AreEqual(1, repoEvents.Count);
        Assert.AreEqual(startTime, repoEvents[0].TimeStamp);

        mainThread.Tick(); // Raised once, the pending event was consumed
        Assert.AreEqual(1, repoEvents.Count);
    }

    [TestMethod]
    public void TestFileChangeIsRaisedOnlyAfterTheTriggerDelay()
    {
        monitor.FileChange(filePath);

        mainThread.Tick();
        Assert.AreEqual(0, fileEvents.Count);

        now += pastDelay;
        mainThread.Tick();
        Assert.AreEqual(1, fileEvents.Count);

        mainThread.Tick();
        Assert.AreEqual(1, fileEvents.Count);
    }

    // A burst of changes is one event, and each change replaces the pending one, so the delay is
    // measured from the latest change rather than the first. A folder that keeps being written to
    // therefore keeps deferring the event, it is not raised every second while the writing lasts.
    [TestMethod]
    public void TestTheDelayIsMeasuredFromTheLatestChangeNotTheFirst()
    {
        monitor.FileChange(filePath);
        now += TimeSpan.FromMilliseconds(500);
        monitor.FileChange(filePath);

        now += TimeSpan.FromMilliseconds(501); // Past the delay of the first change, not the latest
        mainThread.Tick();
        Assert.AreEqual(0, fileEvents.Count);

        now += TimeSpan.FromMilliseconds(500); // Now past the delay of the latest change too
        mainThread.Tick();
        Assert.AreEqual(1, fileEvents.Count);
        Assert.AreEqual(startTime.AddMilliseconds(500), fileEvents[0].TimeStamp);
    }

    // A repo change means the whole repo is re-read, which includes the status, so the file
    // change is dropped rather than raised after it
    [TestMethod]
    public void TestRepoChangeReplacesTheFileChangeOfTheSameTick()
    {
        monitor.FileChange(filePath);
        monitor.RepoChange(refPath, "refs/heads/main", WatcherChangeTypes.Changed);
        now += pastDelay;

        mainThread.Tick();
        Assert.AreEqual(1, repoEvents.Count);
        Assert.AreEqual(0, fileEvents.Count);

        now += pastDelay; // Dropped, not deferred to a later tick
        mainThread.Tick();
        Assert.AreEqual(0, fileEvents.Count);
    }

    // Pause is held while gmd runs a git command, so its own writes do not look like a change the
    // user made. The events are deferred, not dropped, since the pause may hide a real change too.
    [TestMethod]
    public void TestPauseDefersEventsUntilResumed()
    {
        using (monitor.Pause())
        {
            monitor.RepoChange(refPath, "refs/heads/main", WatcherChangeTypes.Changed);
            now += pastDelay;

            mainThread.Tick();
            Assert.AreEqual(0, repoEvents.Count);
        }

        mainThread.Tick();
        Assert.AreEqual(1, repoEvents.Count);
    }

    [TestMethod]
    public void TestSetReadStatusTimeClearsOnlyTheFileChange()
    {
        monitor.FileChange(filePath);
        monitor.RepoChange(refPath, "refs/heads/main", WatcherChangeTypes.Changed);

        monitor.SetReadStatusTime(now);
        now += pastDelay;

        mainThread.Tick();
        Assert.AreEqual(0, fileEvents.Count);
        Assert.AreEqual(1, repoEvents.Count);
    }

    [TestMethod]
    public void TestSetReadRepoTimeClearsBothChanges()
    {
        monitor.FileChange(filePath);
        monitor.RepoChange(refPath, "refs/heads/main", WatcherChangeTypes.Changed);

        monitor.SetReadRepoTime(now);
        now += pastDelay;

        mainThread.Tick();
        Assert.AreEqual(0, fileEvents.Count);
        Assert.AreEqual(0, repoEvents.Count);
    }

    // Git writes a '.lock' file next to every ref it updates, and gmd writes its own branch
    // metadata into the repo, so neither is a change worth re-reading the repo for
    [TestMethod]
    public void TestLockFilesMetaDataAndFoldersAreNotRepoChanges()
    {
        monitor.RepoChange(refPath + ".lock", "refs/heads/main.lock", WatcherChangeTypes.Created);
        monitor.RepoChange("/wd/.git/gmd-metadata-key-value", "gmd-metadata-key-value", WatcherChangeTypes.Changed);
        monitor.RepoChange(Path.GetTempPath(), "refs", WatcherChangeTypes.Changed); // A folder

        now += pastDelay;
        mainThread.Tick();
        Assert.AreEqual(0, repoEvents.Count);
    }

    // The repository layouts below are written by hand, like GitDirTest does, so no git is needed.
    // 'main' is a main worktree, 'main-dev' a linked worktree of it, and 'main/.claude/worktrees/x'
    // a linked worktree nested inside the main one, where Claude Code puts its own.
    string root = "";
    string Main => Path.Join(root, "main");
    string MainGitDir => Path.Join(Main, ".git");

    string CreateMain()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-monitor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(MainGitDir, "refs", "heads"));
        return Main;
    }

    string CreateLinkedWorktree(string path, string name)
    {
        var gitDir = Path.Join(MainGitDir, "worktrees", name);
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Join(gitDir, "commondir"), "../..\n");
        File.WriteAllText(Path.Join(gitDir, "HEAD"), "ref: refs/heads/dev\n");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Join(path, ".git"), $"gitdir: {gitDir}\n");
        return path;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (root != "" && Directory.Exists(root))
            Directory.Delete(root, true);
    }

    // The main worktree keeps everything under '.git', so its refs are watched from there. A
    // linked worktree has its refs in the main repository's '.git' and its HEAD in a folder of
    // its own under it, so three folders are watched: the working folder, the common dir and the
    // worktree's git dir.
    [TestMethod]
    public void TestMainAndLinkedWorktreesWatchTheirGitDirs()
    {
        var main = CreateMain();
        var worktree = CreateLinkedWorktree(Path.Join(root, "main-dev"), "dev");

        monitor.Monitor(main, []);
        CollectionAssert.AreEqual(new[] { main, MainGitDir }, monitor.WatchedPaths.ToArray());

        monitor.Monitor(worktree, []);
        CollectionAssert.AreEqual(
            new[]
            {
                worktree,
                Path.GetFullPath(MainGitDir),
                Path.GetFullPath(Path.Join(MainGitDir, "worktrees", "dev")),
            },
            monitor.WatchedPaths.ToArray()
        );
    }

    // A worktree nested inside the working folder is another checkout, with another status, so
    // what is written there is not a change here — it would otherwise re-read this status for
    // every file a build in that worktree writes
    [TestMethod]
    public void TestChangesInsideAnExcludedFolderAreNotFileChanges()
    {
        var main = CreateMain();
        var nested = Path.Join(main, ".claude", "worktrees", "x");
        monitor.Monitor(main, [nested]);

        monitor.WorkingFolderChange(
            Path.Join(nested, "a.txt"),
            ".claude/worktrees/x/a.txt",
            WatcherChangeTypes.Changed
        );
        now += pastDelay;
        mainThread.Tick();
        Assert.AreEqual(0, fileEvents.Count);

        monitor.WorkingFolderChange(Path.Join(main, "a.txt"), "a.txt", WatcherChangeTypes.Changed);
        now += pastDelay;
        mainThread.Tick();
        Assert.AreEqual(1, fileEvents.Count);
    }

    // The nested worktrees are re-read with the repo, so they are taken even when the folder is
    // the one already being watched
    [TestMethod]
    public void TestExcludedFoldersAreUpdatedForTheFolderAlreadyWatched()
    {
        var main = CreateMain();
        var nested = Path.Join(main, ".claude", "worktrees", "x");
        monitor.Monitor(main, []);
        monitor.Monitor(main, [nested]);

        monitor.WorkingFolderChange(
            Path.Join(nested, "a.txt"),
            ".claude/worktrees/x/a.txt",
            WatcherChangeTypes.Changed
        );
        now += pastDelay;
        mainThread.Tick();
        Assert.AreEqual(0, fileEvents.Count);
    }

    // In a linked worktree '.git' is a file git rewrites when the worktree is moved; in the main
    // worktree it is a folder whose timestamp changes whenever git writes into it
    [TestMethod]
    public void TestTheGitFileOfALinkedWorktreeIsARepoChangeButTheGitFolderIsNot()
    {
        var main = CreateMain();
        var worktree = CreateLinkedWorktree(Path.Join(root, "main-dev"), "dev");

        monitor.WorkingFolderChange(Path.Join(main, ".git"), ".git", WatcherChangeTypes.Changed);
        now += pastDelay;
        mainThread.Tick();
        Assert.AreEqual(0, repoEvents.Count);

        monitor.WorkingFolderChange(Path.Join(worktree, ".git"), ".git", WatcherChangeTypes.Changed);
        now += pastDelay;
        mainThread.Tick();
        Assert.AreEqual(1, repoEvents.Count);
    }

    // What the common dir watcher raises on: a ref, the main worktree's HEAD, and of another
    // worktree only what says what it has checked out — its index is rewritten by every
    // 'git status' run there, which would otherwise re-read this repo each time
    [TestMethod]
    public void TestOnlyRefsAndOtherWorktreesCheckoutStateAreRepoChangesInTheCommonDir()
    {
        void Change(string path, WatcherChangeTypes type = WatcherChangeTypes.Changed) =>
            monitor.CommonDirChange("/main/.git/" + path, path, type);
        int Raised()
        {
            now += pastDelay;
            mainThread.Tick();
            return repoEvents.Count;
        }

        Change("index");
        Change("worktrees/y/index");
        Change("worktrees/y/logs/HEAD");
        Change("worktrees/y/ORIG_HEAD");
        Change("objects/ab/cdef");
        Assert.AreEqual(0, Raised());

        Change("refs/heads/main");
        Assert.AreEqual(1, Raised());
        Change("HEAD");
        Assert.AreEqual(2, Raised());
        Change("worktrees/y/HEAD");
        Assert.AreEqual(3, Raised());
        Change("worktrees/y/locked", WatcherChangeTypes.Created);
        Assert.AreEqual(4, Raised());
        Change("worktrees/y", WatcherChangeTypes.Deleted);
        Assert.AreEqual(5, Raised());
    }

    // The point of IMainThread: the timer runs on whichever thread the main loop uses, but the
    // events are posted rather than raised inline, so subscribers always get them on the UI thread
    [TestMethod]
    public void TestEventsArePostedToTheMainThreadRatherThanRaisedInline()
    {
        monitor.RepoChange(refPath, "refs/heads/main", WatcherChangeTypes.Changed);
        now += pastDelay;

        mainThread.Periodic!();
        Assert.AreEqual(1, mainThread.PostedCount);
        Assert.AreEqual(0, repoEvents.Count);

        mainThread.RunPosted();
        Assert.AreEqual(1, repoEvents.Count);
    }
}
