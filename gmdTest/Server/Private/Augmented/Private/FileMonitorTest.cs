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
        monitor.Monitor("/no/such/folder");
    }

    [TestMethod]
    public void TestMonitorStartsTheTimerEvenIfTheFolderIsNotARepo()
    {
        // Setup already called Monitor once with a folder that is not a repo
        Assert.IsNotNull(mainThread.Periodic);
        Assert.AreEqual(1, mainThread.PeriodicCount);
        Assert.AreEqual(TimeSpan.FromSeconds(1), mainThread.Interval);

        // Monitoring another folder reuses the one timer
        monitor.Monitor("/no/such/other/folder");
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
