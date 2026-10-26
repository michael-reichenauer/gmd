using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.App;

// The Config dialog, from the repo menu
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class ConfigDlgTest
{
    // Tab, or an arrow key the open list of Colors has no use for, which Terminal.Gui 2.x moves the
    // focus with, moves it out of the list to a field beside, and the list closes. It closed as the
    // focus left it, within Terminal.Gui's change of the focus, and put the focus back on its field,
    // which 2.x threw on: gmd crashed.
    [TestMethod]
    [DataRow("Tab")]
    [DataRow("Left")]
    public async Task TestAKeyThatLeavesTheOpenListClosesIt(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenConfig(gmd);
        var (x, y) = TmuxSession.PositionOf(gmd.WaitFor("Colors:"), "▼");
        gmd.Click(x, y);
        gmd.WaitFor("│light ");

        gmd.Send(key);

        StringAssert.Contains(gmd.WaitUntilGone("│light "), "Colors:", "The dialog is still up");
        Assert.IsTrue(gmd.IsRunning);
    }

    // Sharing the branch structure pushes to origin, so turning it on says what is pushed first, and
    // Cancel, the default, leaves it off; Share turns it on; and turning it off says that what was
    // pushed stays on origin, and how it is removed
    [TestMethod]
    public async Task TestTurningSharingOnAndOffSaysWhatItMeans()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        ToggleSharing(gmd, "◙ Share the branch structure");
        StringAssert.Contains(gmd.WaitFor("Share Branch Structure"), "refs/gmd-metadata-key-value/data");
        gmd.Send("Enter");
        gmd.WaitUntilGone("Share Branch Structure");
        Assert.IsFalse(IsSharing(repo), "Cancel is the default");

        ToggleSharing(gmd, "◙ Share the branch structure");
        gmd.WaitFor("Share Branch Structure");
        gmd.Send("M-s");
        gmd.WaitUntilGone("Share Branch Structure");
        Assert.IsTrue(IsSharing(repo));

        ToggleSharing(gmd, "□ Share the branch structure");
        StringAssert.Contains(
            gmd.WaitFor("What was pushed stays on origin"),
            "git push origin :refs/gmd-metadata-key-value/data"
        );
        gmd.Send("Enter");
        gmd.WaitUntilGone("What was pushed stays on origin");
        Assert.IsFalse(IsSharing(repo));
    }

    // Opens the Config dialog, clicks the box, and presses OK, the default button
    static void ToggleSharing(TmuxSession gmd, string toggled)
    {
        OpenConfig(gmd);
        var (x, y) = TmuxSession.PositionOf(gmd.WaitFor("Share the branch structure"), "Share the branch structure");
        gmd.Click(x, y);
        gmd.WaitFor(toggled);
        gmd.Send("Enter");
        gmd.WaitUntilGone("General:");
    }

    static bool IsSharing(TempRepo repo) =>
        File.ReadAllText(Path.Join(repo.Path, ".git", ".gmdconfig")).Contains("\"SyncMetaData\": true");

    static void OpenConfig(TmuxSession gmd)
    {
        gmd.Send("M");
        gmd.WaitFor("Repo Menu");
        gmd.SelectMenuItem("Config ...");
        gmd.Send("Enter");
        gmd.WaitFor("General:");
    }
}
