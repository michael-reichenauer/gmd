using gmdTest.Fixtures;

namespace gmdTest.Cui;

// End-to-end tests: the built gmd binary, real git, a real pty. tmux keeps a screen model, so
// what is asserted is the rendered screen — the drawing, the layout, the key dispatch and the
// dialogs, none of which any other test in this suite reaches.
//
// They name no Terminal.Gui type, deliberately, so they are as valid against a 2.x build as a
// 1.x one, which is what makes them the acceptance suite for that port. They are
// characterization tests: they capture what gmd draws today, not what it ought to draw.
//
// Two rules for anything added here, both learned the hard way:
//   - Never send a key into a screen that has not settled. gmd drops keystrokes while a git
//     command is running rather than queueing them, so a key sent too early is silently lost.
//     Every Send is therefore preceded by a WaitFor.
//   - Escape in the log view quits the application. Use WaitUntilGone to close a dialog and
//     check it really closed, rather than sending a second Escape for safety.
//
// They need tmux, which ./installtools installs, and are in their own categories:
//   ./test --filter "TestCategory!=Integration"   excludes these and the real git tests
//   ./test --filter "TestCategory=E2e"            runs only these
[TestClass]
[TestCategory("Integration")]
[TestCategory("E2e")]
public class TerminalTest
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

    // The guard on every other test here: that the redirected HOME actually took, i.e. that gmd
    // wrote its state into the throwaway home rather than into the developer's. If another write
    // anchored on SpecialFolder.UserProfile is ever added, this is where it shows up.
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
            ┣╮   Mer╭ Commit: 17d85b ───────────────────────╮                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add│Commit ...                          C  │                                  4a15fb Test User      24-10-15 12:04
            ┣╯   Add│Amend ...                           A  │                                  dd7891 Test User      24-10-15 12:01
            ┗    Ini│Commit Diff ...                     D  │                                  9dc406 Test User      24-10-15 12:00
                    │Undo                                  >│
                    │Rebase                                >│
                    │Stash                                 >│
                    │Tag                                   >│
                    │Create Branch from Commit ...       B  │
                    │Merge From Commit to main              │
                    │Cherry Pick Commit to main             │
                    │Switch/Checkout to Commit              │
                    │───────────────────────────────────────│
                    │Show/Open Branch              Shift → >│
                    │Hide All Branches                      │
                    │Toggle Commit Details ...       Enter  │
                    │File History ...                       │
                    │Repo Menu                             >│
                    ╰───────────────────────────────────────╯
            """,
            gmd.WaitFor("Commit ..."),
            repo.Path
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
        Assert.AreEqual(
            """
            ┣╯   Add beta       ╭ Help ────────────────────────────────────────────────────────────────────────╮     24-10-15 12:01
            ┗    Initial        │# Gmd Help Guide                                                             ┃│     24-10-15 12:00
                                │                                                                             ┃│
                                │### Keyboard Shortcuts                                                       ┃│
                                │                                                                             ┃│
                                │Here are some essential keyboard shortcuts:                                  ┃│
            """,
            ScreenText.Rows(screen, repo.Path, 5, 6)
        );

        // The dialog closes and the log view is still there behind it
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Gmd Help Guide"), "Add delta");
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

    // Interactive branch visibility is the feature this application exists for, and it had no
    // end-to-end coverage at all. Asserted as a round trip rather than two independent
    // snapshots: after showing and hiding again the screen has to be what it started as.
    [TestMethod]
    public async Task TestShowAndHideBranchRoundTrip()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        var before = gmd.WaitFor("Initial");

        // Down to the merge commit, left to hoover the branch it merged in, enter to show it
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitFor("Merge branch");
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

    [TestMethod]
    public async Task TestFilterCommits()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        gmd.SendText("dev");

        ScreenText.AssertEqual(
            """
            Filter Commits ────────────────────────────────────────────────────────────────────────────────────────────────────────╮
            Gmd 3 commits, 2 branches, 4e73d2 (main)                                      Search: dev                          ] X │
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────╯
             ╰╊   More dev work                                                          (dev) af3ee6 Test User      24-10-15 12:03
              ┗   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            """,
            gmd.WaitFor("More dev work"),
            repo.Path
        );
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
}
