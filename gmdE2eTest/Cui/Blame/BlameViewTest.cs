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
            Blame  alpha.txt  @7e09a8   4 lines, 2 commits
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┌ 65480e Test User   2024-10-15 │   1┃one
            └                               │   2┃two
            ┌ 7e09a8 Test User   2024-10-15 │   3┃CHANGED
            └                               │   4┃FOUR
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
            Id:         199af616c757fa248670aa1ea368ba31d046f1e3  ({repo})
            Branch:     main  (main)
            Author:     Test User, time: 2024-10-15 12:07:00 +00:00
            Children:
            Parents:    17d85b
            Tips:       (main)
            Add lines

            A body line that only the log knows.
            """,
            ScreenText.Rows(gmd.WaitFor("A body line"), repo.Path, 30, 9)
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
    // commit changed an older version, and the details pane opening above the line
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
            "m menu  Esc close  Enter details  d diff  l line history  p previous  i copy id  g gutter ─"
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
