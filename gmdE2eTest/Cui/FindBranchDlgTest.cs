using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// Finding a branch by typing its name: typing in the Open Branch menu opens the Find Branch
// dialog with what was typed, the list narrows as the name is typed, and Enter shows the branch.
// What matches, and in what order, is BranchFinderTest's.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class FindBranchDlgTest
{
    // 'dev' is hidden in the fixture, so showing it is what picking it does
    [TestMethod]
    public async Task TestTypingInTheOpenBranchMenuFindsABranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("S-Right");
        gmd.WaitFor("type to find");

        gmd.Send("d");

        // The dialog with what was typed, and the one branch that has it at the start of its name.
        // It is as tall as the two branches of the fixture need.
        Assert.AreEqual(
            """
                                  ╭ Find Branch ─────────────────────────────────────────────────────────────╮
                                  │ Name: d                                                                ] │
                                  │──────────────────────────────────────────────────────────────────────────│
                                  │   dev                                                              T U   │
                                  │                                                                          │
                                  │                                                                          │
                                  │ 1 of 2 branches   ↑↓ select  Enter show                                  │
                                  ╰──────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Find Branch"), repo.Path, 16, 8)
        );

        gmd.SendText("ev");
        gmd.WaitFor("Name: dev");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("Find Branch"), "More dev work", "dev is shown");
    }

    // Escape goes back to the log with nothing shown
    [TestMethod]
    public async Task TestEscapeFindsNothing()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("S-Right");
        gmd.WaitFor("type to find");
        gmd.Send("d");
        gmd.WaitFor("Find Branch");

        gmd.Send("Escape");

        Assert.IsFalse(gmd.WaitUntilGone("Find Branch").Contains("More dev work"), "Nothing is shown");
        Assert.IsTrue(gmd.IsRunning);
    }

    // A click on a branch in the list picks it, as Enter does
    [TestMethod]
    public async Task TestClickingABranchPicksIt()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("S-Right");
        gmd.WaitFor("type to find");
        gmd.Send("d");

        var (x, y) = TmuxSession.PositionOf(gmd.WaitFor("Find Branch"), "   dev");
        gmd.Click(x + 3, y);

        StringAssert.Contains(gmd.WaitUntilGone("Find Branch"), "More dev work", "dev is shown");
    }

    // The same menu under Branches in the commit menu, where typing finds a branch as well: it is a
    // sub menu there, and the typing is handed on from the item it was opened from. 'Branches' is
    // second to last in the commit menu, and 'Show/Open Branch' the one below the shown branches.
    [TestMethod]
    public async Task TestTypingInTheShowOpenBranchSubMenuFindsABranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        foreach (var key in new[] { "End", "Up" })
        {
            gmd.Send(key);
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Show/Open Branch");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        gmd.WaitFor("╭ Show/Open Branch");

        gmd.Send("d");

        StringAssert.Contains(gmd.WaitFor("Find Branch"), "Name: d");
    }
}
