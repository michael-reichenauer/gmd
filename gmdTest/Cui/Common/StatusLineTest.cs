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

    [TestMethod]
    public void TestNothingIsShownBeforeAMessage()
    {
        Assert.IsNull(new StatusLine().Current);
    }
}
