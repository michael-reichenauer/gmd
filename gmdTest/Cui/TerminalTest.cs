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

    // ── Copying ───────────────────────────────────────────────────────────────────────────────
    //
    // The one thing gmd does that leaves the application entirely, and the only tier that can
    // check it: what a clipboard holds afterwards is not on any screen and cannot be faked. The
    // session has no display and no clipboard tool it can reach (see TmuxSession), so the path
    // taken here is the last one in ClipboardService's chain — OSC 52, i.e. asking the terminal
    // itself — and tmux keeps what it is sent as a buffer, which is what is read back.
    //
    // That is also the path that matters most for gmd, since it is the only one that works over
    // ssh or from inside a container, where no local tool can reach the user's own clipboard.

    // Shift+Down selects rows, Ctrl+C copies them: the sid and subject of each selected commit
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

        Assert.AreEqual(
            """
            17d85b Add delta
            4e73d2 Merge branch 'dev' into main
            """,
            gmd.WaitForClipboard()
        );
    }

    // A selection within a single row copies that row as it is drawn, rather than the sid and
    // subject the multi row copy builds — so the graph column comes with it, and so does the '|'
    // that marks the row as selected, which stands where the '●' current commit marker is drawn
    // when it is not. Characterization: that is what gmd does today, not what it ought to do.
    [TestMethod]
    public async Task TestCopyOneSelectedRow()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("S-Down");
        gmd.WaitForStable();
        gmd.Send("C-c");

        Assert.AreEqual(
            "┣  | Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06",
            gmd.WaitForClipboard()
        );
    }

    // ── Committing ────────────────────────────────────────────────────────────────────────────
    //
    // The first flow here that changes the repository rather than only looking at it, which is
    // why every test below gets its own throwaway repo and why the session pins the commit dates:
    // without that the commit gmd makes is dated 'now', so its sid and its row in the time column
    // would differ every run. See TmuxSession.EnvironmentVariables.

    // The uncommitted row, which nothing in the suite drew until now: E2eRepo leaves the working
    // tree clean, so the Repo.UncommittedId sentinel commit, its '©2' change count in the
    // application bar and the current branch marker moving up onto it had no coverage at any tier.
    [TestMethod]
    public async Task TestUncommittedChangesAreShown()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);

        // Its time is DateTime.Now rather than a commit date, so it is the one thing on this
        // screen that has to be masked. The commit rows below it keep their pinned times.
        Assert.AreEqual(
            """
             Gmd {repo}, ●main, ©2                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ©2 uncommitted changes                                                (● main)                       NN-NN-NN NN:NN
            ┣  ● Add delta                                                              [v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            ScreenText.MaskTimes(ScreenText.Of(gmd.WaitFor("Initial"), repo.Path), "uncommitted")
        );
    }

    // 'c' commits, i.e. the dialog, the git command behind it and the refreshed log view. The one
    // keystroke in this suite that writes a commit.
    [TestMethod]
    public async Task TestCommitChanges()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");

        gmd.Send("c");
        var dialog = gmd.WaitFor("Commit 2 changes");

        // The dialog itself: the change count and the branch it would commit to in the title row,
        // the subject field, the message body below it and the buttons
        Assert.AreEqual(
            """
                                   ╭ Commit ────────────────────────────────────────────────────────────────╮
                                   │ Commit 2 changes on 'main':                                            │
                                   │                                                                        │
                                   │[                                                  ]                    │
                                   │┌──────────────────────────────────────────────────────────────────────┐│
                                   ││                                                                      ││
            """,
            ScreenText.Rows(dialog, repo.Path, 11, 6)
        );

        // The subject field has the focus, so the message is simply typed, and Enter presses the
        // default OK button
        gmd.SendText("Add epsilon");
        gmd.WaitFor("Add epsilon");
        gmd.Send("Enter");

        // The uncommitted row is gone, the new commit is at the top with the branch tip on it, and
        // its sid and time are the pinned ones — not masked, because the session pinned the dates
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add epsilon                                                          (● main) 2d0391 Test User      24-10-15 12:07
            ┣    Add delta                                                              [v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted changes"),
            repo.Path
        );

        // And the repository really changed, which the screen alone does not prove. Both files are
        // in the commit: gmd runs 'git add .' before 'git commit -am', and the add is what picks
        // up the untracked one.
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
        Assert.AreEqual("Add epsilon", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("alpha.txt\nepsilon.txt", await repo.GitAsync("show --name-only --format= HEAD"));
    }

    // The subject and the message body are two separate fields joined into one commit message, so
    // what git ends up storing is worth pinning: the blank line between them is what makes the
    // subject a subject.
    [TestMethod]
    public async Task TestCommitWithAMessageBody()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.SendText("Add epsilon");
        gmd.WaitFor("Add epsilon");
        gmd.Send("Tab"); // From the subject field into the message body
        gmd.WaitForStable();
        gmd.SendText("Some body text");
        gmd.WaitFor("Some body text");
        gmd.Send("Tab"); // And on to the OK button, since Enter in the body is a newline
        gmd.WaitForStable();
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("uncommitted changes"), "Add epsilon");
        Assert.AreEqual("Add epsilon\n\nSome body text", await repo.GitAsync("log --format=%B -1"));
    }

    // Ctrl-D in the commit dialog shows the diff of what is about to be committed, i.e. reviewing
    // the changes without losing the message already typed. It is also the only path to the
    // uncommitted diff, which is a different screen from a commit diff — it has no commit id or
    // author, and its 'Modified' half is drawn side by side.
    [TestMethod]
    public async Task TestCommitDialogShowsTheUncommittedDiff()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.Send("C-d");
        var screen = gmd.WaitFor("Added: epsilon.txt");

        // From the message row down: the row above it is the diff's own date, which is 'now'
        Assert.AreEqual(
            """
            Message: Uncommitted changes

            2 Files:
              Modified:    alpha.txt
              Added:       epsilon.txt

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Modified: alpha.txt

            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
               1 alpha                                                 │   1 alpha
            ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│   2┃changed
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

            ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Added: epsilon.txt

            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
               1┃epsilon
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            """,
            ScreenText.Rows(screen, repo.Path, 2, 20)
        );

        // Escape leaves the diff and lands back on the commit dialog rather than on the log view,
        // which is a modal over a modal over the log view
        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("Added: epsilon.txt"), "Commit 2 changes on 'main':");
    }

    // Escape cancels the dialog, and cancelling has to leave the repository alone. Note that the
    // same key one view further out quits gmd, so this also pins that the dialog swallows it.
    [TestMethod]
    public async Task TestCancelCommitLeavesTheRepoUnchanged()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.Send("Escape");

        StringAssert.Contains(gmd.WaitUntilGone("Commit 2 changes"), "©2 uncommitted changes");
        Assert.IsTrue(gmd.IsRunning, "Escape in the commit dialog should close it, not quit gmd");
        Assert.AreEqual(" M alpha.txt\n?? epsilon.txt", await repo.GitAsync("status -s"));
        Assert.AreEqual("Add delta", await repo.GitAsync("log --format=%s -1"), "Nothing should be committed");
    }

    // The dialog's one validation rule, which is the difference between a rejected commit and a
    // commit with an empty message
    [TestMethod]
    public async Task TestCommitWithAnEmptyMessageIsRejected()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("c");
        gmd.WaitFor("Commit 2 changes");

        gmd.Send("Enter");

        // Drawn inside the commit dialog, which is still open behind it
        var screen = gmd.WaitFor("Empty commit message");
        Assert.AreEqual(
            """
                                   ││                    ╭ Error ! ───────────────────╮                    ││
                                   ││                    │Empty commit message        │                    ││
                                   ││                    │                            │                    ││
                                   ││                    │          [◦ OK ◦]          │                    ││
                                   ││                    ╰────────────────────────────╯                    ││
            """,
            ScreenText.Rows(screen, repo.Path, 17, 5)
        );

        Assert.AreEqual("Add delta", await repo.GitAsync("log --format=%s -1"), "Nothing should be committed");
    }

    // ── Branching, tagging, switching and merging ──────────────────────────────────────────────
    //
    // The rest of the keys that change the repository. Three of them act on the *hoovered* branch
    // rather than on the current row, which is the part that only an end-to-end test reaches:
    // `Hoover` is unit tested as index math, but which branch a given key sequence ends up on, and
    // therefore what 's' switches to and what 'e' merges, is a property of the running app.

    // 'b' with no branch hoovered creates the branch at the current row's commit
    [TestMethod]
    public async Task TestCreateBranchFromACommit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("b");

        // Both check boxes are on by default: 'Checkout' is why HEAD moves below, and 'Publish' is
        // a push that fails silently here because the fixture has no origin — BranchCreateCommands
        // swallows exactly that error, and this is what pins that it still does
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00







                                                  ╭ Create Branch at Commit ─────────────────╮
                                                  │ From: main at 17d85b                     │
                                                  │                                          │
                                                  ││                                        ││
                                                  │└────────────────────────────────────────┘│
                                                  │ ◙ Checkout                               │
                                                  │ ◙ Publish                                │
                                                  │                                          │
                                                  │                                          │
                                                  │           [◦ OK ◦] [ Cancel ]            │
                                                  ╰──────────────────────────────────────────╯
            """,
            gmd.WaitFor("Create Branch at Commit"),
            repo.Path
        );

        gmd.SendText("feature");
        gmd.WaitFor("feature");
        gmd.Send("Enter");

        // The new branch is current and drawn as its own column, branching out of main
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●feature                                                    (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣─┺ ● Add delta                                            (main)(● feature)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("(● feature)"),
            repo.Path
        );

        Assert.AreEqual("feature", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("17d85ba889a1084f912c412d0ce435c9d7a36f53", await repo.GitAsync("rev-parse feature"));
    }

    // With a branch hoovered the same key creates from that branch instead, which is a different
    // command and a differently titled dialog. Cancelled, so it also pins that cancelling creates
    // nothing.
    [TestMethod]
    public async Task TestCreateBranchFromAHooveredBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Left"); // Hoovers main, the right most branch of the row
        gmd.WaitForStable();
        gmd.Send("b");
        var screen = gmd.WaitFor("Create Branch");

        Assert.AreEqual(
            """
                                                  ╭ Create Branch ───────────────────────────╮
                                                  │ From: main                               │
            """,
            ScreenText.Rows(screen, repo.Path, 14, 2)
        );

        gmd.Send("Escape");
        gmd.WaitUntilGone("Create Branch");
        Assert.AreEqual("  dev\n* main", await repo.GitAsync("branch"), "No branch should have been created");
    }

    [TestMethod]
    public async Task TestAddATag()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("t");
        var screen = gmd.WaitFor("Add Tag");
        Assert.AreEqual(
            """
                                          ╭ Add Tag ─────────────────────────────────────────────────╮
                                          │ Name:                                                    │
                                          ││                         │                               │
                                          │└─────────────────────────┘                               │
                                          │ Message:                                                 │
            """,
            ScreenText.Rows(screen, repo.Path, 13, 5)
        );

        gmd.SendText("v2.0");
        gmd.WaitFor("v2.0");
        gmd.Send("Enter");

        // The new tag is drawn next to the one the fixture already has, on the current row
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                (● main)[v1.0][v2.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("[v2.0]"),
            repo.Path
        );

        Assert.AreEqual("v1.0\nv2.0", await repo.GitAsync("tag --points-at HEAD"));
    }

    // 's' switches to the hoovered branch, with no confirmation of any kind — one keystroke from
    // changing the working tree, which is why it is worth an end-to-end test.
    [TestMethod]
    public async Task TestSwitchToBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // Show dev: down to the merge commit, left to hoover main, enter to open the branch that
        // was merged in there
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitFor("Merge branch");
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");

        // The hoover is left on main, i.e. on the branch that is already current, and 's' on that
        // is deliberately a no-op (OnKeyS's PrimaryName guard). Worth pinning: it is the reason
        // 'show a branch and press s' does nothing, which reads like a dropped keystroke.
        gmd.Send("s");
        gmd.WaitForStable();
        Assert.AreEqual("main", await repo.GitAsync("rev-parse --abbrev-ref HEAD"), "'s' on the current branch");

        // One step right is dev, and there it does switch
        gmd.Send("Right");
        gmd.WaitForStable();
        gmd.Send("s");

        // The current markers moved: '●dev' in the application bar, '●' on dev's tip commit and
        // '(● dev)' on its branch tip, while main keeps its plain '(main)'
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●dev                                                         (dev) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣     Add delta                                                       (main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│    Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊ ● More dev work                                                        (● dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("(● dev)"),
            repo.Path
        );

        Assert.AreEqual("dev", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
    }

    // 'e' merges the hoovered branch into the current one. It does not commit by itself: the merge
    // is left uncommitted and the commit dialog opens on top of it with the message filled in,
    // which is the one thing about this flow that cannot be guessed from the key table.
    [TestMethod]
    public async Task TestMergeBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.GitAsync("checkout -q dev"); // Merge main into dev, since dev is already in main
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");

        gmd.Send("Left"); // Hoovers main, the branch the cursor row is on
        gmd.WaitForStable();
        gmd.Send("e");

        // The merge is in the working tree, as the uncommitted row says, and the dialog offers the
        // message git would have used
        var screen = gmd.WaitFor("Commit 2 changes");
        Assert.AreEqual(
            """
             ╭╊  ©Merge branch 'main' into dev, 2 uncommitted changes                  (● dev)                       NN-NN-NN NN:NN
            """,
            ScreenText.MaskTimes(ScreenText.Rows(screen, repo.Path, 2, 1), "uncommitted")
        );
        StringAssert.Contains(screen, "[Merge branch 'main' into dev");

        gmd.Send("Enter");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●dev                                                         (dev) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭╊ ● Merge branch 'main' into dev                                         (● dev) 60e4d8 Test User      24-10-15 12:07
            ┣╯┃   Add delta                                                       (main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮┃   Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│┃   Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊   More dev work                                                                af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted changes"),
            repo.Path
        );

        // A real merge commit, i.e. two parents: dev's tip and main's tip
        Assert.AreEqual("dev", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("af3ee69 17d85ba", await repo.GitAsync("log --format=%p -1"));
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
    }

    // The other arm of the same key: with the *current* branch hoovered there is nothing to merge
    // into, so it offers the branches to merge from instead
    [TestMethod]
    public async Task TestMergeFromMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitFor("Merge branch");
        gmd.Send("Enter"); // Shows dev, and leaves the hoover on main, which is current
        gmd.WaitFor("More dev work");

        gmd.Send("e");

        // Only the shown branches are offered, so the menu lists dev and nothing else. The stray
        // 'k' is the tail of 'More dev work' behind the menu, which is drawn over the log view.
        Assert.AreEqual(
            """
            ┣│ ╭ Merge from ─╮                                                                 4a15fb Test User      24-10-15 12:04
            ┃╰╊│ o  dev      │k                                                          (dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺╰─────────────╯                                                                 d997ad Test User      24-10-15 12:02
            """,
            ScreenText.Rows(gmd.WaitFor("Merge from"), repo.Path, 4, 3)
        );
    }

    // 'a' amends the last commit, but only while it is still ahead of the remote, i.e. not yet
    // published. That guard is why this is the one flow here that needs a fixture with an origin,
    // and the fixture is also the only place in this suite where the ahead marker, the '(^/main)'
    // remote branch tip and the local branch drawn beside its remote reach a snapshot at all.
    [TestMethod]
    public async Task TestAmendTheLastCommit()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(8));

        // '▲1' in the application bar and '▲' on the commit are the one commit not yet pushed,
        // and '(^/main)' is origin/main, still on the commit below it
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▲1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Add zeta                                                            (● main) 4dd1e9 Test User      24-10-15 12:07
            ┣╯    Add delta                                                     (^/main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("Initial"),
            repo.Path
        );

        gmd.Send("a");
        var dialog = gmd.WaitFor("Amend 0 changes");

        // The dialog is the commit one retitled, with the message of the commit being amended
        // filled in and the cursor at its end. '0 changes' because the working tree is clean here:
        // amending only the message is a normal thing to do.
        Assert.AreEqual(
            """
                                   ╭ Amend ─────────────────────────────────────────────────────────────────╮
                                   │ Amend 0 changes on 'main':                                             │
                                   │                                                                        │
                                   │[Add zeta                                          ]                    │
            """,
            ScreenText.Rows(dialog, repo.Path, 11, 4)
        );

        gmd.SendText(" amended");
        gmd.WaitFor("Add zeta amended");
        gmd.Send("Enter");

        // Same row, same position, new message and a new id — and the time column does not move,
        // since amending keeps the author date and only the committer date is rewritten
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▲1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Add zeta amended                                                    (● main) 9df2d6 Test User      24-10-15 12:07
            ┣╯    Add delta                                                     (^/main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("Add zeta amended"),
            repo.Path
        );

        // One commit was rewritten rather than added, and it is still the one commit that is ahead
        Assert.AreEqual("Add zeta amended", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("Add delta", await repo.GitAsync("log --format=%s -1 HEAD~1"));
        Assert.AreEqual(
            "2024-10-15 12:07:00 +0000|2024-10-15 12:08:00 +0000",
            await repo.GitAsync("log --format=%ad|%cd --date=iso -1"),
            "Amending should keep the author date and move only the committer date"
        );
        StringAssert.Contains(await repo.GitAsync("status -sb"), "[ahead 1]");
    }

    // The guard that makes amend safe: once the commit is on the remote it is not offered at all,
    // and the key silently does nothing rather than rewriting published history.
    [TestMethod]
    public async Task TestAmendIsRefusedForAPushedCommit()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("push -q origin main"); // Nothing is ahead any more
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(8));
        gmd.WaitFor("Initial");

        gmd.Send("a");

        // No dialog opens. The '▲' ahead markers are gone now that everything is pushed, and the
        // remote is drawn as its own '(^)' tip beside the local one on the same commit.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣─┺ ● Add zeta                                                         (^)(● main) 4dd1e9 Test User      24-10-15 12:07
            ┣     Add delta                                                             [v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitForStable(),
            repo.Path
        );

        Assert.AreEqual("Add zeta", await repo.GitAsync("log --format=%s -1"), "Nothing should be rewritten");
    }
}
