using gmd.Common;
using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// The log view itself: the screen gmd starts on and how it narrows, its colors and the current
// row, the keys that quit, scroll and open the details pane, the commit menu, the help, and
// showing and hiding a branch — everything that looks at the repository without changing it.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class LogViewTest
{
    // The whole screen at the standard size, i.e. the application bar, the graph, the subjects,
    // the branch tip and tag decoration and the sid, author and time columns. The one test that
    // would have caught every startup level regression this project has hit.
    [TestMethod]
    public async Task TestStartupShowsTheLogView()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                  (● main)[v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                              4e73d230 Test User      2024-10-15 12:05
            ┣    Add gamma                                                                 4a15fb13 Test User      2024-10-15 12:04
            ┣╯   Add beta                                                                  dd78912d Test User      2024-10-15 12:01
            ┗    Initial                                                                   9dc406e3 Test User      2024-10-15 12:00
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );
    }

    // The guard on every other end-to-end test: that the redirected HOME actually took, i.e. that
    // gmd wrote its state into the throwaway home rather than into the developer's. If another
    // write anchored on SpecialFolder.UserProfile is ever added, this is where it shows up.
    // A subject in Japanese, an emoji in one and an author named in kanji leave the sid, author and
    // time columns where they are: measured in chars rather than in columns, each wide character
    // moved the columns after it one more, off the screen. The emoji is drawn as itself, two
    // columns, where Terminal.Gui 1.x's curses drew '~I' and gmd drew '�' in its place
    [TestMethod]
    public async Task TestWideCharactersKeepTheColumnsInPlace()
    {
        using var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("a.txt", "a\n", "Plain subject", t);
        await repo.CommitFileAtAsync("a.txt", "b\n", "日本語のコミットメッセージ", t.AddMinutes(1));
        await repo.CommitFileAtAsync("a.txt", "c\n", "Party 🎉 time", t.AddMinutes(2));
        repo.WriteFile("a.txt", "d\n");
        await repo.GitAsync("add a.txt");
        repo.GitAt(["commit", "-q", "--author=山田太郎 <taro@example.com>", "-m", "Author in kanji"], t.AddMinutes(3));
        using var gmd = TmuxSession.StartGmd(repo);

        Assert.AreEqual(
            """
            ┣ ● Author in kanji                                                   (● main) 5db3922e 山田太郎       2024-10-15 12:03
            ┣   Party 🎉 time                                                              e24544aa Test User      2024-10-15 12:02
            ┣   日本語のコミットメッセージ                                                 43de22f6 Test User      2024-10-15 12:01
            ┗   Plain subject                                                              e42df25a Test User      2024-10-15 12:00
            """,
            ScreenText.Rows(gmd.WaitFor("Plain subject"), repo.Path, 2, 4)
        );
    }

    [TestMethod]
    public async Task TestRunsUnderTheThrowawayHome()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        var config = File.ReadAllText(Path.Join(gmd.Home, ".gmdconfig"));
        StringAssert.Contains(config, repo.Path, "The opened repo should be remembered in the temp home");
        StringAssert.Contains(config, "\"GitVersion\"", "The git version should be written to the temp home");

        Assert.IsTrue(File.Exists(Path.Join(gmd.Home, "gmd.log")), "gmd should log into the temp home");
    }

    // A repository opens showing main and the current branch, and the other branches are dark ╮ and
    // ╯ marks, which someone new can take for branches gone missing. The first repository with
    // hidden branches says so on the status line, over the bottom row with the key hints off, and
    // the config remembers that it was told, so it is said once. (Every other test opens with it
    // told already, see TempHome.)
    [TestMethod]
    public async Task TestTheFirstRepoSaysWhatIsHidden()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, isHiddenBranchesTip: true);

        var screen = gmd.WaitFor("other branch is hidden");
        Assert.AreEqual(
            "Showing main; 1 other branch is hidden: ⇧→ shows one, as does a click on a dark ╮ or ╯",
            ScreenText.LastLine(screen)
        );

        var config = File.ReadAllText(Path.Join(gmd.Home, ".gmdconfig"));
        StringAssert.Matches(config, new System.Text.RegularExpressions.Regex("\"IsHiddenBranchesTold\":\\s*true"));
    }

    [TestMethod]
    public async Task TestQuitWithQ()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("q");

        gmd.WaitForExit();
        Assert.IsFalse(gmd.IsRunning);
    }

    // Upper case too, which is the case gmd/doc/help.md documents ("Esc / Q")
    [TestMethod]
    public async Task TestQuitWithUpperCaseQ()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Q");

        gmd.WaitForExit();
        Assert.IsFalse(gmd.IsRunning);
    }

    // Escape asks before it quits. Everywhere else in gmd it means close or back, so one too many
    // is an easy slip, and it used to quit on the spot. Yes is the default, so Escape then Enter
    // still quits in a moment.
    [TestMethod]
    public async Task TestEscapeAsksBeforeQuitting()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Escape");
        gmd.WaitFor("Quit gmd?");
        gmd.Send("Enter");

        gmd.WaitForExit();
        Assert.IsFalse(gmd.IsRunning);
    }

    // ... and a second Escape, which is what the slip looks like, answers the question rather than
    // quitting. So does it with the commit details open and moved into, since that pane takes Escape
    // itself when it has the focus.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TestEscapeTwiceStaysInTheLog(bool isInDetails)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        if (isInDetails)
        {
            gmd.Send("Enter");
            gmd.WaitFor("Parents:");
            gmd.Send("Tab");
            gmd.WaitForStable();
        }

        gmd.Send("Escape");
        gmd.WaitFor("Quit gmd?");
        gmd.Send("Escape");

        StringAssert.Contains(gmd.WaitUntilGone("Quit gmd?"), "Merge branch 'dev' into main");
        Assert.IsTrue(gmd.IsRunning, "A second Escape should answer No");
    }

    // The X in the application bar asks as Escape does: it is a single click, beside the '?' of
    // the help
    [TestMethod]
    public async Task TestClickingTheXAsksBeforeQuitting()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        var (x, y) = TmuxSession.PositionOf(gmd.WaitFor("Initial"), "? X");

        gmd.Click(x + 2, y);
        gmd.WaitFor("Quit gmd?");
        gmd.Send("Escape");

        gmd.WaitUntilGone("Quit gmd?");
        Assert.IsTrue(gmd.IsRunning, "No is the answer Escape gives");
    }

    // The quit keys are registered on the log view, so a dialog above it has to swallow them or
    // typing a 'q' into a text field would quit gmd. Worth pinning rather than assuming, since
    // it is what makes registering both cases of the key safe.
    [TestMethod]
    public async Task TestTypingQuitKeysIntoADialogDoesNotQuit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");

        gmd.SendText("qQ");

        StringAssert.Contains(gmd.WaitFor("Search: qQ"), "Search: qQ");
        Assert.IsTrue(gmd.IsRunning, "Typing a quit key into a dialog should not quit gmd");
    }

    // RepoWriter.ColumnWidths drops the sid, author and time columns below a commit width of 70.
    // A whole arm of that calculation, with no other coverage. The application bar is too narrow for
    // 'commit on main' as well, which it leaves out rather than cut off the search, '?' and 'X'.
    [TestMethod]
    public async Task TestNarrowWidthDropsTheSidAuthorAndTimeColumns()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 70, height: 20);

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main            [Ϙ Search] ? X
            ──────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                         (● main)[v1.0]
            ┣╮   Merge branch 'dev' into main
            ┣    Add gamma
            ┣╯   Add beta
            ┗    Initial
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );
    }

    // The two arms between the narrowest and the widest, which is where RepoWriter.ColumnWidths
    // does its most invisible work: it drops and shortens columns with no marker of any kind,
    // since Txt truncates with a plain substring rather than the '┅' the rest of the UI uses.
    //
    // The ladder is on 'commitWidth', not on the pane width: commitWidth = width + 1 - (graphWidth
    // + 3), so the pane width that lands in a given arm depends on how wide the graph is, i.e. on
    // the fixture and on which branches are shown. These two widths were measured against this
    // fixture rather than calculated, and 'dev' is left hidden so the graph stays 6 columns.
    [TestMethod]
    public async Task TestMediumWidthDropsTheSidAndCutsTheTimeToADate()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 95, height: 12);

        // commitWidth 70..99: no sid at all, and the time is cut to its date — the clock is gone
        // with nothing to say so. The author is not visibly cut, since ' Test User' is exactly the
        // 10 columns it is given; a fixture with a longer author name would be needed to see that.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                      commit on main [Ϙ Search] ? X
            ───────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                             (● main)[v1.0] Test User 2024-10-15
            ┣╮   Merge branch 'dev' into main                                         Test User 2024-10-15
            ┣    Add gamma                                                            Test User 2024-10-15
            ┣╯   Add beta                                                             Test User 2024-10-15
            ┗    Initial                                                              Test User 2024-10-15
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );
    }

    [TestMethod]
    public async Task TestNearlyFullWidthKeepsTheSidButStillCutsTheTime()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 112, height: 12);

        // commitWidth 100..109: the sid is back, the time is still a bare date. Seventeen columns
        // narrower than the full arm the other tests here run at, and the only difference is this.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                       commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                     (● main)[v1.0] 17d85ba8 Test User 2024-10-15
            ┣╮   Merge branch 'dev' into main                                                 4e73d230 Test User 2024-10-15
            ┣    Add gamma                                                                    4a15fb13 Test User 2024-10-15
            ┣╯   Add beta                                                                     dd78912d Test User 2024-10-15
            ┗    Initial                                                                      9dc406e3 Test User 2024-10-15
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );
    }

    // The colors a user actually sees, which nothing else in the suite reaches: GraphText.ColorsOf
    // asserts what GraphWriter produced, not what was drawn. Showing many branches at once is what
    // this application is for, so which color each one got is a product feature, not decoration.
    [TestMethod]
    public async Task TestLogViewColors()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // Read it against the picture in TestStartupShowsTheLogView, which it lines up with from
        // row 2 down. Row 0 is the application bar: ' Gmd ' bright magenta, the repo path dark,
        // the current branch magenta, 'commit on' white and 'main' magenta for the row's commit,
        // '[Ϙ Search]' dark, ' ? ' bright cyan. Row 1 is its border.
        // Then one row per commit: the graph rune in the branch color, the subject white, the
        // '[v1.0]' tag green, the sid cyan and the author and time dark.
        //
        // Three of these are the point. Main is magenta, which BranchColorService guarantees and
        // no other test checks reaches the screen. The dark 'D' second rune on the merge rows is
        // the '╮'/'╯' marker a hidden branch leaves behind, which GraphTest asserts on
        // GraphWriter's output rather than on what was drawn. And the current row's author and
        // time are white rather than dark, because the highlight lifts them.
        Assert.AreEqual(
            """
             mmm DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD WMMMM                                               WWWWWW WW MMMM DD DDDDDDD c W
            mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm
            M  W WWW WWWWW                                                  MW MMMMMGGGGGG CCCCCCCC WWWW WWWW      WWWWWWWWWW WWWWW
            MD   WWWWW WWWWWW WWWWW WWWW WWWW                                              CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            M    WWW WWWWW                                                                 CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            MD   WWW WWWW                                                                  CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            M    WWWWWWW                                                                   CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            """,
            ScreenText.ColorRows(gmd.CaptureColors(), 0, 8)
        );

        // Main is a special case, always magenta, so showing 'dev' as well is what covers the
        // palette that every other branch gets its color from — a SHA256 of the branch name into
        // five colors, nudged if it collides with the parent branch. Which color a given name
        // lands on is a promise to the user: it is why a branch keeps its color between runs.
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitFor("Merge branch");
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");

        // 'dev' comes out green, and its '(dev)' tip with it. The white subjects moved with the
        // cursor: RepoWriter.GetSubjectText draws a commit white when it is on the same branch as
        // the row the cursor is on and dark otherwise, so landing on dev turns main's rows dark.
        Assert.AreEqual(
            """
            M   W DDD DDDDD                                                 MW MMMMMGGGGGG CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            MG    DDDDD DDDDDD DDDDD DDDD DDDD                                             CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            MG    DDD DDDDD                                                                CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            MGG   WWWW WWW WWWW                                                      GGGGG CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            MGG   WWWW WW WWW                                                              CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            MG    DDD DDDD                                                                 CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            M     DDDDDDD                                                                  CCCCCCCC DDDD DDDD      DDDDDDDDDD DDDDD
            """,
            ScreenText.ColorRows(gmd.CaptureColors(), 2, 7)
        );
    }

    // The row the cursor is on is drawn with a background rather than a foreground color, so it is
    // invisible to every other assertion here.
    [TestMethod]
    public async Task TestCurrentRowIsHighlighted()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // The first row is the one the cursor is on, and the run of dark gray 'D' is the
        // highlight. It is on the graph too, the rune and the current marker, so the node of the
        // commit is found at a glance, which it was not when the highlight stopped at the graph.
        // The rows below it are plain, black in the 16 colors, the graph too.
        Assert.AreEqual(
            """
            D  D DDD DDDDD                                                  DD DDDDDDDDDDD DDDDDDDD DDDD DDDD      DDDDDDDDDD DDDDD
            ..   ..... ...... ..... .... ....                                              ........ .... ....      .......... .....
            .    ... .....                                                                 ........ .... ....      .......... .....
            ..   ... ....                                                                  ........ .... ....      .......... .....
            """,
            ScreenText.BackgroundRows(gmd.CaptureColors(), 2, 4)
        );
    }

    // ContentView's paging keys, which nothing else exercises through a real key path
    [TestMethod]
    public async Task TestScrollingALongLog()
    {
        using var repo = await E2eRepo.CreateLongAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 120, height: 12);

        // The first page, with the scrollbar drawn at the right edge
        var first = """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣ ● Commit number 29                                                  (● main) a579eca7 Test User      2024-10-15 12:29┃
            ┣   Commit number 28                                                           a2076433 Test User      2024-10-15 12:28┃
            ┣   Commit number 27                                                           9c99a7e6 Test User      2024-10-15 12:27┃
            ┣   Commit number 26                                                           f1183fa0 Test User      2024-10-15 12:26┃
            ┣   Commit number 25                                                           d5e69c9d Test User      2024-10-15 12:25┃
            ┣   Commit number 24                                                           ff000974 Test User      2024-10-15 12:24
            ┣   Commit number 23                                                           78ab6ff2 Test User      2024-10-15 12:23
            ┣   Commit number 22                                                           9c943a3f Test User      2024-10-15 12:22
            ┣   Commit number 21                                                           ad2ca17c Test User      2024-10-15 12:21
            ┣   Commit number 20                                                           bc3421cc Test User      2024-10-15 12:20
            """;
        ScreenText.AssertEqual(first, gmd.WaitFor("Commit number 29"), repo.Path);

        // The first page down only moves the cursor to the bottom of the page it is already on,
        // so nothing scrolls. It takes a second one to move the page.
        gmd.Send("PageDown");
        Assert.AreEqual(first, ScreenText.Of(gmd.WaitForStable(), repo.Path));

        // The second one does, and the scrollbar moves down with it
        gmd.Send("PageDown");
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   Commit number 20                                                           bc3421cc Test User      2024-10-15 12:20
            ┣   Commit number 19                                                           7f6574d2 Test User      2024-10-15 12:19
            ┣   Commit number 18                                                           ba1e6912 Test User      2024-10-15 12:18
            ┣   Commit number 17                                                           02f8717e Test User      2024-10-15 12:17┃
            ┣   Commit number 16                                                           6e07b5d1 Test User      2024-10-15 12:16┃
            ┣   Commit number 15                                                           8d081870 Test User      2024-10-15 12:15┃
            ┣   Commit number 14                                                           b020d900 Test User      2024-10-15 12:14┃
            ┣   Commit number 13                                                           a91a58e0 Test User      2024-10-15 12:13┃
            ┣   Commit number 12                                                           3b1172b4 Test User      2024-10-15 12:12
            ┣   Commit number 11                                                           0399cf11 Test User      2024-10-15 12:11
            """,
            gmd.WaitUntilGone("Commit number 29"),
            repo.Path
        );

        // End goes to the last commit, and the scrollbar to the bottom
        gmd.Send("End");
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   Commit number 09                                                           36b6fd41 Test User      2024-10-15 12:09
            ┣   Commit number 08                                                           f2a4f462 Test User      2024-10-15 12:08
            ┣   Commit number 07                                                           c7829353 Test User      2024-10-15 12:07
            ┣   Commit number 06                                                           5bad1cca Test User      2024-10-15 12:06
            ┣   Commit number 05                                                           f15dfc58 Test User      2024-10-15 12:05
            ┣   Commit number 04                                                           7e31dc36 Test User      2024-10-15 12:04┃
            ┣   Commit number 03                                                           c02addfd Test User      2024-10-15 12:03┃
            ┣   Commit number 02                                                           8332dd93 Test User      2024-10-15 12:02┃
            ┣   Commit number 01                                                           5692a80d Test User      2024-10-15 12:01┃
            ┗   Commit number 00                                                           a823b76e Test User      2024-10-15 12:00┃
            """,
            gmd.WaitFor("Commit number 00"),
            repo.Path
        );
    }

    // Enter opens the details pane, which is anchored to the bottom of the screen, so only its
    // rows are asserted rather than the 22 blank ones above it. The files the commit changed come
    // a moment after the rest, read once the cursor has rested on it.
    [TestMethod]
    public async Task TestCommitDetailsPane()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Enter");
        var screen = gmd.WaitFor("A delta.txt");

        Assert.AreEqual(
            """
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            Id:       17d85ba889a1084f912c412d0ce435c9d7a36f53  [v1.0]
            Branch:   main
            Author:   Test User, 2024-10-15 12:06:00 +00:00
            Parents:  4e73d230

            Add delta

            1 file changed  +1
            A delta.txt  +1
            """,
            ScreenText.Rows(screen, repo.Path, 29, 10)
        );
    }

    // The details of the uncommitted changes list the files, as git status has them
    [TestMethod]
    public async Task TestUncommittedDetailsListTheFiles()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("2 uncommitted changes");

        gmd.Send("Enter");

        Assert.AreEqual(
            """
            Id:       Uncommitted
            Branch:   main
            Parents:  17d85ba8

            2 uncommitted changes

            M alpha.txt
            A epsilon.txt
            """,
            ScreenText.Rows(gmd.WaitFor("epsilon.txt"), repo.Path, 30, 8)
        );
    }

    // Enter hides the details it showed, and the log goes on taking keys. Hiding them hung gmd: the
    // pane, left with rows and no room for one, asked for its rows again and again, since
    // Terminal.Gui 2.x draws a view of no height.
    [TestMethod]
    public async Task TestEnterHidesTheDetails()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Enter");
        gmd.WaitFor("Parents:");
        gmd.Send("Enter");
        gmd.WaitUntilGone("Parents:");
        gmd.Send("Enter");
        gmd.WaitFor("Parents:");
    }

    // Tab moves into the details pane, and the cursor and page keys then scroll a message too long
    // for it rather than move to another commit, which they went on doing, since only the drawn focus
    // moves. Tab back and they move the commit again.
    [TestMethod]
    public async Task TestKeysScrollTheDetailsMovedInto()
    {
        using var repo = await E2eRepo.CreateAsync();
        var body = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Body line {i}"));
        await repo.CommitFileAtAsync("alpha.txt", "one\n", $"Add lines\n\n{body}", TempRepo.BaseTime.AddMinutes(7));
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        // The first row of the pane, below its top border, less the scrollbar at the right edge
        string TopOfDetails(string screen) => ScreenText.Rows(screen, repo.Path, 30, 1).TrimEnd('┃').TrimEnd();

        gmd.Send("Enter");
        gmd.WaitFor("Parents:");
        gmd.Send("Tab");
        gmd.WaitForStable();

        gmd.Send("Down");
        Assert.AreEqual("Branch:   main", TopOfDetails(gmd.WaitUntilGone("Id:")));
        gmd.Send("PageDown"); // The 10 rows less the one kept
        Assert.AreEqual("Body line 4", TopOfDetails(gmd.WaitFor("Body line 13")));
        gmd.Send("Up");
        Assert.AreEqual("Body line 3", TopOfDetails(gmd.WaitUntilGone("Body line 13")));
        gmd.Send("PageUp");
        StringAssert.StartsWith(TopOfDetails(gmd.WaitFor("Id:")), "Id:");

        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.Send("Down");
        StringAssert.StartsWith(TopOfDetails(gmd.WaitFor("Add delta")), "Id:       17d85ba889a1");
    }

    // The scrollbar is magenta in the pane the keys act on and gray in the other, so Tab moves it
    // along with the highlighted top border of the details. And the details show one only when there
    // are rows to scroll: a message that just fills the pane has none, where the blank row the
    // details used to end with gave it one that nothing moved.
    [TestMethod]
    public async Task TestScrollbarsShowWhichPaneTheKeysActOn()
    {
        using var repo = await E2eRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        for (var i = 1; i <= 30; i++) // More commits than the log shows above the details
            await repo.CommitFileAtAsync("alpha.txt", $"{i}\n", $"Change {i}", t.AddMinutes(10 + i));
        // Id, Branch, Author and Parents, the blank row below them, the message, and below a blank row
        // the count of files and the two files fill the 10 rows
        repo.WriteFile("beta.txt", "fits\n");
        await repo.CommitFileAtAsync("alpha.txt", "fits\n", "Just fits", t.AddMinutes(50));
        var body = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Body line {i}"));
        await repo.CommitFileAtAsync("alpha.txt", "long\n", $"Too long\n\n{body}", t.AddMinutes(51));
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        // The color of the last column of the log's rows and of the details' rows, less the rows at
        // the bottom where no scrollbar is drawn
        string LogScrollbar() => ScrollbarOf(gmd.CaptureColors(), 2, 27);
        // Only the color of the details' scrollbar, since its length depends on whether the files the
        // commit changed, which add rows below the message out of sight, have been read yet
        string DetailsScrollbar() => string.Concat(ScrollbarOf(gmd.CaptureColors(), 30, 10).Distinct());

        gmd.Send("Enter");
        gmd.WaitFor("Body line");
        Assert.AreEqual("MMMMMMMMMMMMMMMMMMMMM", LogScrollbar());
        Assert.AreEqual("D", DetailsScrollbar());

        // Both panes waited for, as a stable screen can be the one from before the key on a loaded
        // machine, and a capture can be taken while the redraw is still written to the terminal: the
        // log's rows above, already in their new color, and the details' below, not yet
        gmd.Send("Tab");
        gmd.WaitUntil(
            () => LogScrollbar() == "DDDDDDDDDDDDDDDDDDDDD" && DetailsScrollbar() == "M",
            "the log's scrollbar to be gray and the details' magenta"
        );

        gmd.Send("Tab");
        gmd.WaitUntil(
            () => LogScrollbar() == "MMMMMMMMMMMMMMMMMMMMM" && DetailsScrollbar() == "D",
            "the log's scrollbar to be magenta and the details' gray"
        );

        gmd.Send("Down");
        gmd.WaitFor("M beta.txt");
        Assert.AreEqual("", DetailsScrollbar());
    }

    static string ScrollbarOf(string escapedCapture, int first, int count) =>
        string.Concat(
                ScreenText
                    .ColorRows(escapedCapture, first, count)
                    .Split('\n')
                    .Select(r => r.Length == 120 ? r[^1] : ' ')
            )
            .TrimEnd();

    [TestMethod]
    public async Task TestCommitMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                               commit on main [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                  (● main)[v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮   Mer╭ Commit: 17d85ba8 ─────────────────────╮                              4e73d230 Test User      2024-10-15 12:05
            ┣    Add│Commit ...                          c  │                              4a15fb13 Test User      2024-10-15 12:04
            ┣╯   Add│Amend ...                           a  │                              dd78912d Test User      2024-10-15 12:01
            ┗    Ini│Squash ...                             │                              9dc406e3 Test User      2024-10-15 12:00
                    │Reorder                               >│
                    │Move to New Branch ...                 │
                    │───────────────────────────────────────│
                    │Commit Details                  Enter  │
                    │Commit Diff                         d  │
                    │Mark for Diff                          │
                    │File History ...                       │
                    │Blame File ...                         │
                    │───────────────────────────────────────│
                    │Create Branch from Commit ...       b  │
                    │Switch to Commit                       │
                    │Merge Commit into main                 │
                    │Cherry Pick into main                  │
                    │Set Commit Branch Manually ...         │
                    │───────────────────────────────────────│
                    │Undo                                  >│
                    │Stash                                 >│
                    │Tag                                   >│
                    │Copy                           Ctrl-C >│
                    │Open Commit in Browser                 │
                    │───────────────────────────────────────│
                    │Branches                              >│
                    │Repo Menu                          ⇧m >│
                    ╰───────────────────────────────────────╯
            """,
            gmd.WaitFor("Commit ..."),
            repo.Path
        );
    }

    // The 'Branches' submenu is the second way to the branch menu. The ← / → keys are the first,
    // and nothing on screen says so, so every branch drawn in the graph is listed here too, each
    // one opening the very menu 'm' gives on a hoovered branch.
    [TestMethod]
    public async Task TestBranchesSubMenuInCommitMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // Show dev, so the graph has two branches to list: down to the merge commit, left to hoover
        // main, enter to open the branch merged in there. Then right twice, past dev and off the
        // right side of the row, which is what clears the hoover and selects the commit again — 'm'
        // on a hoovered branch would open the branch menu instead of the commit menu.
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        gmd.Send("Right");
        gmd.WaitForStable();
        gmd.Send("Right");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Commit ...");

        // 'End' to the last item and one up is the shorter and steadier walk to 'Branches': the
        // item below it is unconditionally enabled, while several above it are not and
        // OnCursorDown skips over whatever is disabled.
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();

        // The two shown branches, left to right as the graph draws them, with the '●' marking the
        // current one. Both are submenus, so both carry the '>'. Below the separator, the items
        // that change which branches are shown at all, the undo of showing dev above among them.
        gmd.Send("Right");
        var branches = gmd.WaitFor("╭ Branches");
        Assert.AreEqual(
            """
                     │Open Commit in Browser                 │
                     │───────────────────────────────────────│╭ Branches ───────────────────╮
                     │Branches                              >││●   main                    >│
                     │Repo Menu                          ⇧m >││    dev                     >│
                     ╰───────────────────────────────────────╯│─────────────────────────────│
                                                              │Show Branch              ⇧→ >│
                                                              │Hide All Branches            │
                                                              │Undo Show 'dev'   Backspace  │
                                                              ╰─────────────────────────────╯
            """,
            ScreenText.Rows(branches, repo.Path, 30, 9)
        );

        // Down to dev and into it: the child window is titled with the branch, and its items are
        // the branch menu, built with isLimited so it has no 'Repo Menu' of its own, since the menus
        // it is under already offer it.
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        Assert.AreEqual(
            """
                     │───────────────────────────────────────│                               ╭ dev ──────────────────────────────╮
                     │Create Branch from Commit ...       b  │                               │Switch to Branch                s  │
                     │Switch to Commit                       │                               │Merge dev into main             e  │
                     │Merge Commit into main                 │                               │Merge main into dev            ⇧e  │
                     │Cherry Pick into main                  │                               │Rebase onto                       >│
                     │Set Commit Branch Manually ...         │                               │Pull                            u  │
                     │───────────────────────────────────────│                               │Publish                            │
                     │Undo                                  >│                               │───────────────────────────────────│
                     │Stash                                 >│                               │Create Branch ...               b  │
                     │Tag                                   >│                               │Create Worktree ...                │
                     │Copy                           Ctrl-C >│                               │Rename Branch ...                  │
                     │Open Commit in Browser                 │                               │Delete Branch ...                  │
                     │───────────────────────────────────────│╭ Branches ───────────────────╮│Undo Commit 'More dev work'        │
                     │Branches                              >││●   main                    >││───────────────────────────────────│
                     │Repo Menu                          ⇧m >││    dev                     >││Diff Branch to                  d >│
                     ╰───────────────────────────────────────╯│─────────────────────────────││Open Branch in Browser             │
                                                              │Show Branch              ⇧→ >││Create Pull Request in Browser     │
                                                              │Hide All Branches            ││───────────────────────────────────│
                                                              │Undo Show 'dev'   Backspace  ││Hide Branch                     h  │
                                                              ╰─────────────────────────────╯│Change Branch Color             g  │
                                                                                             ╰───────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Switch to Branch"), repo.Path, 19, 21)
        );
    }

    // The help is gmd/doc/help.md on GitHub. The session has no browser, as over ssh (TmuxSession
    // empties BROWSER and DISPLAY), so gmd copies the link and shows it, and that is what is asserted:
    // the page it would have opened. It is on main for a local build and at the commit for CI's, which
    // stamps the sha before it runs the tests, hence the link gmd itself makes is the expected one.
    [TestMethod]
    [DataRow("?")]
    [DataRow("F1")]
    public async Task TestHelpCopiesItsLinkWithNoBrowser(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send(key);
        StringAssert.Contains(
            gmd.WaitFor("copied to the clipboard"),
            "There is no browser to open it in here, so the link to the help"
        );
        var link = gmd.Clipboard();
        StringAssert.StartsWith(link, "https://github.com/michael-reichenauer/gmd/blob/");
        StringAssert.EndsWith(link, "/gmd/doc/help.md");
        Assert.AreEqual(Project.HelpUrl(), link);

        // The box closes and the log view is still there behind it
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitUntilGone("copied to the clipboard"), "Add delta");
    }

    // Interactive branch visibility is the feature this application exists for, and it had no
    // end-to-end coverage at all. Asserted as a round trip rather than two independent
    // snapshots: after showing and hiding again the screen has to be what it started as.
    [TestMethod]
    public async Task TestShowAndHideBranchRoundTrip()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        var before = gmd.WaitFor("Initial");

        // Down to the merge commit, left to hoover the branch it merged in, enter to show it.
        //
        // Both waits are for the screen to settle rather than for any text, and that is as strong
        // as this one gets: neither key changes anything drawn. Moving the current row only moves a
        // highlight, which is a background, and moving the hoover shows nowhere at all — the
        // application bar keeps naming 'main' until the branch is actually shown. So there is no
        // 'wait for what changed' to use here, and WaitFor("Merge branch") would only have looked
        // like one, since that text is already on screen before the first key.
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                commit on dev [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ● Add delta                                                 (● main)[v1.0] 17d85ba8 Test User      2024-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                             4e73d230 Test User      2024-10-15 12:05
            ┣│    Add gamma                                                                4a15fb13 Test User      2024-10-15 12:04
            ┃╰╊   More dev work                                                      (dev) af3ee699 Test User      2024-10-15 12:03
            ┃╭┺   Work on dev                                                              d997ad22 Test User      2024-10-15 12:02
            ┣╯    Add beta                                                                 dd78912d Test User      2024-10-15 12:01
            ┗     Initial                                                                  9dc406e3 Test User      2024-10-15 12:00
            """,
            gmd.WaitFor("More dev work"),
            repo.Path
        );

        // And hiding it again gives back exactly the screen we started with. The highlight is still on
        // main, which is always shown, so it moves on to dev first: 'h' on main used to hide every
        // branch made from it, dev too, and says main is always shown now.
        gmd.Send("Right");
        gmd.WaitForStable();
        gmd.Send("h");
        ScreenText.AssertEqual(ScreenText.Of(before, repo.Path), gmd.WaitUntilGone("More dev work"), repo.Path);
    }

    // A log that shrinks under the cursor leaves the cursor where it was, past its last row (see
    // ContentScroll), and 'm' opened the commit menu for the row the cursor is on, which threw and
    // ended gmd. The menu is the last commit's, as the key hints and every key already take it.
    [TestMethod]
    public async Task TestTheMenuKeyAfterTheLogShrankUnderTheCursor()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        // Show dev from the merge commit, which puts the cursor on dev's tip, then down to the last row
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        foreach (var _ in Enumerable.Range(0, 3))
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right"); // The hoover followed down onto main, and right of it is the commit
        gmd.WaitForStable();

        // Back to dev hidden, two rows fewer, so the cursor is below the last of them
        gmd.Send("BSpace");
        gmd.WaitUntilGone("More dev work");
        gmd.Send("m");

        StringAssert.Contains(gmd.WaitFor("Commit: 9dc406e3"), "Commit Diff", "The menu of 'Initial', the last row");
        Assert.IsTrue(gmd.IsRunning, "gmd is still running");
    }

    // Backspace goes back to the branches shown before the last show or hide, one step at a time, as
    // a browser goes back a page, and says what it undid. With nothing left to undo it says so,
    // rather than doing nothing. The rows are compared without the bottom one, which the message
    // is drawn over: the top bar, its line and the commits, nine rows with dev shown and seven without.
    [TestMethod]
    public async Task TestBackspaceUndoesTheShowsAndHides()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        var before = gmd.WaitFor("Initial");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        var shown = gmd.WaitFor("More dev work");
        // The hoover is still on main, the one branch Left found on the merge row, and the cursor on
        // dev's tip, where Right moves it to dev, for h to hide dev rather than what hangs off main
        gmd.Send("Right");
        gmd.WaitForStable();
        gmd.Send("h");
        gmd.WaitUntilGone("More dev work");

        gmd.Send("BSpace");
        var undone = gmd.WaitFor("Undid Hide 'dev'");
        Assert.AreEqual(ScreenText.Rows(shown, repo.Path, 0, 9), ScreenText.Rows(undone, repo.Path, 0, 9));

        gmd.Send("BSpace");
        var undoneAgain = gmd.WaitFor("Undid Show 'dev'");
        Assert.AreEqual(ScreenText.Rows(before, repo.Path, 0, 7), ScreenText.Rows(undoneAgain, repo.Path, 0, 7));

        gmd.Send("BSpace");
        Assert.AreEqual(
            "No branch has been shown or hidden to undo",
            ScreenText.LastLine(gmd.WaitFor("No branch has been shown"))
        );
    }
}
