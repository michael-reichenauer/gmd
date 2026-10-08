using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// Recover Lost Commits, end to end: a commit a 'reset --hard' left behind is listed, and a branch
// made from the list brings it back.
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class LostWorkDlgTest
{
    // The commit menu opens on 'Amend ...', the last commit not being pushed, so Undo is three moves
    // down, past 'Commit Diff' and 'Mark for Diff'. In it the cursor starts on 'Undo Uncommit', the reset, so the item is one move down.
    [TestMethod]
    public async Task TestRecoverACommitAResetLeftBehind()
    {
        using var repo = await E2eRepo.CreateAsync();
        var lost = await repo.CommitFileAtAsync("lost.txt", "lost\n", "Lost work", TempRepo.BaseTime.AddMinutes(7));
        await repo.GitAsync("reset -q --hard HEAD~1");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Recover Lost Commits");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        // One line of work, the commit the reset left, on the branch it was made on; the line below
        // the list says what became of it
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                    (● main)[v1.0] 17d85b Test User      2024-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                4e73d2 Test User      2024-10-15 12:05
            ┣    Add gamma                                                                   4a15fb Test User      2024-10-15 12:04
            ┣╯   Add beta                                                                    dd7891 Test User      2024-10-15 12:01
            ┗    Initial                                                                     9dc406 Test User      2024-10-15 12:00









                 ╭ Recover Lost Commits ──────────────────────────────────────────────────────────────────────────────────────╮
                 │  Time              Branch              Commits  Lost by   Subject                                          │
                 │ ┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐ │
                 │ │2024-10-15 12:07  main                      1  reset     Lost work                                      │ │
                 │ └────────────────────────────────────────────────────────────────────────────────────────────────────────┘ │
                 │  cd56a3 by Test User: left behind on 'main' by a reset                                                     │
                 │ [ Diff ]  [ Create Branch ... ]                                                                 [ Close ]  │
                 ╰────────────────────────────────────────────────────────────────────────────────────────────────────────────╯
            """,
            gmd.WaitFor("Lost work"),
            repo.Path
        );

        gmd.Send("b");
        gmd.WaitFor("Create Branch at Commit");
        gmd.SendText("restored");
        gmd.WaitFor("restored");
        gmd.Send("Enter");

        gmd.WaitFor("(● restored)");
        Assert.AreEqual(lost, await repo.GitAsync("rev-parse restored"));
    }
}
