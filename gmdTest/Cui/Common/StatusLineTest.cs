using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// The status line's message and how long it is shown. The clock is the test's, so the seconds
// pass when the test says so.
[TestClass]
public class StatusLineTest
{
    [TestMethod]
    public void TestTheLastMessageIsShownAndSaysWhatKindItIs()
    {
        var now = DateTime.UtcNow;
        var status = new StatusLine { Now = () => now };
        var changes = 0;
        status.Changed += () => changes++;

        status.Info("Pushed 'main'");
        status.Notice("Nothing to commit");

        Assert.AreEqual("Nothing to commit", status.Current?.Text);
        Assert.AreEqual(StatusKind.Notice, status.Current?.Kind);
        Assert.AreEqual(2, changes, "Every message is announced, so the line is redrawn for it");
    }

    // A tip is for reading rather than a glance, e.g. that branches are hidden, so it stays three
    // times as long as the rest
    [TestMethod]
    public void TestATipIsShownLongerThanAMessage()
    {
        var now = DateTime.UtcNow;
        var status = new StatusLine { Now = () => now };

        status.Tip("Showing main; 1 other branch is hidden");
        now += TimeSpan.FromSeconds(10);
        Assert.AreEqual("Showing main; 1 other branch is hidden", status.Current?.Text, "Still shown at 10 s");
        Assert.AreEqual(StatusKind.Info, status.Current?.Kind);

        now += TimeSpan.FromSeconds(6);
        Assert.IsNull(status.Current, "Gone at 16 s");
    }

    // Shown for a few seconds, and then not, with nothing else to do: the key hints come back
    [TestMethod]
    public void TestAMessageIsGoneAfterItsSeconds()
    {
        var now = DateTime.UtcNow;
        var status = new StatusLine { Now = () => now };

        status.Failure("Fetch failed: Could not resolve host");
        now += StatusLine.Duration - TimeSpan.FromMilliseconds(1);
        Assert.IsNotNull(status.Current, "Still shown just before its time is up");

        now += TimeSpan.FromMilliseconds(1);
        Assert.IsNull(status.Current, "and gone when it is");
    }

    // What a command is doing is shown for as long as it takes, not for the seconds of a message,
    // and taken away when it is done, so the key hints come back
    [TestMethod]
    public void TestAProgressIsShownUntilItIsDone()
    {
        var now = DateTime.UtcNow;
        var status = new StatusLine { Now = () => now };
        var changes = 0;
        status.Changed += () => changes++;

        var working = status.Progress("Pushing 'main'");
        now += StatusLine.Duration * 3;
        Assert.AreEqual("Pushing 'main'...", status.Current?.Text, "Still shown long after a message would be gone");
        Assert.AreEqual(StatusKind.Progress, status.Current?.Kind);

        working.Dispose();
        Assert.IsNull(status.Current);
        Assert.AreEqual(2, changes, "Shown and taken away, and the line redrawn for both");
    }

    // What the command did replaces what it was doing, and stays for its seconds after the progress
    // is done, which is how the line goes from "Pushing 'main'..." to "Pushed 'main'"
    [TestMethod]
    public void TestWhatWasDoneReplacesTheProgress()
    {
        var now = DateTime.UtcNow;
        var status = new StatusLine { Now = () => now };

        using (status.Progress("Pushing 'main'"))
        {
            status.Info("Pushed 'main'");
        }

        Assert.AreEqual("Pushed 'main'", status.Current?.Text);
        now += StatusLine.Duration;
        Assert.IsNull(status.Current, "and gone after its seconds, like any message");
    }

    // One step after another, as pulling all branches does: each step replaces the one before, and
    // the one done before it cannot take the next one away
    [TestMethod]
    public void TestTheNextProgressIsNotTakenAwayByTheOneBefore()
    {
        var status = new StatusLine();

        var first = status.Progress("Updating 'dev'");
        var second = status.Progress("Updating 'feature'");
        first.Dispose();

        Assert.AreEqual("Updating 'feature'...", status.Current?.Text);
        second.Dispose();
        Assert.IsNull(status.Current);
    }

    [TestMethod]
    public void TestNothingIsShownBeforeAMessage()
    {
        Assert.IsNull(new StatusLine().Current);
    }
}
