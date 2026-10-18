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

    // Config is the fourth item from the end of the repo menu: Config, Help, About and Quit
    static void OpenConfig(TmuxSession gmd)
    {
        gmd.Send("M");
        gmd.WaitFor("Repo Menu");
        gmd.Send("End");
        gmd.WaitForStable();
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Up");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        gmd.WaitFor("General:");
    }
}
