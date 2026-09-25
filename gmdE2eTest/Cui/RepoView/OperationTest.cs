using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// An operation git stopped part way through, on conflicts: how it is reported when a command of
// gmd's runs into them, how it is shown for as long as it lasts, and the ways to finish or abort
// it, which are in the repo menu (Shift-M) and behind a click on it in the application bar.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class OperationTest
{
    // A merge in gmd that conflicts says what stopped and the way on, and offers to resolve, where
    // it used to put git's output in a red error box
    [TestMethod]
    public async Task TestAMergeThatConflictsSaysHowToGoOn()
    {
        using var repo = await E2eRepo.CreateWithConflictingBranchAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Change it on main");
        ShowDev(gmd);

        // Showing dev put the cursor on its commit: left to highlight dev, and merge it into main
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("e");

        var screen = gmd.WaitFor("Merge Stopped on Conflicts");
        Assert.IsFalse(screen.Contains("Error !"), "Not an error box");
        // The application bar says what is in progress, and the box what stopped and how to go on
        Assert.AreEqual(
            """
             Gmd Merging: 1 conflict {repo}, ●main, ©1                               (main) [Ϙ Search] ? X
            """,
            ScreenText.Rows(screen, repo.Path, 0, 1)
        );
        Assert.AreEqual(
            """
                                       ╭ Merge Stopped on Conflicts ────────────────────────────────────╮
                                       │Merge stopped on conflicts in 1 file:                           │
                                       │                                                                │
                                       │  long.txt                                                      │
                                       │                                                                │
                                       │Resolve them in the diff of the uncommitted changes, where Enter│
                                       │on a file opens it, then commit (c) to finish the merge.        │
                                       │Shift-M opens the repo menu, to abort it later.                 │
                                       │                                                                │
                                       │       [◦ Resolve Conflicts ◦] [ Abort Merge ] [ Later ]        │
                                       ╰────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(screen, repo.Path, 14, 11)
        );

        // Resolve Conflicts is the default, and opens the diff where Enter resolves a file
        gmd.Send("Enter");
        gmd.WaitFor("Conflicts:   long.txt");
    }

    // Abort in the box throws the merge away there and then, with no second question, since nothing
    // has been resolved yet to lose; the application bar has nothing in progress to show after
    [TestMethod]
    public async Task TestAbortInTheBoxAbortsTheMerge()
    {
        using var repo = await E2eRepo.CreateWithConflictingBranchAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Change it on main");
        ShowDev(gmd);
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("e");
        gmd.WaitFor("Merge Stopped on Conflicts");

        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("Merging:"), "Change it on main");
        Assert.IsFalse(File.Exists(Path.Join(repo.Path, ".git", "MERGE_HEAD")), "The merge is aborted");
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"), "and the working folder is as it was");
    }

    // For as long as it lasts, the application bar says what is in progress and what is left, and a
    // click on that opens its items
    [TestMethod]
    public async Task TestTheApplicationBarShowsTheOperation()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);

        var screen = gmd.WaitFor("Merging: 1 conflict");
        var (x, y) = TmuxSession.PositionOf(screen, "Merging: 1 conflict");
        gmd.Click(x + 2, y);

        var menu = gmd.WaitFor("Resolve Conflicts ...");
        StringAssert.Contains(menu, "Abort Merge");
    }

    // Shift-M opens the repo menu, which is where Abort is, headed by what is in progress
    [TestMethod]
    public async Task TestShiftMOpensTheRepoMenu()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Merging: 1 conflict");

        gmd.Send("M");

        StringAssert.Contains(gmd.WaitFor("Repo Menu"), "Abort Merge");
    }

    // 'dev' is not shown at first, so it is found and shown by name
    static void ShowDev(TmuxSession gmd)
    {
        gmd.Send("S-Right");
        gmd.WaitFor("type to find");
        gmd.Send("d");
        gmd.WaitFor("Find Branch");
        gmd.SendText("ev");
        gmd.WaitFor("Name: dev");
        gmd.Send("Enter");
        gmd.WaitFor("Change it on dev");
    }
}
