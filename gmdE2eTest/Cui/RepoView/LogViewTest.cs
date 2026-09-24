using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// The log view itself: the screen gmd starts on and how it narrows, its colors and the current
// row, the keys that quit, scroll and open the details pane, the commit menu, the help page, and
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
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );
    }

    // The guard on every other end-to-end test: that the redirected HOME actually took, i.e. that
    // gmd wrote its state into the throwaway home rather than into the developer's. If another
    // write anchored on SpecialFolder.UserProfile is ever added, this is where it shows up.
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

    // Escape quits from the log view, which is why nothing here ever sends a 'safety' Escape
    [TestMethod]
    public async Task TestQuitWithEscape()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Escape");

        gmd.WaitForExit();
        Assert.IsFalse(gmd.IsRunning);
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
    // A whole arm of that calculation, with no other coverage.
    [TestMethod]
    public async Task TestNarrowWidthDropsTheSidAuthorAndTimeColumns()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: 70, height: 20);

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main     (main) [Ϙ Search] ? X
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
             Gmd {repo}, ●main                              (main) [Ϙ Search] ? X
            ───────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                               (● main)[v1.0] Test User 24-10-15
            ┣╮   Merge branch 'dev' into main                                           Test User 24-10-15
            ┣    Add gamma                                                              Test User 24-10-15
            ┣╯   Add beta                                                               Test User 24-10-15
            ┗    Initial                                                                Test User 24-10-15
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
             Gmd {repo}, ●main                                               (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                         (● main)[v1.0] 17d85b Test User 24-10-15
            ┣╮   Merge branch 'dev' into main                                                     4e73d2 Test User 24-10-15
            ┣    Add gamma                                                                        4a15fb Test User 24-10-15
            ┣╯   Add beta                                                                         dd7891 Test User 24-10-15
            ┗    Initial                                                                          9dc406 Test User 24-10-15
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
        // the current branch magenta, '[Ϙ Search]' dark, ' ? ' bright cyan. Row 1 is its border.
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
             mmm DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD WMMMM                                                       MMMMMM DD DDDDDDD c W
            mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm
            M  W WWW WWWWW                                                      MW MMMMMGGGGGG CCCCCC WWWW WWWW      WWWWWWWW WWWWW
            MD   WWWWW WWWWWW WWWWW WWWW WWWW                                                  CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            M    WWW WWWWW                                                                     CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            MD   WWW WWWW                                                                      CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            M    WWWWWWW                                                                       CCCCCC DDDD DDDD      DDDDDDDD DDDDD
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
            M   W DDD DDDDD                                                     MW MMMMMGGGGGG CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            MG    DDDDD DDDDDD DDDDD DDDD DDDD                                                 CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            MG    DDD DDDDD                                                                    CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            MGG   WWWW WWW WWWW                                                          GGGGG CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            MGG   WWWW WW WWW                                                                  CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            MG    DDD DDDD                                                                     CCCCCC DDDD DDDD      DDDDDDDD DDDDD
            M     DDDDDDD                                                                      CCCCCC DDDD DDDD      DDDDDDDD DDDDD
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
        // highlight. It starts after the graph, which keeps its own background — that is
        // RepoWriter applying Highlight() to the non-graph part of the row only, a detail that is
        // easy to break and invisible to every other assertion here. The rows below it are plain.
        Assert.AreEqual(
            """
            -  . DDD DDDDD                                                      DD DDDDDDDDDDD DDDDDD DDDD DDDD      DDDDDDDD DDDDD
            --   ..... ...... ..... .... ....                                                  ...... .... ....      ........ .....
            -    ... .....                                                                     ...... .... ....      ........ .....
            --   ... ....                                                                      ...... .... ....      ........ .....
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
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣ ● Commit number 29                                                      (● main) a579ec Test User      24-10-15 12:29┃
            ┣   Commit number 28                                                               a20764 Test User      24-10-15 12:28┃
            ┣   Commit number 27                                                               9c99a7 Test User      24-10-15 12:27┃
            ┣   Commit number 26                                                               f1183f Test User      24-10-15 12:26┃
            ┣   Commit number 25                                                               d5e69c Test User      24-10-15 12:25┃
            ┣   Commit number 24                                                               ff0009 Test User      24-10-15 12:24
            ┣   Commit number 23                                                               78ab6f Test User      24-10-15 12:23
            ┣   Commit number 22                                                               9c943a Test User      24-10-15 12:22
            ┣   Commit number 21                                                               ad2ca1 Test User      24-10-15 12:21
            ┣   Commit number 20                                                               bc3421 Test User      24-10-15 12:20
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
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   Commit number 20                                                               bc3421 Test User      24-10-15 12:20
            ┣   Commit number 19                                                               7f6574 Test User      24-10-15 12:19
            ┣   Commit number 18                                                               ba1e69 Test User      24-10-15 12:18
            ┣   Commit number 17                                                               02f871 Test User      24-10-15 12:17┃
            ┣   Commit number 16                                                               6e07b5 Test User      24-10-15 12:16┃
            ┣   Commit number 15                                                               8d0818 Test User      24-10-15 12:15┃
            ┣   Commit number 14                                                               b020d9 Test User      24-10-15 12:14┃
            ┣   Commit number 13                                                               a91a58 Test User      24-10-15 12:13┃
            ┣   Commit number 12                                                               3b1172 Test User      24-10-15 12:12
            ┣   Commit number 11                                                               0399cf Test User      24-10-15 12:11
            """,
            gmd.WaitUntilGone("Commit number 29"),
            repo.Path
        );

        // End goes to the last commit, and the scrollbar to the bottom
        gmd.Send("End");
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   Commit number 09                                                               36b6fd Test User      24-10-15 12:09
            ┣   Commit number 08                                                               f2a4f4 Test User      24-10-15 12:08
            ┣   Commit number 07                                                               c78293 Test User      24-10-15 12:07
            ┣   Commit number 06                                                               5bad1c Test User      24-10-15 12:06
            ┣   Commit number 05                                                               f15dfc Test User      24-10-15 12:05
            ┣   Commit number 04                                                               7e31dc Test User      24-10-15 12:04┃
            ┣   Commit number 03                                                               c02add Test User      24-10-15 12:03┃
            ┣   Commit number 02                                                               8332dd Test User      24-10-15 12:02┃
            ┣   Commit number 01                                                               5692a8 Test User      24-10-15 12:01┃
            ┗   Commit number 00                                                               a823b7 Test User      24-10-15 12:00┃
            """,
            gmd.WaitFor("Commit number 00"),
            repo.Path
        );
    }

    // Enter opens the details pane, which is anchored to the bottom of the screen, so only its
    // rows are asserted rather than the 22 blank ones above it
    [TestMethod]
    public async Task TestCommitDetailsPane()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Enter");
        var screen = gmd.WaitFor("Id:");

        Assert.AreEqual(
            """
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            Id:         17d85ba889a1084f912c412d0ce435c9d7a36f53  ({repo})
            Branch:     main  (main)
            Author:     Test User, time: 2024-10-15 12:06:00 +00:00
            Children:
            Parents:    4e73d2
            Tags:       [v1.0]
            Tips:       (main)
            Add delta
            """,
            ScreenText.Rows(screen, repo.Path, 29, 9)
        );
    }

    [TestMethod]
    public async Task TestCommitMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Mer╭ Commit: 17d85b ─────────────────────╮                                    4e73d2 Test User      24-10-15 12:05
            ┣    Add│Commit ...                        C  │                                    4a15fb Test User      24-10-15 12:04
            ┣╯   Add│Amend ...                         A  │                                    dd7891 Test User      24-10-15 12:01
            ┗    Ini│Commit Diff ...                   D  │                                    9dc406 Test User      24-10-15 12:00
                    │Undo                                >│
                    │Rebase                              >│
                    │Stash                               >│
                    │Tag                                 >│
                    │Create Branch from Commit ...     B  │
                    │Merge From Commit to main            │
                    │Cherry Pick Commit to main           │
                    │Switch/Checkout to Commit            │
                    │Toggle Commit Details ...     Enter  │
                    │Full File History ...                │
                    │Blame File ...                       │
                    │─────────────────────────────────────│
                    │Branches                            >│
                    │Repo Menu                           >│
                    ╰─────────────────────────────────────╯
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
        // that change which branches are shown at all, and the ones that pull and push them all.
        gmd.Send("Right");
        var branches = gmd.WaitForStable();
        Assert.AreEqual(
            """
                     │Full File History ...                │
                     │Blame File ...                       │
                     │─────────────────────────────────────│╭ Branches ────────────────────────╮
                     │Branches                            >││●   main                         >│
                     │Repo Menu                           >││    dev                          >│
                     ╰─────────────────────────────────────╯│──────────────────────────────────│
                                                            │Show/Open Branch         Shift → >│
                                                            │Hide All Branches                 │
                                                            │Pull/Update All Branches Shift-U  │
                                                            │Push All Branches        Shift-P  │
                                                            ╰──────────────────────────────────╯
            """,
            ScreenText.Rows(branches, repo.Path, 19, 11)
        );

        // Down to dev and into it: the child window is titled with the branch, and its items are
        // the branch menu, built with isLimited so it has no 'Show/Open Branch', 'Pull/Update All
        // Branches', 'Push All Branches' or 'Repo Menu' of its own, since the menus it is under
        // already offer those. The Branches menu is too wide to leave room for it on the right in
        // 120 columns, so it opens on the left, over the commit menu.
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        Assert.AreEqual(
            """
                     │Full File History ...                │
                     │Blame File ...                       │
                     │─────────────────────────────────────│╭ Branches ────────────────────────╮
                  ╭ dev ───────────────────────────────────╮│●   main                         >│
                  │Switch/Checkout to Branch            S  ││    dev                          >│
                  │Merge to main                        E  ││──────────────────────────────────│
                  │Merge from main                Shift-E  ││Show/Open Branch         Shift → >│
                  │Rebase and push on                     >││Hide All Branches                 │
                  │Hide Branch                          H  ││Pull/Update All Branches Shift-U  │
                  │Pull/Update                          U  ││Push All Branches        Shift-P  │
                  │Push                                 P  │╰──────────────────────────────────╯
                  │Create Branch ...                    B  │
                  │Create Worktree ...                     │
                  │Rename Branch ...                       │
                  │Delete Branch ...                       │
                  │Diff Branch to                       D >│
                  │Change Branch Color                  G  │
                  │────────────────────────────────────────│
                  │Set Commit Branch Manually ...          │
                  ╰────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Switch/Checkout to Branch"), repo.Path, 19, 20)
        );
    }

    // The help page is the most deterministic screen in the app: static text embedded in the
    // binary, no git and no clock. Only the top of it is asserted, since the rest belongs to
    // gmd/doc/help.md and editing the docs should not fail a UI test.
    [TestMethod]
    public async Task TestHelpDialog()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("?");
        var screen = gmd.WaitFor("Gmd Help Guide");

        // Note the '┃' at the right: that is the scroll bar, and its length is worked out from how
        // long the document is — so this snapshot moves whenever gmd/doc/help.md grows or shrinks,
        // even though nothing near the top of it changed.
        Assert.AreEqual(
            """
            ┣╯   Add beta       ╭ Help ────────────────────────────────────────────────────────────────────────╮     24-10-15 12:01
            ┗    Initial        │# Gmd Help Guide                                                             ┃│     24-10-15 12:00
                                │                                                                             ┃│
                                │## Keyboard Shortcuts                                                        ┃│
                                │                                                                              │
                                │The most used keys of the log view. The menus show the key of every command   │
            """,
            ScreenText.Rows(screen, repo.Path, 5, 6)
        );

        // The dialog closes and the log view is still there behind it
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Gmd Help Guide"), "Add delta");
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
             Gmd {repo}, ●main                                                        (dev) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│    Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊   More dev work                                                          (dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("More dev work"),
            repo.Path
        );

        // And hiding it again gives back exactly the screen we started with
        gmd.Send("h");
        ScreenText.AssertEqual(ScreenText.Of(before, repo.Path), gmd.WaitUntilGone("More dev work"), repo.Path);
    }
}
