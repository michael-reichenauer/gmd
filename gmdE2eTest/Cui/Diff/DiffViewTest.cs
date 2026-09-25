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

            1 Files:
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
            Commit:  c00a3cc9fb5f429e9136ddb81fe75│Scroll to                               S >│
            Author:  Test User <test@example.com> │Diff File                                 >│
            Date:    2024-10-15 12:02:00          │Resolve Conflicts                   Enter >│
            Message: Change both files            │Run External Merge Tool                   >│
                                                  │Discard Changes                         U >│
            2 Files:                              │Refresh                                 R  │
              Modified:    long.txt               │Commit                                  C  │
              Modified:    short.txt              │More Context of long.txt (15 lines)     +  │
                                                  │Less Context                            -  │
            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━│Focus Left Column                       ←  │━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Modified: long.txt                    │Focus Right Column                      →  │
                                                  │Close                                 Esc  │
            ──────────────────────────────────────╰───────────────────────────────────────────╯────────────────────────────────────
            """,
            ScreenText.Rows(gmd.WaitFor("Diff Menu"), repo.Path, 0, 14),
            repo.Path
        );

        // 'Less Context' is dark, i.e. disabled, while 'More Context' is white — the file is at the
        // default, so there is nothing narrower to ask for
        var colors = ScreenText.ColorRows(gmd.CaptureColors(), 8, 2).Split('\n');
        StringAssert.Contains(colors[0], "mWWWW WWWWWWW", "'More Context' and its shortcut are enabled");
        StringAssert.Contains(colors[1], "mDDDD DDDDDDD", "'Less Context' is dark, i.e. disabled");

        // Two moves down from 'Scroll to', since the disabled items in between are skipped
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Down");
        gmd.WaitForStable();
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
}
