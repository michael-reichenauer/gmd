using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// Branching, tagging, switching and merging.
//
// Three of these keys act on the *hoovered* branch rather than on the current row, which is the
// part that only an end-to-end test reaches:
// `Hoover` is unit tested as index math, but which branch a given key sequence ends up on, and
// therefore what 's' switches to and what 'e' merges, is a property of the running app.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class BranchTest
{
    // 'b' with no branch hoovered creates the branch at the current row's commit
    [TestMethod]
    public async Task TestCreateBranchFromACommit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("b");

        // Both check boxes are on by default: 'Checkout' is why HEAD moves below, and 'Publish' is
        // a push that fails silently here because the fixture has no origin — BranchCreateCommands
        // swallows exactly that error, and this is what pins that it still does
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00







                                                  ╭ Create Branch at Commit ─────────────────╮
                                                  │ From: main at 17d85b                     │
                                                  │                                          │
                                                  ││                                        ││
                                                  │└────────────────────────────────────────┘│
                                                  │ ◙ Checkout                               │
                                                  │ ◙ Publish                                │
                                                  │                                          │
                                                  │                                          │
                                                  │           [◦ OK ◦] [ Cancel ]            │
                                                  ╰──────────────────────────────────────────╯
            """,
            gmd.WaitFor("Create Branch at Commit"),
            repo.Path
        );

        gmd.SendText("feature");
        gmd.WaitFor("feature");
        gmd.Send("Enter");

        // The new branch is current and drawn as its own column, branching out of main
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●feature                                                    (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣─┺ ● Add delta                                            (main)(● feature)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("(● feature)"),
            repo.Path
        );

        Assert.AreEqual("feature", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("17d85ba889a1084f912c412d0ce435c9d7a36f53", await repo.GitAsync("rev-parse feature"));
    }

    // With a branch hoovered the same key creates from that branch instead, which is a different
    // command and a differently titled dialog. Cancelled, so it also pins that cancelling creates
    // nothing.
    [TestMethod]
    public async Task TestCreateBranchFromAHooveredBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Left"); // Hoovers main, the right most branch of the row
        gmd.WaitForStable();
        gmd.Send("b");
        var screen = gmd.WaitFor("Create Branch");

        Assert.AreEqual(
            """
                                                  ╭ Create Branch ───────────────────────────╮
                                                  │ From: main                               │
            """,
            ScreenText.Rows(screen, repo.Path, 14, 2)
        );

        gmd.Send("Escape");
        gmd.WaitUntilGone("Create Branch");
        Assert.AreEqual("  dev\n* main", await repo.GitAsync("branch"), "No branch should have been created");
    }

    // Renaming is menu only, so this is also the one test that drives a menu item all the way to
    // its command. The item is disabled for the main branch, hence renaming dev, and the fixture
    // has no origin, so this is the local half of a rename; the remote half is a push and a delete
    // of the old remote branch, which the integration tests cover.
    [TestMethod]
    public async Task TestRenameBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // Show dev and hoover it: down to the merge commit, left to hoover main, enter to open the
        // branch merged in there, right to move the hoover from main on to dev
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        gmd.Send("Right");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Branch: dev");

        // Down to 'Rename Branch ...', which is seven moves and not nine, since 'Rebase and push
        // on' and 'Pull/Update' are disabled here and are skipped over. One key at a time: a menu
        // redraw drops the keys sent behind it, so a single Send of seven would arrive as three.
        for (var i = 0; i < 7; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        // The name is filled in and the cursor is at its end, so the rename is a matter of editing
        // it. There is no remote branch here, so the dialog says nothing about origin.
        var dialog = gmd.WaitFor("Rename Branch");
        Assert.AreEqual(
            """
                                          ╭ Rename Branch ───────────────────────────────────────────╮
                                          │ From: dev                                                │
                                          │                                                          │
                                          ││dev                                                     ││
                                          │└────────────────────────────────────────────────────────┘│
                                          │                                                          │
                                          │                                                          │
                                          │                   [◦ OK ◦] [ Cancel ]                    │
                                          ╰──────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(dialog, repo.Path, 15, 9)
        );

        gmd.SendText("2");
        gmd.WaitFor("dev2");
        gmd.Send("Enter");

        // The branch is drawn under its new name, in the same column and with the same commits
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (dev2) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│    Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊   More dev work                                                         (dev2) af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("(dev2)"),
            repo.Path
        );

        Assert.AreEqual("  dev2\n* main", await repo.GitAsync("branch"));
        Assert.AreEqual("main", await repo.GitAsync("rev-parse --abbrev-ref HEAD"), "Renaming does not check out");
    }

    // Deleting a branch, which is one item below the rename above and so one move further down
    [TestMethod]
    public async Task TestDeleteBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        gmd.Send("Right");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Branch: dev");
        for (var i = 0; i < 8; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        gmd.WaitFor("Delete Branch");
        gmd.Send("Enter");

        gmd.WaitUntilGone("Delete Branch");
        StringAssert.DoesNotMatch(
            await repo.GitAsync("branch --list"),
            new System.Text.RegularExpressions.Regex(@"\bdev\b"),
            "The branch is gone from git"
        );
    }

    [TestMethod]
    public async Task TestAddATag()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("t");
        var screen = gmd.WaitFor("Add Tag");
        Assert.AreEqual(
            """
                                          ╭ Add Tag ─────────────────────────────────────────────────╮
                                          │ Name:                                                    │
                                          ││                         │                               │
                                          │└─────────────────────────┘                               │
                                          │ Message:                                                 │
            """,
            ScreenText.Rows(screen, repo.Path, 13, 5)
        );

        gmd.SendText("v2.0");
        gmd.WaitFor("v2.0");
        gmd.Send("Enter");

        // The new tag is drawn next to the one the fixture already has, on the current row
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                (● main)[v1.0][v2.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("[v2.0]"),
            repo.Path
        );

        Assert.AreEqual("v1.0\nv2.0", await repo.GitAsync("tag --points-at HEAD"));
    }

    // 's' switches to the hoovered branch, with no confirmation of any kind — one keystroke from
    // changing the working tree, which is why it is worth an end-to-end test.
    [TestMethod]
    public async Task TestSwitchToBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // Show dev: down to the merge commit, left to hoover main, enter to open the branch that
        // was merged in there
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitFor("Merge branch");
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");

        // The hoover is left on main, i.e. on the branch that is already current, and 's' on that
        // is deliberately a no-op (OnKeyS's PrimaryName guard). Worth pinning: it is the reason
        // 'show a branch and press s' does nothing, which reads like a dropped keystroke.
        gmd.Send("s");
        gmd.WaitForStable();
        Assert.AreEqual("main", await repo.GitAsync("rev-parse --abbrev-ref HEAD"), "'s' on the current branch");

        // One step right is dev, and there it does switch
        gmd.Send("Right");
        gmd.WaitForStable();
        gmd.Send("s");

        // The current markers moved: '●dev' in the application bar, '●' on dev's tip commit and
        // '(● dev)' on its branch tip, while main keeps its plain '(main)'
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●dev                                                         (dev) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣     Add delta                                                       (main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│    Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊ ● More dev work                                                        (● dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("(● dev)"),
            repo.Path
        );

        Assert.AreEqual("dev", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
    }

    // 'e' merges the hoovered branch into the current one. It does not commit by itself: the merge
    // is left uncommitted and the commit dialog opens on top of it with the message filled in,
    // which is the one thing about this flow that cannot be guessed from the key table.
    [TestMethod]
    public async Task TestMergeBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.GitAsync("checkout -q dev"); // Merge main into dev, since dev is already in main
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");

        gmd.Send("Left"); // Hoovers main, the branch the cursor row is on
        gmd.WaitForStable();
        gmd.Send("e");

        // The merge is in the working tree, as the uncommitted row says, and the dialog offers the
        // message git would have used
        var screen = gmd.WaitFor("Commit 2 changes");
        Assert.AreEqual(
            """
             ╭╊  ©Merge branch 'main' into dev, 2 uncommitted changes                  (● dev)                       NN-NN-NN NN:NN
            """,
            ScreenText.MaskTimes(ScreenText.Rows(screen, repo.Path, 2, 1), "uncommitted")
        );
        StringAssert.Contains(screen, "[Merge branch 'main' into dev");

        gmd.Send("Enter");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●dev                                                         (dev) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭╊ ● Merge branch 'main' into dev                                         (● dev) 60e4d8 Test User      24-10-15 12:07
            ┣╯┃   Add delta                                                       (main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮┃   Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│┃   Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊   More dev work                                                                af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted changes"),
            repo.Path
        );

        // A real merge commit, i.e. two parents: dev's tip and main's tip
        Assert.AreEqual("dev", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("af3ee69 17d85ba", await repo.GitAsync("log --format=%p -1"));
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
    }

    // The other arm of the same key: with the *current* branch hoovered there is nothing to merge
    // into, so it offers the branches to merge from instead
    [TestMethod]
    public async Task TestMergeFromMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitFor("Merge branch");
        gmd.Send("Enter"); // Shows dev, and leaves the hoover on main, which is current
        gmd.WaitFor("More dev work");

        gmd.Send("e");

        // Only the shown branches are offered, so the menu lists dev and nothing else. The stray
        // 'k' is the tail of 'More dev work' behind the menu, which is drawn over the log view.
        Assert.AreEqual(
            """
            ┣│ ╭ Merge from ─╮                                                                 4a15fb Test User      24-10-15 12:04
            ┃╰╊│ o  dev      │k                                                          (dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺╰─────────────╯                                                                 d997ad Test User      24-10-15 12:02
            """,
            ScreenText.Rows(gmd.WaitFor("Merge from"), repo.Path, 4, 3)
        );
    }

    // 'E' is the other direction: the current branch is merged into the hoovered one. Git can only
    // merge into the branch that is checked out, so the whole point of this test is what happens
    // around the merge — the target is checked out, the commit dialog opens there, and once it is
    // committed the branch that was current at the start is checked out again.
    [TestMethod]
    public async Task TestMergeToBranch()
    {
        // Current is main, which has 'Add delta' that dev does not, so main into dev is a real
        // merge. It is TestMergeBranch's merge in the other direction and driven the other way:
        // there the target was checked out first, here gmd does that checkout itself.
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");

        // Show dev and hoover it: down to the merge commit, left to hoover main, enter to open the
        // branch merged in there, right to move the hoover from main on to dev
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        gmd.Send("Right");
        gmd.WaitForStable();

        gmd.Send("E");

        // The uncommitted row says dev is the current branch now, i.e. gmd switched to the target
        // on the way, and the dialog offers the message git would have used
        var screen = gmd.WaitFor("Commit 2 changes");
        Assert.AreEqual(
            """
             ╭╊  ©Merge branch 'main' into dev, 2 uncommitted changes                  (● dev)                       NN-NN-NN NN:NN
            """,
            ScreenText.MaskTimes(ScreenText.Rows(screen, repo.Path, 2, 1), "uncommitted")
        );
        StringAssert.Contains(screen, "[Merge branch 'main' into dev");

        gmd.Send("Enter");

        // Back on main, which is where it started, with the merge commit on dev
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭╊   Merge branch 'main' into dev                                           (dev) 60e4d8 Test User      24-10-15 12:07
            ┣╯┃ ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮┃   Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│┃   Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊   More dev work                                                                af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted changes"),
            repo.Path
        );

        // Switched back to where it started, and dev's tip is a real merge commit, i.e. two
        // parents: dev's old tip and main's tip
        Assert.AreEqual("main", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("af3ee69 17d85ba", await repo.GitAsync("log --format=%p -1 dev"));
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
    }

    // Nothing to merge is the outcome that has to switch back without a commit dialog, since there
    // is nothing to commit. The fixture has dev merged into main already, so it is the plain case.
    [TestMethod]
    public async Task TestMergeToBranchThatIsAlreadyUpToDate()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.GitAsync("checkout -q dev");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Left"); // Hoovers main, the branch the cursor row is on
        gmd.WaitForStable();
        gmd.Send("E");

        Assert.AreEqual(
            """
                                                   ╭ Merge ─────────────────────────────────╮
                                                   │'main' is already up to date with 'dev'.│
                                                   │                                        │
                                                   │                [◦ OK ◦]                │
                                                   ╰────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("already up to date"), repo.Path, 17, 5)
        );

        // Left on the branch it started on, with nothing committed anywhere
        Assert.AreEqual("dev", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
        Assert.AreEqual("17d85ba", await repo.GitAsync("rev-parse --short=7 main"));
    }

    // The other arm of 'E', mirroring TestMergeFromMenu: with the current branch hoovered there is
    // nothing to merge it out of, so it offers the branches to merge it into instead
    [TestMethod]
    public async Task TestMergeToMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitFor("Merge branch");
        gmd.Send("Enter"); // Shows dev, and leaves the hoover on main, which is current
        gmd.WaitFor("More dev work");

        gmd.Send("E");

        // Only the shown branches are offered, so the menu lists dev and nothing else. The stray
        // 'k' is the tail of 'More dev work' behind the menu, which is drawn over the log view.
        Assert.AreEqual(
            """
            ┣│ ╭ Merge to ─╮                                                                   4a15fb Test User      24-10-15 12:04
            ┃╰╊│ o  dev    │ork                                                          (dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺╰───────────╯v                                                                  d997ad Test User      24-10-15 12:02
            """,
            ScreenText.Rows(gmd.WaitFor("Merge to"), repo.Path, 4, 3)
        );
    }
}
