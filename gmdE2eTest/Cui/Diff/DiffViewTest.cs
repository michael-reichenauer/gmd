using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Diff;

// The diff view: the diff of a commit, the context around its changes that '+' and '-' step per
// file, and closing it.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class DiffViewTest
{
    [TestMethod]
    public async Task TestDiffOfACommit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("d");

        // The diff view is a Toplevel filling the screen, so the application bar is gone
        ScreenText.AssertEqual(
            """
            ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
            Commit:  17d85ba889a1084f912c412d0ce435c9d7a36f53
            Author:  Test User <test@example.com>
            Date:    2024-10-15 12:06:00
            Message: Add delta

            1 File:  1 added   +1 -0 lines
              Added:       delta.txt

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Added: delta.txt

            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
               1┃delta
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            """,
            gmd.WaitFor("Added: delta.txt"),
            repo.Path
        );

        // Escape leaves the diff and the log view is back
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Added: delta.txt"), "Merge branch 'dev' into main");
    }

    // '+' shows more of the file around its changes and '-' shows less, stepping 6 → 15 → the whole
    // file. It is per file: the file the cursor is on is the one that changes. The window asserted
    // is the file header and the first lines under it, which is where both halves show — the header
    // names the context it is at, and the first line says how far up the file it now starts.
    //
    // The cursor has to be inside a file for the keys to mean anything, hence the Down presses;
    // each one is its own Send, since a key sent into a screen that has not settled is dropped.
    // The '┃' down the right hand side is the scroll bar, which appears once the diff is taller
    // than the view.
    [TestMethod]
    public async Task TestDiffContextIsSteppedPerFile()
    {
        using var repo = await E2eRepo.CreateWithLongFileAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Change both files");

        gmd.Send("d");
        var atDefault = ScreenText.Of(gmd.WaitFor("Modified: long.txt"), repo.Path);
        MoveIntoTheLongFile();

        // Six lines either side of the change, so the file is drawn from line 14
        StringAssert.Contains(atDefault, "  14 line 14");
        StringAssert.Contains(atDefault, "Modified: long.txt");
        Assert.IsFalse(atDefault.Contains("long.txt  ("), "No context is named while it is the default");

        gmd.Send("+");
        ScreenText.AssertEqual(
            """
            Modified: long.txt  (context 15)                                                                                       ┃
                                                                                                                                   ┃
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┃
               5 line 5                                                │   5 line 5                                                ┃
               6 line 6                                                │   6 line 6                                                ┃
            """,
            ScreenText.Rows(gmd.WaitFor("(context 15)"), repo.Path, 11, 5),
            repo.Path
        );

        gmd.Send("+");
        ScreenText.AssertEqual(
            """
            Modified: long.txt  (whole file)                                                                                       ┃
                                                                                                                                   ┃
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┃
               1 line 1                                                │   1 line 1                                                ┃
               2 line 2                                                │   2 line 2                                                ┃
            """,
            ScreenText.Rows(gmd.WaitFor("(whole file)"), repo.Path, 11, 5),
            repo.Path
        );

        // The other file of the same commit was left at the default, which is the point of the
        // whole thing: only the file the cursor was on was re-fetched and redrawn
        gmd.Send("End");
        var bottom = ScreenText.Of(gmd.WaitForStable(), repo.Path);
        StringAssert.Contains(bottom, "Modified: short.txt");
        StringAssert.Contains(bottom, "   2┃two", "Its two lines, exactly as it was drawn to begin with");
        Assert.IsFalse(bottom.Contains("short.txt  ("), "The other file was left at the default");

        // '-' acts on the file the cursor is on too, and short.txt is already at the narrowest
        gmd.Send("-");
        var unchanged = ScreenText.Of(gmd.WaitForStable(), repo.Path);
        Assert.IsFalse(unchanged.Contains("short.txt  ("), "Nothing to narrow");
        StringAssert.Contains(unchanged, "  40 line 40", "And the long file is still drawn to its end");

        // Back onto the long file and all the way down again, which is where it started
        gmd.Send("Home");
        gmd.WaitForStable();
        MoveIntoTheLongFile();
        gmd.Send("-");
        gmd.WaitFor("(context 15)");
        gmd.Send("-");
        ScreenText.AssertEqual(atDefault, gmd.WaitUntilGone("(context 15)"), repo.Path);

        void MoveIntoTheLongFile()
        {
            for (int i = 0; i < 18; i++)
            {
                gmd.Send("Down");
                gmd.WaitForStable();
            }
        }
    }

    // f, or /, asks for a text and puts the cursor on the first line with it, in any case, from the
    // cursor down, with the text marked black on yellow on the cursor's row too; n and Shift-N go on
    // from there and say where they stop, as the log's do. What it found is said over the bottom row,
    // the key hints being off.
    [TestMethod]
    public async Task TestSearchTheDiff()
    {
        using var repo = await E2eRepo.CreateWithLongFileAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Change both files");
        gmd.Send("d");
        gmd.WaitFor("Modified: long.txt");

        gmd.Send("/");
        gmd.WaitFor("Text to find in the diff");
        gmd.SendText("LINE 2");
        gmd.Send("Enter");

        var found = gmd.WaitFor("Match 1 of");
        Assert.AreEqual("Match 1 of 7 for 'LINE 2'", ScreenText.LastLine(found));
        var row = ScreenText.Of(found, repo.Path).Split('\n').FindIndexBy(l => l.Contains("line 20 changed"));
        // The cursor's row: highlighted on the left side, 'D', where the match is, and 'line 2' marked,
        // 'y', on both sides, beside the change, 'g'
        Assert.AreEqual(
            "  DDDyyyy yD                                               .  ...yyyy y. ggggggg",
            ScreenText.BackgroundRows(gmd.CaptureColors(), row, 1)
        );

        gmd.Send("n");
        Assert.AreEqual("Match 2 of 7 for 'LINE 2'", ScreenText.LastLine(gmd.WaitFor("Match 2 of")));
        gmd.Send("N");
        gmd.WaitFor("Match 1 of");
        gmd.Send("N");
        Assert.AreEqual(
            "No more matches for 'LINE 2' above: n goes down",
            ScreenText.LastLine(gmd.WaitFor("No more matches"))
        );

        gmd.Send("f");
        gmd.WaitFor("Text to find in the diff");
        gmd.SendText("absent");
        gmd.Send("Enter");
        Assert.AreEqual("'absent' is not in the diff", ScreenText.LastLine(gmd.WaitFor("is not in the diff")));
    }

    // The same thing from the diff menu, which is how the keys are found in the first place. The
    // items name the file they would act on and what it would then show, and the direction that
    // has nowhere to go is disabled — at the default context there is no less context to ask for.
    [TestMethod]
    public async Task TestDiffContextMenuItems()
    {
        using var repo = await E2eRepo.CreateWithLongFileAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Change both files");

        gmd.Send("d");
        gmd.WaitFor("Modified: long.txt");
        for (int i = 0; i < 18; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }

        gmd.Send("m");
        ScreenText.AssertEqual(
            """
            ══════════════════════════════════════╭ Diff Menu ────────────────────────────────╮════════════════════════════════════
            Commit:  c00a3cc9fb5f429e9136ddb81fe75│Scroll to                               s >│
            Author:  Test User <test@example.com> │Search ...                              f  │
            Date:    2024-10-15 12:02:00          │Next Match                              n  │
            Message: Change both files            │Previous Match                         ⇧n  │
                                                  │Diff File                                 >│
            2 Files:  2 modified   +2 -2 lines    │Resolve Conflicts                   Enter >│
              Modified:    long.txt               │Run External Merge Tool                   >│
              Modified:    short.txt              │Discard Changes                         u >│
                                                  │Refresh                                 r  │
            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━│Commit                                  c  │━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Modified: long.txt                    │More Context of long.txt (15 lines)     +  │
                                                  │Less Context                            -  │
            ──────────────────────────────────────│Hide Whitespace Changes                 w  │────────────────────────────────────
              14 line 14                          │Focus Left Column                       ←  │
              15 line 15                          │Focus Right Column                      →  │
              16 line 16                          │Close                                 Esc  │
              17 line 17                          ╰───────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Diff Menu"), repo.Path, 0, 18),
            repo.Path
        );

        // 'Less Context' is dark, i.e. disabled, while 'More Context' is white — the file is at the
        // default, so there is nothing narrower to ask for
        var colors = ScreenText.ColorRows(gmd.CaptureColors(), 11, 2).Split('\n');
        StringAssert.Contains(colors[0], "mWWWW WWWWWWW", "'More Context' and its shortcut are enabled");
        StringAssert.Contains(colors[1], "mDDDD DDDDDDD", "'Less Context' is dark, i.e. disabled");

        // Three moves down from 'Scroll to', past 'Search ...' and 'Refresh', since the disabled
        // items in between are skipped
        for (int i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        StringAssert.Contains(
            ScreenText.Of(gmd.WaitFor("(context 15)"), repo.Path),
            "Modified: long.txt  (context 15)",
            "The menu item does what the '+' key does"
        );
    }

    // Both cases close the diff, and neither quits the application, which is the difference
    // between closing a view and closing gmd
    [TestMethod]
    [DataRow("q")]
    [DataRow("Q")]
    public async Task TestDiffViewClosesWithQ(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("d");
        gmd.WaitFor("Added: delta.txt");

        gmd.Send(key);

        StringAssert.Contains(gmd.WaitUntilGone("Added: delta.txt"), "Merge branch 'dev' into main");
        Assert.IsTrue(gmd.IsRunning, $"'{key}' should close the diff view, not quit gmd");
    }

    // A key the diff view has no use for does nothing there. It used to fall through to the log view
    // below, since the diff was a toplevel that was not modal, and in the log view 'P' pushes every
    // branch and 'p' the current one — from a screen that shows neither, and says nothing of it.
    [TestMethod]
    public async Task TestLogViewKeysDoNothingInTheDiff()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        var remoteMain = await repo.GitAsync("ls-remote origin main");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("▲1");
        gmd.Send("d");
        gmd.WaitFor("Added: zeta.txt");

        foreach (var key in new[] { "P", "p", "U", "u" })
        {
            gmd.Send(key);
            gmd.WaitForStable();
        }

        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Added: zeta.txt"), "▲1", "Nothing was pushed");
        Assert.AreEqual(remoteMain, await repo.GitAsync("ls-remote origin main"), "origin is untouched");
    }

    // w hides the changes of whitespace alone and says so, and w shows them again. A file whose changes
    // are all whitespace stays listed, and can still be discarded, rather than vanish from the diff of
    // the changes about to be committed; and a re-indented line is unchanged, beside the added one.
    [TestMethod]
    public async Task TestHideWhitespace()
    {
        using var repo = await E2eRepo.CreateAsync();
        repo.WriteFile("alpha.txt", "  alpha\nmore\n");
        repo.WriteFile("beta.txt", "beta gamma delta  \n");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("©2 uncommitted changes");
        gmd.Send("d");
        gmd.WaitFor("Modified: beta.txt");

        gmd.Send("w");
        var hidden = gmd.WaitFor("whitespace hidden");
        // From the message down, the date of the uncommitted changes being the time now
        Assert.AreEqual(
            """
            Message: Uncommitted changes

            2 Files:  2 modified   +1 -0 lines   whitespace hidden
              Modified:    alpha.txt
              Modified:    beta.txt (Whitespace only)

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Modified: alpha.txt

            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
               1   alpha                                               │   1   alpha
            ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│   2┃more
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Modified: beta.txt  (Whitespace only)

            Only whitespace changed, which is hidden
            """,
            ScreenText.Rows(hidden, repo.Path, 2, 18)
        );
        Assert.AreEqual("Whitespace changes hidden: w shows them", ScreenText.LastLine(hidden));

        gmd.Send("u");
        StringAssert.Contains(gmd.WaitFor("All Changes"), "beta.txt", "Still offered to discard");
        gmd.Send("Escape");
        gmd.WaitUntilGone("All Changes");

        gmd.Send("w");
        StringAssert.Contains(gmd.WaitUntilGone("whitespace hidden"), "Modified: beta.txt");
    }

    // A file with two changes, two made TWO, and eleven made ELEVEN with a line added after it, not
    // committed, and the diff of it open with the cursor on the second change, which search put it on
    static async Task<(TempRepo Repo, TmuxSession Gmd)> OnTheSecondChangeAsync(string search)
    {
        var repo = await E2eRepo.CreateAsync();
        var lines = Enumerable.Range(1, 12).Select(i => $"line {i}").ToList();
        await repo.CommitFileAtAsync("lines.txt", Text(lines), "Add lines", TempRepo.BaseTime.AddMinutes(7));
        lines[1] = "TWO";
        lines[10] = "ELEVEN";
        lines.Insert(11, "extra");
        repo.WriteFile("lines.txt", Text(lines));

        var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");
        gmd.Send("d");
        gmd.WaitFor("Modified: lines.txt");
        gmd.Send("/");
        gmd.WaitFor("Text to find in the diff");
        gmd.SendText(search);
        gmd.Send("Enter");
        gmd.WaitFor("Match 1 of 1");
        return (repo, gmd);
    }

    static string Text(IEnumerable<string> lines) => string.Concat(lines.Select(l => l + "\n"));

    // u on a change offers it first, above the files, and asks, No being the default; then that change
    // alone is taken back, its added line taken out too, and the file's other change stays
    [TestMethod]
    public async Task TestDiscardTheChangeTheCursorIsOn()
    {
        var (repo, gmd) = await OnTheSecondChangeAsync("ELEVEN");
        using var _ = repo;
        using var __ = gmd;

        gmd.Send("u");
        gmd.WaitFor("This Change in lines.txt");
        gmd.Send("Enter");
        var question = gmd.WaitFor("Discard this change in lines.txt?");
        StringAssert.Contains(question, "-line 11");
        StringAssert.Contains(question, "+ELEVEN");
        StringAssert.Contains(question, "+extra");
        gmd.Send("Left"); // From No, the default, to Yes
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual("Discarded 3 lines of lines.txt", ScreenText.LastLine(gmd.WaitFor("Discarded")));
        Assert.AreEqual(
            "line 1\nTWO\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9\nline 10\nline 11\nline 12\n",
            File.ReadAllText(Path.Join(repo.Path, "lines.txt"))
        );
        StringAssert.Contains(gmd.WaitUntilGone("ELEVEN"), "TWO", "The diff read again, with the other change");
    }

    // Rows selected are the lines discarded, here the added line alone of a change of three, which
    // leaves the rest of the change as it was
    [TestMethod]
    public async Task TestDiscardTheSelectedLines()
    {
        var (repo, gmd) = await OnTheSecondChangeAsync("extra");
        using var _ = repo;
        using var __ = gmd;

        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("u");
        gmd.WaitFor("Selected Lines in lines.txt");
        gmd.Send("Enter");
        gmd.WaitFor("Discard the selected lines in lines.txt?");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");

        Assert.AreEqual("Discarded 1 line of lines.txt", ScreenText.LastLine(gmd.WaitFor("Discarded")));
        StringAssert.EndsWith(File.ReadAllText(Path.Join(repo.Path, "lines.txt")), "line 10\nELEVEN\nline 12\n");
    }

    // A greyed out item of the diff's menu says why on the diff's own status line, where it is seen;
    // the log's, which it was said on, is behind the diff
    [TestMethod]
    public async Task TestAGreyedOutItemSaysWhyInTheDiff()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add delta");
        gmd.Send("d");
        gmd.WaitFor("Added: delta.txt");
        gmd.Send("m");
        gmd.WaitFor("Diff Menu");

        gmd.Send("n"); // Next Match, greyed out with no search

        Assert.AreEqual(
            "No search to go on with: search with f first",
            ScreenText.LastLine(gmd.WaitFor("No search to go on with"))
        );
    }

    // Undoing uncommitted changes from the diff asks first, and No is the default: the item is
    // chosen from a menu, where the slip is an Enter on the wrong line, and a new file is deleted
    // with no way to get it back. The question says which of the two it is.
    [TestMethod]
    [DataRow("Down", "Delete the new file?")]
    [DataRow("End", "Discard all uncommitted changes?")]
    public async Task TestUndoFromTheDiffAsksFirst(string move, string question)
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("©2 uncommitted changes");
        gmd.Send("d");
        gmd.WaitFor("Added: epsilon.txt");
        gmd.Send("u");
        gmd.WaitFor("All Changes");
        gmd.Send(move);
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor(question);

        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone(question), "Added: epsilon.txt", "Still in the diff");
        Assert.AreEqual(" M alpha.txt\n?? epsilon.txt", await repo.GitAsync("status -s"), "Nothing undone");
    }

    // Both cases open the menu: the menus write their shortcuts in upper case, so that is what gets
    // pressed
    [TestMethod]
    [DataRow("m")]
    [DataRow("M")]
    public async Task TestDiffMenuOpensWithM(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("d");
        gmd.WaitFor("Added: delta.txt");

        gmd.Send(key);

        gmd.WaitFor("Diff Menu");
    }

    // The help opens from the diff as from the log view, at the part about the diff, and closing the
    // box with its link is back in the diff. The session has no browser, so the link is copied (see
    // LogViewTest.TestHelpCopiesItsLinkWithNoBrowser).
    [TestMethod]
    [DataRow("?")]
    [DataRow("F1")]
    public async Task TestHelpOpensFromTheDiff(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("d");
        gmd.WaitFor("Added: delta.txt");

        gmd.Send(key);
        gmd.WaitFor("copied to the clipboard");
        StringAssert.EndsWith(gmd.Clipboard(), "/gmd/doc/help.md#diff-and-blame");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("copied to the clipboard"), "Added: delta.txt", "Back in the diff");
    }

    // Any two commits, wherever they are: Mark for Diff on one, and Diff with it in the menu of the
    // other, which diffs the older to the newer. In the menu of 'Add beta', which opens on 'Amend ...'
    // of the last commit, Mark for Diff is two moves down, past its own Amend and Squash, which are
    // disabled, and Commit Diff; in the menu of 'Add delta', Diff with is one further.
    [TestMethod]
    public async Task TestADiffOfAnyTwoCommits()
    {
        using var repo = await E2eRepo.CreateAsync();
        var beta = (await repo.GitAsync("rev-parse \":/Add beta\"")).Trim()[..8];
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        for (int i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        PickCommitMenuItem(gmd, 2, "Mark for Diff");
        Assert.AreEqual(
            $"Marked {beta} for a diff: Diff with {beta} in the menu of another commit diffs the two",
            ScreenText.LastLine(gmd.WaitFor($"Marked {beta}"))
        );

        gmd.Send("Home");
        gmd.WaitForStable();
        PickCommitMenuItem(gmd, 3, $"Diff with {beta}");

        var diff = gmd.WaitFor($"Diff {beta} to 17d85ba8");
        StringAssert.Contains(diff, "Added: delta.txt");
        StringAssert.Contains(diff, "Added: gamma.txt", "And what the merge brought in between");
    }

    static void PickCommitMenuItem(TmuxSession gmd, int moves, string item)
    {
        gmd.Send("m");
        gmd.WaitFor(item);
        for (int i = 0; i < moves; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
    }

    // A range diff is the changes the selected commits made, which it only is for commits of one
    // branch: the check for that compared a commit with itself and never refused anything. It says
    // so on the status line, and diffs a range on one branch. Ctrl-D diffs the rows whatever branch
    // is highlighted, which showing dev leaves main.
    [TestMethod]
    public async Task TestARangeDiffNeedsCommitsOfOneBranch()
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

        // 'Add gamma' on main and 'More dev work' on dev, the third and fourth rows. The first
        // Shift-Down selects the row the cursor is on, the second adds the one below.
        SelectTwoRowsFrom(gmd, 2);
        gmd.Send("C-d");
        Assert.AreEqual(
            "The selected commits are on different branches: Mark for Diff in the commit menu diffs any two",
            ScreenText.LastLine(gmd.WaitFor("different branches"))
        );

        // 'More dev work' and 'Work on dev', both on dev
        SelectTwoRowsFrom(gmd, 3);
        gmd.Send("C-d");
        gmd.WaitFor("dev.txt");
    }

    static void SelectTwoRowsFrom(TmuxSession gmd, int row)
    {
        string[] keys = ["Home", .. Enumerable.Repeat("Down", row), "S-Down", "S-Down"];
        foreach (var key in keys)
        {
            gmd.Send(key);
            gmd.WaitForStable();
        }
    }
}
