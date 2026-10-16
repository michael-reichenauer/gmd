using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Blame;

// The blame view, opened from the commit menu: the gutter with its run bracket, the commit
// details pane, and closing it.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class BlameViewTest
{
    // The blame view is the only place the run bracket is drawn, so it is asserted here rather
    // than only as rows: '┌ │ └' for a run of several lines, '╺' for a run of one, and the sid,
    // author and date named once per run instead of on every line, which is the point of it.
    [TestMethod]
    public async Task TestBlameFile()
    {
        using var repo = await E2eRepo.CreateAsync();
        // Two commits over one file, so the blame has two runs of two lines each. It has to be
        // alpha.txt: the file picker opens on the first file of the tree and OpenBlameOf takes it.
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("alpha.txt", "one\ntwo\nthree\nfour\n", "Add lines", t.AddMinutes(7));
        await repo.CommitFileAtAsync("alpha.txt", "one\ntwo\nCHANGED\nFOUR\n", "Change lines", t.AddMinutes(8));

        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        OpenBlameOf(gmd, "alpha.txt");

        Assert.AreEqual(
            """
            Blame  alpha.txt  @7e09a86a   4 lines, 2 commits
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┌ 65480ece Test User   2024-10-15 │   1┃one
            └                                 │   2┃two
            ┌ 7e09a86a Test User   2024-10-15 │   3┃CHANGED
            └                                 │   4┃FOUR
            """,
            ScreenText.Rows(gmd.WaitFor("CHANGED"), repo.Path, 0, 6)
        );
    }

    // Enter toggles the same commit details pane the log view shows, for the current line's commit.
    // The blame itself only knows the first line of the message and nothing about branches, so this
    // is also what proves the details are read from the shown log.
    [TestMethod]
    public async Task TestBlameCommitDetails()
    {
        using var repo = await E2eRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync(
            "alpha.txt",
            "one\ntwo\n",
            "Add lines\n\nA body line that only the log knows.",
            t.AddMinutes(7)
        );

        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("Blame  alpha.txt");

        gmd.Send("Enter");

        Assert.AreEqual(
            """
            Id:       199af616c757fa248670aa1ea368ba31d046f1e3
            Branch:   main
            Author:   Test User, 2024-10-15 12:07:00 +00:00
            Parents:  17d85ba8

            Add lines

            A body line that only the log knows.
            """,
            DetailsRows(gmd.WaitFor("A body line"), repo.Path, 8)
        );
    }

    // Both cases close the blame view, and neither quits the application
    [TestMethod]
    [DataRow("q")]
    [DataRow("Q")]
    public async Task TestBlameViewClosesWithQ(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("Blame  alpha.txt");

        gmd.Send(key);

        StringAssert.Contains(gmd.WaitUntilGone("Blame  alpha.txt"), "Merge branch 'dev' into main");
        Assert.IsTrue(gmd.IsRunning, $"'{key}' should close the blame view, not quit gmd");
    }

    // A key the blame view has no use for does nothing there. It used to fall through to the log view
    // below, where 'U' updates every branch and 'u' pulls the current one.
    [TestMethod]
    public async Task TestLogViewKeysDoNothingInTheBlame()
    {
        using var repo = await E2eRepo.CreateBehindOriginAsync();
        var localMain = await repo.GitAsync("rev-parse main");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("▼1");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("Blame  alpha.txt");

        foreach (var key in new[] { "U", "u" })
        {
            gmd.Send(key);
            gmd.WaitForStable();
        }

        gmd.Send("q");
        StringAssert.Contains(gmd.WaitUntilGone("Blame  alpha.txt"), "▼1", "Nothing was pulled");
        Assert.AreEqual(localMain, await repo.GitAsync("rev-parse main"), "main is untouched");
    }

    // Both cases open the menu, as in the diff view
    [TestMethod]
    [DataRow("m")]
    [DataRow("M")]
    public async Task TestBlameMenuOpensWithM(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("Blame  alpha.txt");

        gmd.Send(key);

        gmd.WaitFor("Blame: alpha.txt");
    }

    // 'Blame File ...' is the second last item of the commit menu, so 'End' and two 'Up' is the
    // steadier walk to it than counting downwards past the items OnCursorDown skips. One key per
    // Send with a wait after each, since a menu redraw drops whatever was sent behind it.
    // 'i' copies the line's commit id and Shift-I its whole message, as they copy a commit's in the
    // log view; the message from the log, since the blame has only its subject. 'g' steps the
    // gutter's detail down, the author first.
    [TestMethod]
    public async Task TestCopyTheLineCommitIdAndMessage()
    {
        using var repo = await E2eRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("alpha.txt", "one\ntwo\n", "Add lines\n\nA body line.", t.AddMinutes(7));

        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("two");

        gmd.Send("i");
        var id = gmd.WaitForClipboard();
        Assert.AreEqual(await repo.GitAsync("rev-parse HEAD"), id);

        gmd.Send("I");
        Assert.AreEqual("Add lines\n\nA body line.", gmd.WaitForClipboard(previous: id));

        gmd.Send("g");
        gmd.WaitUntilGone("Test User"); // The author is dropped first
    }

    // The key hints of the blame view, on its bottom row as the log has them: 'p' for a line whose
    // commit changed an older version, and the details pane opening above the line. At 120 columns
    // the last of them, 'g gutter', is left out for want of room.
    [TestMethod]
    public async Task TestBlameHasHintsOfItsOwn()
    {
        using var repo = await E2eRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("alpha.txt", "one\ntwo\n", "Add lines", t.AddMinutes(7));
        await repo.CommitFileAtAsync("alpha.txt", "one\nTWO\n", "Change a line", t.AddMinutes(8));
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        gmd.WaitFor("Initial");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("TWO");
        gmd.Send("Down");

        var hints = ScreenText.LastLine(gmd.WaitFor("p previous")).TrimStart('─', ' ');
        StringAssert.StartsWith(
            hints,
            "m menu  Esc close  Enter details  d diff  l line history  p previous  f search  i copy id ─"
        );
        StringAssert.EndsWith(hints, "? help");

        gmd.Send("Enter");
        var screen = gmd.WaitFor("hide details");
        StringAssert.Contains(screen, "Id:", "The details are shown");
        StringAssert.Contains(ScreenText.LastLine(screen), "Enter hide details", "With the hints still under them");
    }

    // 'l' shows every commit that changed the current line, newest first, each with the diff of that
    // line alone: here the second line of alpha.txt, added as 'two' and then made 'TWO', and not the
    // commit that made the file, which made only the first line
    [TestMethod]
    public async Task TestTheHistoryOfALine()
    {
        using var repo = await E2eRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("alpha.txt", "alpha\ntwo\n", "Add lines", t.AddMinutes(7));
        await repo.CommitFileAtAsync("alpha.txt", "alpha\nTWO\n", "Change a line", t.AddMinutes(8));
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("TWO");
        gmd.Send("Down");
        gmd.WaitForStable();

        gmd.Send("l");

        var history = gmd.WaitFor("Change a line");
        StringAssert.Contains(history, "Add lines");
        Assert.IsFalse(history.Contains("Initial"), "The commit that made the file did not make this line");
    }

    // A line history is 'git log -L', which takes no context: '+' and '-' are not offered, and '+'
    // does nothing. It read the same lines again, and headed the file '(context 15)'.
    [TestMethod]
    public async Task TestTheHistoryOfALineHasNoContextToStep()
    {
        using var repo = await E2eRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("alpha.txt", "alpha\ntwo\n", "Add lines", t.AddMinutes(7));
        await repo.CommitFileAtAsync("alpha.txt", "alpha\nTWO\n", "Change a line", t.AddMinutes(8));
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        gmd.WaitFor("Initial");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("TWO");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("l");
        var history = gmd.WaitFor("Change a line");
        var reads = gmd.LogCount("-L2,2:alpha.txt");

        StringAssert.Contains(ScreenText.LastLine(history), "} { file", "The diff's own hints");
        Assert.IsFalse(ScreenText.LastLine(history).Contains("context"), "No '+ -' hint");

        gmd.Send("+");
        gmd.WaitForStable();
        gmd.Send("m");
        var menu = gmd.WaitFor("Diff Menu");
        Assert.IsFalse(menu.Contains("(context"), "The file is not said to have more context");
        Assert.AreEqual(reads, gmd.LogCount("-L2,2:alpha.txt"), "The history is not read again");
    }

    // The diff of an uncommitted line, from the blame of the working tree, is read only: 'c' closed it
    // and opened no commit dialog, the blame being two dialogs deep, and a discard left the blame
    // showing the lines it had changed
    [TestMethod]
    public async Task TestTheDiffOfAnUncommittedLineIsReadOnly()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        gmd.WaitFor("Initial");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("changed");
        gmd.Send("Down");
        gmd.WaitForStable();

        gmd.Send("d");
        var diff = gmd.WaitFor("} { file");

        Assert.IsFalse(ScreenText.LastLine(diff).Contains("commit"), "No 'c commit' hint");
        Assert.IsFalse(ScreenText.LastLine(diff).Contains("discard"), "No 'u discard' hint");
        gmd.Send("c");
        gmd.WaitForStable();
        gmd.Send("u");
        var screen = gmd.WaitForStable();
        StringAssert.Contains(ScreenText.LastLine(screen), "} { file", "The diff is still open");
        Assert.IsFalse(screen.Contains("Discard Changes"), "Nothing to discard");
        Assert.AreEqual("alpha\nchanged\n", File.ReadAllText(Path.Join(repo.Path, "alpha.txt")));
    }

    // The first rows of the details pane, less the scrollbar at the right edge, which the files the
    // commit changed bring when they are read
    static string DetailsRows(string screen, string repoPath, int count) =>
        string.Join(
            "\n",
            ScreenText.Rows(screen, repoPath, 30, count).Split('\n').Select(r => r.TrimEnd('┃').TrimEnd())
        );

    // The code is searched, as in the diff, and not the gutter beside it: 'o' is in three lines, and
    // 'Test User', the author on every line, is in none. n before any search says how to start one.
    [TestMethod]
    public async Task TestSearchTheBlame()
    {
        using var repo = await E2eRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("alpha.txt", "one\ntwo\nthree\nfour\n", "Add lines", t.AddMinutes(7));
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenBlameOf(gmd, "alpha.txt");
        gmd.WaitFor("four");

        gmd.Send("n");
        Assert.AreEqual(
            "No search to go on with: search with f first",
            ScreenText.LastLine(gmd.WaitFor("No search to go on with"))
        );

        gmd.Send("f");
        gmd.WaitFor("Text to find in the file");
        gmd.SendText("O");
        gmd.Send("Enter");
        Assert.AreEqual("Match 1 of 3 for 'O'", ScreenText.LastLine(gmd.WaitFor("Match 1 of 3")));
        gmd.Send("n");
        gmd.WaitFor("Match 2 of 3");
        gmd.Send("n");
        gmd.WaitFor("Match 3 of 3");
        gmd.WaitForStable();
        // The cursor on the third match, 'four', highlighted, 'D', and each 'o' marked, 'y'
        Assert.AreEqual(
            """
            - -------- ---- ----   ---------- -   --y..
            -                                 -   ----y
            -                                 -   -------
            -                                 D   DDDyDD
            """,
            ScreenText.BackgroundRows(gmd.CaptureColors(), 2, 4)
        );
        gmd.Send("n");
        Assert.AreEqual(
            "No more matches for 'O' below: ⇧n goes back up",
            ScreenText.LastLine(gmd.WaitFor("No more matches"))
        );

        gmd.Send("f");
        gmd.WaitFor("Text to find in the file");
        gmd.SendText("Test User");
        gmd.Send("Enter");
        gmd.WaitFor("'Test User' is not in alpha.txt");
    }

    static void OpenBlameOf(TmuxSession gmd, string path)
    {
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor(path);
        gmd.Send("Enter");
    }
}
