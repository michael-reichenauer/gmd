using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// Linked worktrees: starting inside one, the worktrees dialog, creating and removing one, the
// branch menu of a branch checked out elsewhere, and the top bar marker that counts the other
// worktrees and turns yellow when one of them has changes.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class WorktreeTest
{
    // Started inside a linked worktree: 'dev' is current there, 'main' is held by the main folder
    // and marked so in the margin and in its tip, and the top bar counts the one other worktree
    [TestMethod]
    public async Task TestStartupInsideALinkedWorktree()
    {
        using var repo = await E2eRepo.CreateWithWorktreeAsync();
        var worktree = repo.WorktreePath("dev");
        using var gmd = TmuxSession.StartGmd(worktree);

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●dev, ⌂1                                                    (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ⌂ Add delta                                                     (⌂ main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│    Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊ ● More dev work                                                        (● dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("Initial"),
            worktree
        );
    }

    // The worktrees dialog, opened with 'w': one row per worktree, the one gmd is in marked
    [TestMethod]
    public async Task TestWorktreesDialog()
    {
        using var repo = await E2eRepo.CreateWithWorktreeAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("⌂1");

        gmd.Send("w");
        var screen = gmd.WaitFor("Worktrees");

        // The main worktree's row is checked by its start only: its path is the temp folder, which
        // is too long for the column and so is cut to an end that is different on every run
        var lines = ScreenText.Of(screen, repo.Path).Split('\n');
        var first = Array.FindIndex(lines, l => l.Contains("╭ Worktrees"));
        Assert.IsTrue(first > 0, "The dialog is on the screen");
        var rows = lines[first..(first + 9)];
        Assert.AreEqual(
            """
                      ╭ Worktrees ───────────────────────────────────────────────────────────────────────────────────────╮
                      │    Kind    Branch                Changes State    Merged    Path                                 │
                      │ ┌──────────────────────────────────────────────────────────────────────────────────────────────┐ │
            """,
            string.Join('\n', rows[..3])
        );
        StringAssert.StartsWith(rows[3], "          │ │● main    main                   -                         ┅");
        Assert.AreEqual(
            """
                      │ │  linked  dev                    -               merged    {repo}-dev│ │
                      │ └──────────────────────────────────────────────────────────────────────────────────────────────┘ │
                      │                                                                                                  │
                      │ [ Open ]  [ Add ... ]  [ Remove ... ]  [ Prune ]  [ Copy Path ]                       [ Close ]  │
                      ╰──────────────────────────────────────────────────────────────────────────────────────────────────╯
            """,
            string.Join('\n', rows[4..])
        );

        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Kind    Branch"), "Add delta");
    }

    // The branch menu of a branch checked out in another worktree: the switch item opens that
    // worktree instead, since git refuses to check the branch out here
    [TestMethod]
    public async Task TestBranchMenuOfABranchInAnotherWorktreeOffersToOpenIt()
    {
        using var repo = await E2eRepo.CreateWithWorktreeAsync();
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
        var menu = gmd.WaitFor("Branch: dev");

        StringAssert.Contains(menu, "Open Worktree ");
        Assert.IsFalse(menu.Contains("Switch to Branch"), "The switch item is replaced, not added to");

        // Taking it opens the worktree: 'dev' becomes current and 'main' is the one held elsewhere
        gmd.Send("Enter");
        var opened = gmd.WaitFor("●dev");
        StringAssert.Contains(opened, "(⌂ main)");
    }

    // The top bar marker for the other worktrees turns yellow when one of them gets uncommitted
    // changes — found by the periodic re-read, since nothing else touches this repo meanwhile
    [TestMethod]
    public async Task TestWorktreeMarkerTurnsYellowWhenTheWorktreeGetsChanges()
    {
        using var repo = await E2eRepo.CreateWithWorktreeAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("⌂1");
        Assert.AreEqual('W', ColorOfMarker(gmd), "Clean, so the marker is white");

        File.WriteAllText(Path.Join(repo.WorktreePath("dev"), "new.txt"), "new\n");

        // The re-read is every thirty seconds
        var deadline = DateTime.UtcNow.AddSeconds(75);
        while (ColorOfMarker(gmd) != 'Y' && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
        }
        Assert.AreEqual('Y', ColorOfMarker(gmd), "Yellow once the worktree has changes");
    }

    // A worktree that already has changes when gmd starts: the marker is yellow within moments.
    // The other worktrees are read right after the repo is shown, not with it (so a slow status
    // there never delays the repo) and not thirty seconds later by the first periodic re-read.
    [TestMethod]
    public async Task TestWorktreeMarkerIsYellowSoonAfterStartupWhenAWorktreeHasChanges()
    {
        using var repo = await E2eRepo.CreateWithWorktreeAsync();
        File.WriteAllText(Path.Join(repo.WorktreePath("dev"), "new.txt"), "new\n");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("⌂1");

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (ColorOfMarker(gmd) != 'Y' && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(200);
        }
        Assert.AreEqual('Y', ColorOfMarker(gmd), "Yellow soon after startup, not after the first periodic re-read");
    }

    // Creating a worktree for a branch from its menu: the dialog proposes a folder beside the repo
    // named after it and the branch, and opening the new worktree is the default, so gmd ends up
    // in it with 'dev' current. The proposed folder is exactly TempRepo.WorktreePath("dev"), so
    // the fixture can be told to delete it.
    [TestMethod]
    public async Task TestCreateWorktreeFromTheBranchMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        var worktree = repo.WorktreePath("dev");
        repo.TrackFolder(worktree);
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

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
        // Down to 'Create Worktree ...', past the disabled 'Rebase and Push onto' and 'Pull'
        for (var i = 0; i < 6; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        var dialog = gmd.WaitFor("Create Worktree");
        var lines = ScreenText.Of(dialog, repo.Path).Split('\n');
        var first = Array.FindIndex(lines, l => l.Contains("╭ Create Worktree"));
        Assert.IsTrue(first > 0, "The dialog is on the screen");
        Assert.AreEqual(
            """
                                ╭ Create Worktree ─────────────────────────────────────────────────────────────╮
                                │ Branch:│dev                                                              ▼│  │
                                │        └──────────────────────────────────────────────────────────────────┘  │
                                │         Existing branch                                                      │
                                │                                                                              │
                                │ Path:   {repo}-dev    [ Browse ] │
                                │                                                                              │
                                │ Put in: [ Beside repo ] [ .claude/worktrees ] [ .worktrees ]                 │
                                │                                                                              │
                                │                                                                              │
                                │ ◙ Open worktree after creating                                               │
                                │                                                                              │
                                │                                                                              │
                                │                                                                              │
            """,
            string.Join('\n', lines[first..(first + 14)])
        );

        gmd.Send("Enter");

        // Opened in the new worktree, where 'dev' is current and 'main' is the one held elsewhere
        var opened = gmd.WaitFor("●dev");
        StringAssert.Contains(opened, "(⌂ main)");
        StringAssert.Contains(opened, "⌂1");
        Assert.IsTrue(Directory.Exists(worktree));
        StringAssert.Contains(await repo.GitAsync("worktree list"), $"{worktree}");
        Assert.AreEqual("dev", (await repo.GitAsync($"-C \"{worktree}\" rev-parse --abbrev-ref HEAD")).Trim());
        Assert.IsFalse(File.Exists(Path.Join(repo.Path, ".gitignore")), "Beside the repo, nothing to ignore");
    }

    // The branch drop-down of the create dialog: Down opens the list on the branch the field names,
    // the arrow keys move in it, Enter picks. Picking 'main', which is checked out here, is what
    // the hint says, and the path follows the pick.
    [TestMethod]
    public async Task TestCreateWorktreeDialogBranchDropDown()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

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
        for (var i = 0; i < 6; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        gmd.WaitFor("Existing branch");

        // Open the list (on 'dev'), move to 'main', pick it
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        var picked = gmd.WaitFor("Already checked out at");
        StringAssert.Contains(picked, "Branch:│main");
        StringAssert.Contains(picked, "{repo}-main".Replace("{repo}", repo.Path));
        Assert.IsFalse(picked.Contains("├"), "The list is closed again");

        // Escape closes the dialog rather than the log view, since the field has the focus back
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Create Worktree"), "Add delta");
    }

    // Removing a worktree from the dialog, with its branch: 'r' is the Remove action's key while
    // the list has the focus, and the branch box is offered checked since 'dev' is merged into main
    [TestMethod]
    public async Task TestRemoveWorktreeAndItsBranchFromTheDialog()
    {
        using var repo = await E2eRepo.CreateWithWorktreeAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("⌂1");

        gmd.Send("w");
        gmd.WaitFor("Kind    Branch");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("r");

        var dialog = gmd.WaitFor("Remove Worktree");
        StringAssert.Contains(dialog, "Delete branch 'dev' too");
        gmd.Send("Enter");

        // Back in the list, which now has the main worktree only; then closed
        gmd.WaitUntilGone("Remove Worktree");
        var list = gmd.WaitFor("Kind    Branch");
        Assert.IsFalse(list.Contains("linked"), "The linked worktree is gone from the list");
        gmd.Send("Escape");
        gmd.WaitUntilGone("Kind    Branch");

        Assert.IsFalse(Directory.Exists(repo.WorktreePath("dev")));
        Assert.AreEqual("", await repo.GitAsync("branch --list dev"), "The branch is deleted with it");
        Assert.AreEqual(1, (await repo.GitAsync("worktree list")).Split('\n').Length);
    }

    // The color of the '⌂' in the top bar, one letter per cell as ScreenText.ColorRows gives them
    static char ColorOfMarker(TmuxSession gmd)
    {
        var bar = gmd.Capture().Split('\n')[0];
        var column = bar.IndexOf('⌂');
        Assert.IsTrue(column >= 0, "The marker is in the top bar");
        var colors = ScreenText.ColorRows(gmd.CaptureColors(), 0, 1);
        return colors[column];
    }
}
