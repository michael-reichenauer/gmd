using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// Copying commits from the log view: Ctrl+C asks what to copy, 'i' and '⇧i' copy the ids and the
// messages straight away.
//
// The one thing gmd does that leaves the application entirely, and the only tier that can
// check it: what a clipboard holds afterwards is not on any screen and cannot be faked. The
// session has no display and no clipboard tool it can reach (see TmuxSession), so the path
// taken here is the last one in ClipboardService's chain — OSC 52, i.e. asking the terminal
// itself — and tmux keeps what it is sent as a buffer, which is what is read back.
//
// That is also the path that matters most for gmd, since it is the only one that works over
// ssh or from inside a container, where no local tool can reach the user's own clipboard.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class CopyTest
{
    // Shift+Down selects rows, and Ctrl+C asks what to copy of the commits selected: here their ids
    // and subjects, one to a line, oldest first, which is not the order the log shows them in
    [TestMethod]
    public async Task TestCopySelectedCommits()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("C-c");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  | Add delta                                                    (● main)[v1.0] 17d85b Test User      2024-10-15 12:06
            ┣╮ | Merge branch 'dev' into main                                                4e73d2 Test User      2024-10-15 12:05
            ┣    Add╭ Copy: 2 Commits ───────╮                                               4a15fb Test User      2024-10-15 12:04
            ┣╯   Add│Commit Ids             i│                                               dd7891 Test User      2024-10-15 12:01
            ┗    Ini│Commit Messages       ⇧i│                                               9dc406 Test User      2024-10-15 12:00
                    │Ids and Subjects        │
                    │Range 4e73d2^..17d85b   │
                    ╰────────────────────────╯
            """,
            gmd.WaitFor("Copy: 2 Commits"),
            repo.Path
        );

        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual(
            """
            4e73d23 Merge branch 'dev' into main
            17d85ba Add delta
            """,
            gmd.WaitForClipboard()
        );
        Assert.AreEqual("Copied the ids and subjects of 2 commits", ScreenText.LastLine(gmd.WaitFor("Copied the ids")));
    }

    // A drag selects from the row pressed to the row released: it started a row on, at the first
    // move, so a drag over two rows selected one. The 'i' the menu shows picks its item, the full
    // ids, on one line as a git command takes them.
    [TestMethod]
    public async Task TestCopyTheRowsDraggedOver()
    {
        using var repo = await E2eRepo.CreateAsync();
        var ids =
            $"{(await repo.GitAsync("rev-parse main~1")).Trim()} {(await repo.GitAsync("rev-parse main")).Trim()}";
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Drag(20, 2, 20, 3);
        gmd.WaitForStable();
        gmd.Send("C-c");
        gmd.WaitFor("Copy: 2 Commits");
        gmd.Send("i");

        Assert.AreEqual(ids, gmd.WaitForClipboard());
    }

    // One row selected is its commit, so Ctrl+C asks what to copy of it, as with nothing selected,
    // and nothing of the graph comes with it, which it did when the row was copied as drawn
    [TestMethod]
    public async Task TestCopyOneSelectedRow()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("C-c");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  | Add delta                                                    (● main)[v1.0] 17d85b Test User      2024-10-15 12:06
            ┣╮   Mer╭ Copy: 17d85b ───╮o main                                                4e73d2 Test User      2024-10-15 12:05
            ┣    Add│Commit Id       i│                                                      4a15fb Test User      2024-10-15 12:04
            ┣╯   Add│Commit Message ⇧i│                                                      dd7891 Test User      2024-10-15 12:01
            ┗    Ini│Subject          │                                                      9dc406 Test User      2024-10-15 12:00
                    │Id and Subject   │
                    │Short Id         │
                    │Link to Commit   │
                    ╰─────────────────╯
            """,
            gmd.WaitFor("Copy: 17d85b"),
            repo.Path
        );

        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual("Add delta", gmd.WaitForClipboard());
        Assert.AreEqual("Copied the subject of 17d85b", ScreenText.LastLine(gmd.WaitFor("Copied the subject")));
    }

    // With nothing selected, Ctrl+C is about the commit on the cursor's row, and the '⇧i' the menu
    // shows picks its message
    [TestMethod]
    public async Task TestCopyTheRowsCommit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("C-c");
        gmd.WaitFor("Copy: 4e73d2");
        gmd.Send("I");

        Assert.AreEqual("Merge branch 'dev' into main", gmd.WaitForClipboard());
        Assert.AreEqual("Copied the message of 4e73d2", ScreenText.LastLine(gmd.WaitFor("Copied the message")));
    }

    // The range git takes for the selected commits, the oldest included, with full ids
    [TestMethod]
    public async Task TestCopyTheRangeOfTheSelectedCommits()
    {
        using var repo = await E2eRepo.CreateAsync();
        var range =
            $"{(await repo.GitAsync("rev-parse main~1")).Trim()}^..{(await repo.GitAsync("rev-parse main")).Trim()}";
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("C-c");
        gmd.WaitFor("Copy: 2 Commits");
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual(range, gmd.WaitForClipboard());
        Assert.AreEqual("Copied the range 4e73d2^..17d85b", ScreenText.LastLine(gmd.WaitFor("Copied the range")));
    }

    // 'i' and '⇧i' copy the commits selected too, as their items in the menu do: the messages with a
    // blank line between them
    [TestMethod]
    public async Task TestShiftICopiesTheMessagesOfTheSelectedCommits()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("I");

        Assert.AreEqual(
            """
            Merge branch 'dev' into main

            Add delta
            """,
            gmd.WaitForClipboard()
        );
        Assert.AreEqual("Copied the messages of 2 commits", ScreenText.LastLine(gmd.WaitFor("Copied the messages")));
    }

    // 'i' copies the id of the commit on the cursor's row, and Shift-I its message, the commonest
    // things copied from a log, and each says what it copied. The commit menu has both.
    [TestMethod]
    public async Task TestCopyTheCommitIdAndMessage()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("i");
        Assert.AreEqual(await repo.GitAsync("rev-parse main"), gmd.WaitForClipboard());
        Assert.AreEqual("Copied the id of 17d85b", ScreenText.LastLine(gmd.WaitFor("Copied the id")));

        // The status line is written after the copy, so once it shows, the clipboard has it
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("I");
        Assert.AreEqual("Copied the message of 4e73d2", ScreenText.LastLine(gmd.WaitFor("Copied the message")));
        Assert.AreEqual("Merge branch 'dev' into main", gmd.Clipboard());
    }

    // The uncommitted changes are no commit, so there is no id or message to copy, which is said
    [TestMethod]
    public async Task TestTheUncommittedChangesHaveNoIdToCopy()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");

        gmd.Send("i");

        Assert.AreEqual(
            "The uncommitted changes are no commit yet: move to one first",
            ScreenText.LastLine(gmd.WaitFor("no commit yet"))
        );
        Assert.AreEqual("", gmd.Clipboard(), "Nothing was copied");
    }

    // Ctrl+C there says so too, rather than opening a menu with nothing in it to copy
    [TestMethod]
    public async Task TestTheUncommittedChangesHaveNothingToCopy()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");

        gmd.Send("C-c");

        var screen = gmd.WaitFor("no commit yet");
        Assert.AreEqual("The uncommitted changes are no commit yet: move to one first", ScreenText.LastLine(screen));
        Assert.IsFalse(screen.Contains("Copy:"), "No menu opened");
        Assert.AreEqual("", gmd.Clipboard(), "Nothing was copied");
    }
}
