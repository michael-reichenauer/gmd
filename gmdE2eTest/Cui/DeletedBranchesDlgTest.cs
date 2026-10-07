using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// Restore Deleted Branch, end to end: a branch deleted from its branch menu is listed, by a later run
// of gmd too, since gmd records what it deletes, and restoring it puts it back where it was.
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class DeletedBranchesDlgTest
{
    [TestMethod]
    public async Task TestRestoreABranchDeletedHere()
    {
        using var repo = await E2eRepo.CreateAsync();
        var devTip = (await repo.GitAsync("rev-parse dev")).Trim();

        using (var gmd = TmuxSession.StartGmd(repo))
        {
            gmd.WaitFor("Initial");
            ShowAndHooverDev(gmd, "Merge branch");
            DeleteHooveredBranch(gmd, 8);

            var screen = gmd.WaitFor("Deleted 'dev'");
            Assert.AreEqual(
                "Deleted 'dev': Restore Deleted Branch in the Undo menu brings it back",
                ScreenText.LastLine(screen)
            );
        }

        // A later run of gmd has the record too. The time is when it was deleted, i.e. now.
        using var again = TmuxSession.StartGmd(repo);
        again.WaitFor("Initial");
        OpenRestoreDeletedBranch(again);

        // The deleted branch is still drawn, gray, since it was shown, and the list has the one row
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│    Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰┲   More dev work                                                         (~dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00







                      ╭ Restore Deleted Branch ──────────────────────────────────────────────────────────────────────────╮
                      │  Time            Branch              Deleted        Subject                                      │
                      │ ┌──────────────────────────────────────────────────────────────────────────────────────────────┐ │
                      │ │NN-NN-NN NN:NN  dev                 local          More dev work                              │ │
                      │ └──────────────────────────────────────────────────────────────────────────────────────────────┘ │
                      │  Restore creates 'dev' at af3ee6 again                                                           │
                      │ [ Restore ]                                                                           [ Close ]  │
                      ╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.MaskTimes(again.WaitFor("Restore creates"), "local"),
            repo.Path
        );

        // Shift or Caps Lock on, the key restores as 'r' and Enter do
        again.Send("R");
        again.WaitFor("Restored 'dev'");
        Assert.AreEqual(devTip, (await repo.GitAsync("rev-parse dev")).Trim());
    }

    // Deleted here and on origin, as the delete dialog does by default for a branch with a remote
    // branch, and restored on both. That is a push, so the sides are asked for first, as the delete
    // asked for them. Here 'Add zeta' is on top, not pushed, so the merge row is one further down, and
    // the commit menu opens on 'Amend ...', so Undo is two moves away rather than one.
    [TestMethod]
    public async Task TestRestoreABranchDeletedHereAndOnOrigin()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("push -q -u origin dev");
        var devTip = (await repo.GitAsync("rev-parse dev")).Trim();

        using (var gmd = TmuxSession.StartGmd(repo))
        {
            gmd.WaitFor("Initial");
            gmd.Send("Down");
            gmd.WaitForStable();
            ShowAndHooverDev(gmd, "Merge branch");
            DeleteHooveredBranch(gmd, 7);

            var screen = gmd.WaitFor("Deleted 'dev' here and on origin");
            Assert.AreEqual(
                "Deleted 'dev' here and on origin: Restore Deleted Branch in the Undo menu brings it back",
                ScreenText.LastLine(screen)
            );
        }
        Assert.AreEqual("", (await repo.GitAsync("ls-remote origin refs/heads/dev")).Trim());

        using var again = TmuxSession.StartGmd(repo);
        again.WaitFor("Initial");
        OpenRestoreDeletedBranch(again, 2);
        again.WaitFor("and pushes 'origin/dev' back");
        again.Send("Enter");

        Assert.AreEqual(
            """
                                                  ╭ Restore Branch ──────────────────────────╮
                                                  │ Restore: dev                             │
                                                  │                                          │
                                                  │ ◙ Restore Local                          │
                                                  │ ◙ Restore Remote                         │
                                                  │                                          │
                                                  │                                          │
                                                  │                                          │
                                                  │           [◦ OK ◦] [ Cancel ]            │
                                                  ╰──────────────────────────────────────────╯
            """,
            ScreenText.Rows(again.WaitFor("Restore: dev"), repo.Path, 15, 10)
        );
        again.Send("Enter");

        again.WaitFor("Restored 'dev' here and on origin");
        Assert.AreEqual(devTip, (await repo.GitAsync("rev-parse dev")).Trim());
        StringAssert.StartsWith(await repo.GitAsync("ls-remote origin refs/heads/dev"), devTip);
        Assert.AreEqual("origin/dev", (await repo.GitAsync("rev-parse --abbrev-ref dev@{upstream}")).Trim());
    }

    // Shows dev and hoovers it, as BranchTest.TestRenameBranch does: down to the merge commit, left
    // to hoover main, enter to open the branch merged in there, right to move the hoover on to dev
    static void ShowAndHooverDev(TmuxSession gmd, string mergeRow)
    {
        gmd.Send("Down");
        gmd.WaitFor(mergeRow);
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        gmd.Send("Right");
        gmd.WaitForStable();
    }

    // Down the branch menu to 'Delete Branch ...', which is a different number of moves with origin,
    // where more items are enabled, and OK in the delete dialog, whose boxes are on for each side
    static void DeleteHooveredBranch(TmuxSession gmd, int moves)
    {
        gmd.Send("m");
        gmd.WaitFor("Branch: dev");
        for (var i = 0; i < moves; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        gmd.WaitFor("Delete Local");
        gmd.Send("Enter");
        gmd.WaitUntilGone("Delete Local");
    }

    // Undo is the given number of moves down the commit menu, and in it the cursor starts on 'Undo
    // Commit', main's last change, so the item is two moves down, past 'Recover Lost Commits ...'
    static void OpenRestoreDeletedBranch(TmuxSession gmd, int movesToUndo = 1)
    {
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (var i = 0; i < movesToUndo; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Restore Deleted Branch");
        for (var i = 0; i < 2; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
    }
}
