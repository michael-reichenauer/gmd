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

        // The diff view is a Toplevel filling the screen, so the application bar is gone, and its own
        // top row is the commit, with the file and the hunk once the view scrolls down into one
        ScreenText.AssertEqual(
            """
            17d85ba8  Add delta ───────────────────────────────────────────────────────────────────────────────────────────────────
            Test User <test@example.com>  2024-10-15 12:06:00

            1 file changed  1 added  +1
              A delta.txt  +1

            ▌ A delta.txt  +1 ─────────────────────────────────────────────────────────────────────────────────────────────────────
            ▌   1 delta
            """,
            gmd.WaitFor("A delta.txt"),
            repo.Path
        );

        // Escape leaves the diff and the log view is back
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("A delta.txt"), "Merge branch 'dev' into main");
    }

    // '+' shows more of the file around its changes and '-' shows less, stepping 6 → 15 → the whole
    // file. It is per file: the file the cursor is on is the one that changes. The window asserted
    // is the file header and the first lines under it, which is where both halves show — the header
    // names the context it is at, and the first line says how far up the file it now starts. The
    // hunk header between them is the line git found above the hunk, as it names a function.
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
        var atDefault = ScreenText.Of(gmd.WaitFor("M long.txt"), repo.Path);
        MoveIntoTheLongFile();

        // Six lines either side of the change, so the file is drawn from line 14
        StringAssert.Contains(atDefault, "  14 line 14");
        StringAssert.Contains(atDefault, "M long.txt");
        Assert.IsFalse(atDefault.Contains("(context"), "No context is named while it is the default");

        gmd.Send("+");
        ScreenText.AssertEqual(
            """
            ▌ M long.txt  +1 -1  (context 15) ─────────────────────────────────────────────────────────────────────────────────────┃
               @@ line 4                                                                                                           ┃
                5 line 5                                               │    5 line 5                                               ┃
                6 line 6                                               │    6 line 6                                               ┃
                7 line 7                                               │    7 line 7                                               ┃
            """,
            ScreenText.Rows(gmd.WaitFor("(context 15)"), repo.Path, 7, 5),
            repo.Path
        );

        gmd.Send("+");
        ScreenText.AssertEqual(
            """
            ▌ M long.txt  +1 -1  (whole file) ─────────────────────────────────────────────────────────────────────────────────────┃
               @@ line 1                                                                                                           ┃
                1 line 1                                               │    1 line 1                                               ┃
                2 line 2                                               │    2 line 2                                               ┃
                3 line 3                                               │    3 line 3                                               ┃
            """,
            ScreenText.Rows(gmd.WaitFor("(whole file)"), repo.Path, 7, 5),
            repo.Path
        );

        // The other file of the same commit was left at the default, which is the point of the
        // whole thing: only the file the cursor was on was re-fetched and redrawn
        gmd.Send("End");
        var bottom = ScreenText.Of(gmd.WaitForStable(), repo.Path);
        StringAssert.Contains(bottom, "▌ M short.txt  +1 -1 ─", "The other file was left at the default");
        StringAssert.Contains(bottom, "▌   2 two", "Its two lines, exactly as it was drawn to begin with");

        // '-' acts on the file the cursor is on too, and short.txt is already at the narrowest
        gmd.Send("-");
        var unchanged = ScreenText.Of(gmd.WaitForStable(), repo.Path);
        Assert.IsFalse(unchanged.Contains("short.txt  +1 -1  ("), "Nothing to narrow");
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

    // ] and [ go to the next and the previous change, } and { to the next and the previous file, each
    // saying which of how many it went to, or that there is none that way. The top row is the commit,
    // and once a file's header has scrolled out of sight, the file and the hunk the top of the view is
    // in, as a breadcrumb.
    [TestMethod]
    public async Task TestGoToTheNextChangeAndFile()
    {
        using var repo = await E2eRepo.CreateWithLongFileAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Change both files");
        gmd.Send("d");
        gmd.WaitFor("M long.txt");
        StringAssert.StartsWith(ScreenText.Of(gmd.Capture(), repo.Path), "c00a3cc9  Change both files ───");

        gmd.Send("}");
        Assert.AreEqual("File 1 of 2: long.txt", ScreenText.LastLine(gmd.WaitFor("File 1 of 2")));

        // The whole of long.txt, so that its header can scroll out of sight
        gmd.Send("+");
        gmd.WaitFor("(context 15)");
        gmd.Send("+");
        gmd.WaitFor("(whole file)");
        gmd.Send("]");
        Assert.AreEqual("Change 1 of 2", ScreenText.LastLine(gmd.WaitFor("Change 1 of 2")));
        gmd.Send("PageDown");
        StringAssert.StartsWith(
            ScreenText.Of(gmd.WaitFor("› line 1"), repo.Path),
            "c00a3cc9  Change both files › M long.txt  +1 -1  (whole file) › line 1 ───"
        );

        // The page down took the cursor to the last row, below the last file's header and change
        gmd.Send("}");
        Assert.AreEqual("No more files below: { goes up", ScreenText.LastLine(gmd.WaitFor("No more files")));
        gmd.Send("{");
        Assert.AreEqual("File 2 of 2: short.txt", ScreenText.LastLine(gmd.WaitFor("File 2 of 2")));
        gmd.Send("]");
        Assert.AreEqual("Change 2 of 2", ScreenText.LastLine(gmd.WaitFor("Change 2 of 2")));
        gmd.Send("]");
        Assert.AreEqual("No more changes below: [ goes up", ScreenText.LastLine(gmd.WaitFor("No more changes")));
        gmd.Send("[");
        Assert.AreEqual("Change 1 of 2", ScreenText.LastLine(gmd.WaitFor("Change 1 of 2")));
        gmd.Send("{");
        Assert.AreEqual("File 1 of 2: long.txt", ScreenText.LastLine(gmd.WaitFor("File 1 of 2")));
    }

    // A view narrower than 110 columns shows the diff in one column, a change's removed lines above its
    // added ones, and a wider one side by side, following the terminal as it is resized, until v
    // chooses one for the diff
    [TestMethod]
    public async Task TestANarrowDiffIsInOneColumn()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 100, height: 30);
        gmd.WaitFor("uncommitted");
        gmd.Send("d");
        StringAssert.Contains(ScreenText.Of(gmd.WaitFor("A epsilon.txt"), repo.Path), "    1 alpha\n▌   2 changed");

        gmd.Resize(140, 30); // Side by side, and narrower than the list of files is drawn at
        gmd.WaitFor("│▌   2 changed");

        gmd.Send("v");
        Assert.AreEqual(
            "In one column: v shows the sides beside each other",
            ScreenText.LastLine(gmd.WaitFor("In one column"))
        );
        StringAssert.Contains(
            ScreenText.Of(gmd.Capture(), repo.Path),
            "    1 alpha\n▌   2 changed",
            "Wide, as v chose"
        );
        gmd.Send("v");
        gmd.WaitFor("│▌   2 changed");
    }

    // On a terminal 150 wide or more, the files are listed beside the diff, the one the cursor is in
    // marked. Tab moves the keys to the list, where down goes to the next file, and Esc moves them back
    // before it closes the diff.
    [TestMethod]
    public async Task TestTheFilesAreListedBesideAWideDiff()
    {
        using var repo = await E2eRepo.CreateWithLongFileAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 160, height: 30);
        gmd.WaitFor("Change both files");
        gmd.Send("d");
        var screen = ScreenText.Of(gmd.WaitFor("Files 2"), repo.Path).Split('\n');
        StringAssert.StartsWith(screen[1], " M long.txt  +1 -1");
        StringAssert.StartsWith(screen[2], " M short.txt  +1 -1");

        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Down");
        gmd.WaitFor("▌M short.txt");

        // The diff's cursor went with it: there is no file below short.txt's header
        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.Send("}");
        Assert.AreEqual("No more files below: { goes up", ScreenText.LastLine(gmd.WaitFor("No more files")));

        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.Send("Escape");
        gmd.WaitForStable();
        gmd.Send("Down");
        StringAssert.Contains(gmd.WaitForStable(), "Files 2", "Esc left the list, and the diff is still open");
        gmd.Send("Escape");
        gmd.WaitUntilGone("Files 2");
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
        gmd.WaitFor("M long.txt");

        gmd.Send("/");
        gmd.WaitFor("Text to find in the diff");
        gmd.SendText("LINE 2");
        gmd.Send("Enter");

        var found = gmd.WaitFor("Match 1 of");
        Assert.AreEqual("Match 1 of 7 for 'LINE 2'", ScreenText.LastLine(found));
        var row = ScreenText.Of(found, repo.Path).Split('\n').FindIndexBy(l => l.Contains("line 20 changed"));
        // The cursor's row: its number reversed on the left side, 'W', where the match is, and 'line 2'
        // marked, 'y', on both sides, beside the word added, 'g'
        Assert.AreEqual(
            "-  WW yyyy y.                                              ..  .. yyyy y. ggggggg",
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
        gmd.WaitFor("M long.txt");
        for (int i = 0; i < 18; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }

        gmd.Send("m");
        ScreenText.AssertEqual(
            """
            c00a3cc9  Change both files ──────────╭ Diff Menu ────────────────────────────────╮────────────────────────────────────
            Test User <test@example.com>  2024-10-│Go to                                   s >│
                                                  │Search ...                              f  │
            2 files changed  2 modified  +2 -2    │Next Match                              n  │
              M long.txt   +1 -1                  │Previous Match                         ⇧n  │
              M short.txt  +1 -1                  │Diff File                                 >│
                                                  │Resolve Conflicts                   Enter >│
            ▌ M long.txt  +1 -1 ──────────────────│Run External Merge Tool                   >│────────────────────────────────────
               @@ line 13                         │Discard Changes                         u >│
               14 line 14                         │Refresh                                 r  │
               15 line 15                         │Commit                                  c  │
               16 line 16                         │Leave Out of Commit                     x  │
               17 line 17                         │More Context of long.txt (15 lines)     +  │
               18 line 18                         │Less Context                            -  │
               19 line 19                         │Hide Whitespace Changes                 w  │
            ▌  20 line 20                         │Show in One Column                      v  │
               21 line 21                         │Focus Left Column                       ←  │
               22 line 22                         │Focus Right Column                      →  │
               23 line 23                         │Close                                 Esc  │
            """,
            ScreenText.Rows(gmd.WaitFor("Diff Menu"), repo.Path, 0, 19),
            repo.Path
        );

        // 'Less Context' is dark, i.e. disabled, while 'More Context' is white — the file is at the
        // default, so there is nothing narrower to ask for
        var colors = ScreenText.ColorRows(gmd.CaptureColors(), 12, 2).Split('\n');
        StringAssert.Contains(colors[0], "mWWWW WWWWWWW", "'More Context' and its shortcut are enabled");
        StringAssert.Contains(colors[1], "mDDDD DDDDDDD", "'Less Context' is dark, i.e. disabled");

        // Three moves down from 'Go to', past 'Search ...' and 'Refresh', since the disabled
        // items in between are skipped, 'Leave Out of Commit' too, which only the uncommitted diff has
        for (int i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        StringAssert.Contains(
            ScreenText.Of(gmd.WaitFor("(context 15)"), repo.Path),
            "▌ M long.txt  +1 -1  (context 15)",
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
        gmd.WaitFor("A delta.txt");

        gmd.Send(key);

        StringAssert.Contains(gmd.WaitUntilGone("A delta.txt"), "Merge branch 'dev' into main");
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
        gmd.WaitFor("A zeta.txt");

        foreach (var key in new[] { "P", "p", "U", "u" })
        {
            gmd.Send(key);
            gmd.WaitForStable();
        }

        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("A zeta.txt"), "▲1", "Nothing was pushed");
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
        gmd.WaitFor("M beta.txt");

        gmd.Send("w");
        var hidden = gmd.WaitFor("whitespace hidden");
        // From the files row down, the row above it being the date of the uncommitted changes, now
        Assert.AreEqual(
            """
            2 files changed  2 modified  +1  whitespace hidden
              M alpha.txt  +1
              M beta.txt   whitespace only

            ▌ M alpha.txt  +1 ─────────────────────────────────────────────────────────────────────────────────────────────────────
               @@ line 1
                1   alpha                                              │    1   alpha
                                                                       │▌   2 more

            ▌ M beta.txt  whitespace only ─────────────────────────────────────────────────────────────────────────────────────────
                  Only whitespace changed, which is hidden
            """,
            ScreenText.Rows(hidden, repo.Path, 3, 11)
        );
        Assert.AreEqual("Whitespace changes hidden: w shows them", ScreenText.LastLine(hidden));

        gmd.Send("u");
        StringAssert.Contains(gmd.WaitFor("All Changes"), "beta.txt", "Still offered to discard");
        gmd.Send("Escape");
        gmd.WaitUntilGone("All Changes");

        gmd.Send("w");
        StringAssert.Contains(gmd.WaitUntilGone("whitespace hidden"), "M beta.txt");
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
        gmd.WaitFor("M lines.txt");
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

        Assert.AreEqual(
            "Discarded 3 lines of lines.txt: Restore Discarded Changes in the Undo menu brings it back",
            ScreenText.LastLine(gmd.WaitFor("Discarded"))
        );
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

        Assert.AreEqual(
            "Discarded 1 line of lines.txt: Restore Discarded Changes in the Undo menu brings it back",
            ScreenText.LastLine(gmd.WaitFor("Discarded"))
        );
        StringAssert.EndsWith(File.ReadAllText(Path.Join(repo.Path, "lines.txt")), "line 10\nELEVEN\nline 12\n");
    }

    // x leaves the change the cursor is on out of the commit: drawn dark, counted on the file's header
    // and in the commit dialog, and a commit then takes the file's other change and leaves that one
    // in the working tree, uncommitted, with nothing staged
    [TestMethod]
    public async Task TestLeaveAChangeOutOfTheCommit()
    {
        var (repo, gmd) = await OnTheSecondChangeAsync("ELEVEN");
        using var _ = repo;
        using var __ = gmd;

        gmd.Send("x");
        Assert.AreEqual(
            "Left 3 lines of lines.txt out of the commit: x puts them back",
            ScreenText.LastLine(gmd.WaitFor("out of the commit"))
        );
        var diff = ScreenText.Of(gmd.Capture(), repo.Path);
        StringAssert.Contains(diff, "▌ M lines.txt  +3 -2  (3 lines left out)");
        StringAssert.Contains(diff, "╎  11 line 11");

        gmd.Send("c");
        StringAssert.Contains(gmd.WaitFor("Commit 1 change on 'main'"), "M lines.txt  3 lines left out");
        gmd.SendText("Make two TWO");
        gmd.Send("Enter");
        gmd.WaitFor("Committed to 'main'");

        Assert.AreEqual(
            "line 1\nTWO\nline 3",
            string.Join("\n", (await repo.GitAsync("show HEAD:lines.txt")).Split('\n').Take(3))
        );
        Assert.IsFalse((await repo.GitAsync("show HEAD:lines.txt")).Contains("ELEVEN"), "Left out of the commit");
        StringAssert.Contains(File.ReadAllText(Path.Join(repo.Path, "lines.txt")), "ELEVEN\nextra\n", "Still there");
        Assert.AreEqual(" M lines.txt", (await repo.GitAsync("status --porcelain")).TrimEnd());
    }

    // A line left out that is not found as it was, the file changed since, stops the commit, with
    // nothing committed, rather than it going in unseen
    [TestMethod]
    public async Task TestACommitStopsWhenALineLeftOutIsNotFound()
    {
        var (repo, gmd) = await OnTheSecondChangeAsync("ELEVEN");
        using var _ = repo;
        using var __ = gmd;
        var head = await repo.GitAsync("rev-parse HEAD");
        gmd.Send("x");
        gmd.WaitFor("out of the commit");
        gmd.Send("Escape");
        gmd.WaitUntilGone("M lines.txt");
        repo.WriteFile("lines.txt", File.ReadAllText(Path.Join(repo.Path, "lines.txt")).Replace("ELEVEN", "ELEVEN!"));

        gmd.Send("c");
        gmd.WaitFor("3 lines left out");
        gmd.SendText("Make two TWO");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitFor("has changed since the diff was read"), "lines.txt");
        Assert.AreEqual(head, await repo.GitAsync("rev-parse HEAD"), "Nothing committed");
    }

    // Every discard is kept, and Restore Discarded Changes, in the commit menu's Undo, puts it back:
    // here all changes, a file changed and a new one, discarded from the diff and back as they were,
    // the new file untracked again and nothing staged. The status line says where the way back is.
    [TestMethod]
    public async Task TestDiscardedChangesArePutBack()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("©2 uncommitted changes");
        var before = await repo.GitAsync("status --porcelain");
        gmd.Send("d");
        gmd.WaitFor("A epsilon.txt");
        gmd.Send("u");
        gmd.WaitFor("All Changes");
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitFor("Discard all uncommitted changes?"), "Restore Discarded Changes");
        gmd.Send("Left"); // From No, the default, to Yes
        gmd.WaitForStable();
        gmd.Send("Enter");
        Assert.AreEqual(
            "Discarded all changes: Restore Discarded Changes in the Undo menu brings it back",
            ScreenText.LastLine(gmd.WaitFor("Discarded all changes"))
        );
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
        gmd.Send("Escape");
        gmd.WaitUntilGone("A epsilon.txt");

        // The commit menu starts on 'Amend ...', and 'Undo' is three moves down, past 'Commit Diff'
        // and 'Mark for Diff'; in it, on 'Undo Commit', main's last change, as DeletedBranchesDlgTest
        // counts, the item is three moves down, past the two Recover and Restore before it
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Restore Discarded Changes ...");
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitFor("Time              Discarded"), "All changes");
        gmd.Send("r");

        Assert.AreEqual("Restored all changes", ScreenText.LastLine(gmd.WaitFor("Restored all changes")));
        Assert.AreEqual(before, await repo.GitAsync("status --porcelain"));
        Assert.AreEqual("alpha\nchanged\n", File.ReadAllText(Path.Join(repo.Path, "alpha.txt")));
    }

    // A file moved, with no 'git mv', and changed in two places: one change left out with x, and the
    // commit takes the rename with the other, the old path gone, and leaves the one out uncommitted
    [TestMethod]
    public async Task TestLeaveAChangeOfARenamedFileOut()
    {
        using var repo = await E2eRepo.CreateAsync();
        var lines = Enumerable.Range(1, 8).Select(i => $"line {i}").ToList();
        await repo.CommitFileAtAsync("old.txt", Text(lines), "Add lines", TempRepo.BaseTime.AddMinutes(7));
        File.Move(Path.Join(repo.Path, "old.txt"), Path.Join(repo.Path, "new.txt"));
        lines[0] = "LINE 1";
        lines[7] = "LINE 8";
        repo.WriteFile("new.txt", Text(lines));
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");
        gmd.Send("d");
        gmd.WaitFor("new.txt");
        gmd.Send("/");
        gmd.WaitFor("Text to find in the diff");
        gmd.SendText("LINE 8");
        gmd.Send("Enter");
        gmd.WaitFor("Match 1 of 1");

        gmd.Send("x");
        gmd.WaitFor("Left 2 lines of new.txt out of the commit");
        gmd.Send("c");
        // Moved with no 'git mv', the file is new and the old one deleted as the dialog lists them
        StringAssert.Contains(gmd.WaitFor("2 lines left out"), "A new.txt  2 lines left out");
        gmd.SendText("Move the lines");
        gmd.Send("Enter");
        gmd.WaitFor("Committed to 'main'");

        StringAssert.StartsWith(await repo.GitAsync("show HEAD:new.txt"), "LINE 1\n");
        StringAssert.EndsWith(await repo.GitAsync("show HEAD:new.txt"), "line 8");
        Assert.AreEqual("", await repo.GitAsync("ls-tree --name-only HEAD old.txt"), "The old path gone");
        Assert.AreEqual(" M new.txt", (await repo.GitAsync("status --porcelain")).TrimEnd());
    }

    // A line of a deleted file discarded is put back, the file there again with that line alone
    [TestMethod]
    public async Task TestDiscardLinesOfADeletedFile()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.CommitFileAtAsync(
            "lines.txt",
            "one\ntwo\nthree\nfour\n",
            "Add lines",
            TempRepo.BaseTime.AddMinutes(7)
        );
        repo.DeleteFile("lines.txt");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");
        gmd.Send("d");
        gmd.WaitFor("lines.txt");
        gmd.Send("/");
        gmd.WaitFor("Text to find in the diff");
        gmd.SendText("two");
        gmd.Send("Enter");
        gmd.WaitFor("Match 1 of 1");

        gmd.Send("S-Down"); // Selects the row the cursor is on
        gmd.WaitForStable();
        gmd.Send("u");
        gmd.WaitFor("Selected Lines in lines.txt");
        gmd.Send("Enter");
        gmd.WaitFor("Discard the selected lines in lines.txt?");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("Discarded 1 line of lines.txt");

        Assert.AreEqual("two\n", File.ReadAllText(Path.Join(repo.Path, "lines.txt")));
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
        gmd.WaitFor("A delta.txt");
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
        gmd.WaitFor("A epsilon.txt");
        gmd.Send("u");
        gmd.WaitFor("All Changes");
        gmd.Send(move);
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor(question);

        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone(question), "A epsilon.txt", "Still in the diff");
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
        gmd.WaitFor("A delta.txt");

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
        gmd.WaitFor("A delta.txt");

        gmd.Send(key);
        gmd.WaitFor("copied to the clipboard");
        StringAssert.EndsWith(gmd.Clipboard(), "/gmd/doc/help.md#diff-and-blame");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("copied to the clipboard"), "A delta.txt", "Back in the diff");
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
        StringAssert.Contains(diff, "A delta.txt");
        StringAssert.Contains(diff, "A gamma.txt", "And what the merge brought in between");
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
