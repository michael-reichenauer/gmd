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
        // that change which branches are shown at all.
        gmd.Send("Right");
        var branches = gmd.WaitForStable();
        Assert.AreEqual(
            """
                     │Full File History ...                │
                     │Blame File ...                       │
                     │─────────────────────────────────────│╭ Branches ─────────────────╮
                     │Branches                            >││●   main                  >│
                     │Repo Menu                           >││    dev                   >│
                     ╰─────────────────────────────────────╯│───────────────────────────│
                                                            │Show/Open Branch  Shift → >│
                                                            │Hide All Branches          │
                                                            ╰───────────────────────────╯
            """,
            ScreenText.Rows(branches, repo.Path, 19, 9)
        );

        // Down to dev and into it: the child window is titled with the branch, and its items are
        // the branch menu, built with isLimited so it has no 'Show/Open Branch' or 'Repo Menu' of
        // its own to recurse into.
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        Assert.AreEqual(
            """
                     │Full File History ...                │
                     │Blame File ...                       │
                     │─────────────────────────────────────│╭ Branches ─────────────────╮
                     │Branches                            >││●   main                  >│╭ dev ───────────────────────────────────╮
                     │Repo Menu                           >││    dev                   >││Switch/Checkout to Branch            S  │
                     ╰─────────────────────────────────────╯│───────────────────────────││Merge to main                        E  │
                                                            │Show/Open Branch  Shift → >││Merge from main                Shift-E  │
                                                            │Hide All Branches          ││Rebase and push on                     >│
                                                            ╰───────────────────────────╯│Hide Branch                          H  │
                                                                                         │Pull/Update                          U  │
                                                                                         │Push                                 P  │
                                                                                         │Create Branch ...                    B  │
                                                                                         │Rename Branch ...                       │
                                                                                         │Delete Branch ...                       │
                                                                                         │Diff Branch to                       D >│
                                                                                         │Change Branch Color                  G  │
                                                                                         │────────────────────────────────────────│
                                                                                         │Pull/Update All Branches       Shift-U  │
                                                                                         │Push All Branches              Shift-P  │
                                                                                         │Set Commit Branch Manually ...          │
                                                                                         ╰────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Switch/Checkout to Branch"), repo.Path, 19, 21)
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
                                │### Keyboard Shortcuts                                                       ┃│
                                │                                                                             ┃│
                                │Here are some essential keyboard shortcuts:                                   │
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
                                                  │Undo/Restore Uncommitted                U >│
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
            ┌ 65480e Test User   24-10-15 │   1┃one
            └                             │   2┃two
            ┌ 7e09a8 Test User   24-10-15 │   3┃CHANGED
            └                             │   4┃FOUR
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

    // 'Blame File ...' is the second last item of the commit menu, so 'End' and two 'Up' is the
    // steadier walk to it than counting downwards past the items OnCursorDown skips. One key per
    // Send with a wait after each, since a menu redraw drops whatever was sent behind it.
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
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
             ╰╊   More dev work                                                          (dev) af3ee6 Test User      24-10-15 12:03
              ┗   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            """,
            gmd.WaitFor("More dev work"),
            repo.Path
        );
    }

    // A filter matching nothing says so in a row of its own rather than emptying the list.
    // Regression test for two bugs that hid it: ViewRepoCreater built that row all along and
    // discarded it (ViewRepoCreater.cs:73), and the dialog was drawn over the log view's first
    // row, so even once it was returned it was covered. Note the counts still read 0 — the row
    // is on the virtual '<none>' branch, which the dialog counts as neither commit nor branch.
    [TestMethod]
    public async Task TestFilterWithNoMatchesSaysSo()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        gmd.SendText("zzzznothing");

        // The row is virtual, so its time is DateTime.Now rather than a commit date and has to be
        // masked, exactly as the uncommitted row's is
        Assert.AreEqual(
            """
            Filter Commits ────────────────────────────────────────────────────────────────────────────────────────────────────────╮
            Gmd 0 commits, 0 branches, ffffff (<none>)                                    Search: zzzznothing                  ] X │
            ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────╯
            ┏   <... No commits matching filter ...>                                 (~<none>) ffffff                NN-NN-NN NN:NN
            """,
            ScreenText.MaskTimes(
                ScreenText.Of(gmd.WaitFor("No commits matching filter"), repo.Path),
                "No commits matching"
            )
        );
    }

    // Stashing, i.e. the menu, the dialog behind it and what the log view says afterwards. The
    // 'ß' is drawn nowhere else, so this is the only cover WriteBlankOrStash has at any tier.
    //
    // Four moves down to 'Stash' rather than five: 'Amend ...' is disabled without a remote to be
    // ahead of, and OnCursorDown skips it. With a clean tree it is three, since 'Stash Changes'
    // being disabled changes what is enabled above as well — see TestStashPopBringsTheChangesBack.
    [TestMethod]
    public async Task TestStashPutsTheChangesAsideAndMarksTheCommit()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("uncommitted");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (int i = 0; i < 4; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Stash Changes");
        gmd.Send("Enter");
        gmd.WaitFor("Stash Message");
        gmd.SendText("stashed work");
        gmd.Send("Enter");

        // The uncommitted row is gone, the tree is clean, and the commit it was stashed on carries
        // the 'ß'. The application bar counts it too, where the change count used to be.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ß1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣ ß● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted"),
            repo.Path
        );

        Assert.AreEqual("stash@{0}: On main: stashed work", await repo.GitAsync("stash list"));
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"), "The working tree is clean again");
    }

    // And back again. Three moves rather than four, since a clean tree disables 'Stash Changes',
    // which is also why 'Stash Pop' is where the cursor lands when the sub menu opens.
    [TestMethod]
    public async Task TestStashPopBringsTheChangesBack()
    {
        using var repo = await E2eRepo.CreateWithStashAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (int i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Stash Pop");
        gmd.Send("Right");
        gmd.WaitFor("stashed work");
        gmd.Send("Enter");

        gmd.WaitFor("uncommitted");
        Assert.AreEqual("", await repo.GitAsync("stash list"), "The stash is gone once it is popped");
        Assert.AreEqual(
            """
             M alpha.txt
            ?? epsilon.txt
            """,
            await repo.GitAsync("status --porcelain"),
            "Both the modified file and the untracked one come back"
        );
    }

    // Deleting a branch, which is one item below the rename above and so one move further down
    [TestMethod]
    public async Task TestDeleteBranch()
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
        gmd.Send("Right");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Branch: dev");
        for (var i = 0; i < 7; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        gmd.WaitFor("Delete Branch");
        gmd.Send("Enter");

        gmd.WaitUntilGone("Delete Branch");
        StringAssert.DoesNotMatch(
            await repo.GitAsync("branch --list"),
            new System.Text.RegularExpressions.Regex(@"\bdev\b"),
            "The branch is gone from git"
        );
    }

    // Uncommitting the last commit, i.e. 'git reset HEAD~1', which puts its changes back into the
    // working tree. Reached through the commit menu's Undo sub menu.
    //
    // On a clean tree the menu opens with the cursor already on 'Commit Diff ...' — 'Commit ...'
    // and 'Amend ...' are both disabled, and Menu.Show starts on the first item that is not — so
    // 'Undo' is one move away rather than three.
    [TestMethod]
    public async Task TestUncommitTheLastCommit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Right");
        gmd.WaitFor("Uncommit");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        // The commit is gone and what it added is back in the working tree as an untracked file
        gmd.WaitFor("uncommitted");
        Assert.AreEqual("Merge branch 'dev' into main", await repo.GitAsync("log --format=%s -1"));
        Assert.AreEqual("?? delta.txt", await repo.GitAsync("status --porcelain"));
    }

    // Pushing the current branch with 'p'. The fixture has one commit that is not on the remote,
    // so before the push the local and remote branches are on different commits and each names
    // itself — '(^/main)' on the remote's tip and '(● main)' on the local one — and the commit
    // between them carries the bright green '▲'. Afterwards they are back on the same commit and
    // are drawn as the one combined '(^)(● main)' tip.
    //
    // This is the only test here that pushes, so it is also the only cover the ahead markers and
    // the split branch tips have at this tier.
    [TestMethod]
    public async Task TestPushTheCurrentBranch()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add zeta");

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
            gmd.WaitForStable(),
            repo.Path
        );

        gmd.Send("p");

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
            gmd.WaitUntilGone("▲"),
            repo.Path
        );

        Assert.AreEqual(
            await repo.GitAsync("rev-parse main"),
            await repo.GitAsync("rev-parse origin/main"),
            "The remote branch is on the commit the local one is"
        );

        // v1.0 is still there, and still unpushed: pushing a branch does not push its tags,
        // and the fetch that follows no longer prunes the ones the remote has not got
        Assert.AreEqual("v1.0", await repo.GitAsync("tag --list"));
    }

    // Pulling with 'u', the mirror of the push above: origin has a commit the local branch has
    // not got, so it is drawn bright blue with the '▼' behind marker until it is pulled in.
    [TestMethod]
    public async Task TestPullTheCurrentBranch()
    {
        using var repo = await E2eRepo.CreateBehindOriginAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add zeta");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▼1                                                   (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣    ▼Add zeta                                                            (^/main) 4dd1e9 Test User      24-10-15 12:07
            ┣─┺ ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣     Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitForStable(),
            repo.Path
        );

        gmd.Send("u");

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
            gmd.WaitUntilGone("▼"),
            repo.Path
        );

        Assert.AreEqual(
            await repo.GitAsync("rev-parse origin/main"),
            await repo.GitAsync("rev-parse main"),
            "The local branch has caught up with the remote"
        );
    }

    // 'Shift-U' updates every branch it can and says which it could not. A branch that is not
    // current is updated with a fetch, and git rejects that for a diverged branch, which used to
    // abort the whole command: every branch after it in the list went unpulled, with an error box
    // as the only sign. Here 'main' is the diverged one and 'dev' the plain fast-forward.
    [TestMethod]
    public async Task TestPullAllBranchesSkipsTheDivergedBranch()
    {
        using var repo = await E2eRepo.CreateWithDivergedMainAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Main local");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●work, ▼2, ▲1                                               (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺      ▲Main local                                                                   (main) 801397 Test User 24-10-15
            ┣│       ▼Main remote                                                                (^/main) 292075 Test User 24-10-15
            ┃│  ┣    ▼Work remote                                                                (^/work) ff355b Test User 24-10-15
            ┃│ ╭┺─┺ ● Work on work                                                               (● work) 1d4a58 Test User 24-10-15
            ┣┴─╯      Add zeta                                                                            4dd1e9 Test User 24-10-15
            ┣         Add delta                                                                    [v1.0] 17d85b Test User 24-10-15
            ┣╮        Merge branch 'dev' into main                                                        4e73d2 Test User 24-10-15
            ┣         Add gamma                                                                           4a15fb Test User 24-10-15
            ┣╯        Add beta                                                                            dd7891 Test User 24-10-15
            ┗         Initial                                                                             9dc406 Test User 24-10-15
            """,
            gmd.WaitForStable(),
            repo.Path
        );

        gmd.Send("U");

        // The diverged branch is named rather than passed over: it keeps its '▼' marker, so
        // silence would look exactly like the pull having failed
        var message = gmd.WaitFor("Pull/Update All Branches");
        Assert.AreEqual(
            """
                                  ╭ Pull/Update All Branches ───────────────────────────────────────────────╮
                                  │These branches have both local and remote commits, which an update of all│
                                  │branches cannot merge, since it only fast-forwards a branch it is not on.│
                                  │Switch to the branch and pull it to merge:                               │
                                  │                                                                         │
                                  │  main                                                                   │
                                  │                                                                         │
                                  │                                [◦ OK ◦]                                 │
                                  ╰─────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(message, repo.Path, 15, 9)
        );

        gmd.Send("Enter");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●work, ▼1, ▲1                                               (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺      ▲Main local                                                                   (main) 801397 Test User 24-10-15
            ┣│       ▼Main remote                                                                (^/main) 292075 Test User 24-10-15
            ┃│  ┣─┺ ● Work remote                                                             (^)(● work) ff355b Test User 24-10-15
            ┃│ ╭┺     Work on work                                                                        1d4a58 Test User 24-10-15
            ┣┴─╯      Add zeta                                                                            4dd1e9 Test User 24-10-15
            ┣         Add delta                                                                    [v1.0] 17d85b Test User 24-10-15
            ┣╮        Merge branch 'dev' into main                                                        4e73d2 Test User 24-10-15
            ┣         Add gamma                                                                           4a15fb Test User 24-10-15
            ┣╯        Add beta                                                                            dd7891 Test User 24-10-15
            ┗         Initial                                                                             9dc406 Test User 24-10-15
            """,
            gmd.WaitUntilGone("Pull/Update All Branches"),
            repo.Path
        );

        Assert.AreEqual(
            await repo.GitAsync("rev-parse origin/work"),
            await repo.GitAsync("rev-parse work"),
            "The behind branch was pulled"
        );
        Assert.AreNotEqual(
            await repo.GitAsync("rev-parse origin/main"),
            await repo.GitAsync("rev-parse main"),
            "The diverged branch was left as it was"
        );
    }

    // Cherry-picking a commit from another branch onto the current one. It runs with --no-commit
    // and hands what it staged to the commit dialog, which is why the dialog opens with the picked
    // commit's message already in it.
    //
    // 'Up' first: the cursor opens on the current branch's tip, and cherry-pick is only offered for
    // a commit that is not on the current branch (rb != cb). One move up is 'Add gamma' on main,
    // and it is a move that lands there whether the cursor started on row 0 or row 1.
    //
    // Then seven moves down to it. The menu opens on 'Commit Diff ...' — with nothing to commit,
    // 'Commit ...' and 'Amend ...' are both disabled and Menu.Show starts on the first that is not.
    [TestMethod]
    public async Task TestCherryPickACommitFromAnotherBranch()
    {
        using var repo = await E2eRepo.CreateWithUnmergedBranchAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(3));
        gmd.WaitFor("Work on dev");

        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("m");
        gmd.WaitFor("Cherry Pick Commit to dev");
        for (var i = 0; i < 7; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        // The dialog arrives filled in with the message of the commit being picked
        gmd.WaitFor("Add gamma, 1 uncommitted changes");
        gmd.Send("Enter");

        // 'dev' now has its own copy of the commit, with an id of its own, and main still has the
        // original — the same subject on two branches is what a cherry-pick looks like
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●dev                                                         (dev) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
              ┣ ● Add gamma                                                            (● dev) b03776 Test User      24-10-15 12:03
            ┣ ┃   Add gamma                                                             (main) de2e9a Test User      24-10-15 12:02
            ┃╭┺   Work on dev                                                                  ee3602 Test User      24-10-15 12:01
            ┗╯    Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitUntilGone("uncommitted"),
            repo.Path
        );

        Assert.AreEqual(
            """
            Add gamma
            Work on dev
            Initial
            """,
            await repo.GitAsync("log --format=%s")
        );
    }

    // Squashing a range of commits into one. The range is a shift-selection of two rows, which is
    // what puts the ids into the menu item's own text and is what enables it at all.
    [TestMethod]
    public async Task TestSquashTwoCommitsIntoOne()
    {
        using var repo = await E2eRepo.CreatePushedPlainCommitsAsync(4);
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(4));
        gmd.WaitFor("Commit number 00");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Commit Diff ...");
        for (var i = 0; i < 2; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");

        // The item names the range it would squash, which is how it says a selection was picked up
        gmd.WaitFor("Squash c02add...8332dd");
        gmd.Send("Enter");

        gmd.WaitFor("Squash c02add...8332dd on 'main'");
        gmd.Send("Enter");

        // The two are now one, and the branch has diverged from its remote: one commit ahead of
        // origin and two behind it, since the originals are still the remote's. Which is the
        // clearest possible illustration of why squashing pushed commits is the wrong way round.
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main, ▼2, ▲1                                               (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭┺ ●▲Commit number 02                                                    (● main) 96a909 Test User      24-10-15 12:04
            ┣│   ▼Commit number 03                                                    (^/main) c02add Test User      24-10-15 12:03
            ┣│   ▼Commit number 02                                                             8332dd Test User      24-10-15 12:02
            ┣╯    Commit number 01                                                             5692a8 Test User      24-10-15 12:01
            ┗     Commit number 00                                                             a823b7 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("▲"),
            repo.Path
        );

        Assert.AreEqual(
            """
            Commit number 02
            Commit number 01
            Commit number 00
            """,
            await repo.GitAsync("log --format=%s"),
            "The top two commits are one, keeping the older of the two messages"
        );
    }

    // Regression test: squashing commits that have not been pushed. This used to be refused, with
    // "Commits not on current branch", because the guard asked for 'IsLocalCurrent' alone — a flag
    // only ever set on a *remote* branch whose local branch is current (Augmenter.cs:63). A commit
    // that has not been pushed belongs to the local branch, which never carries it.
    //
    // Which was the wrong way round: the commits it did allow were the ones already published.
    [TestMethod]
    public async Task TestSquashCommitsThatHaveNotBeenPushed()
    {
        using var repo = await E2eRepo.CreateLongAsync(4);
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(4));
        gmd.WaitFor("Commit number 00");

        gmd.Send("S-Down");
        gmd.Send("S-Down");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Commit Diff ...");
        for (var i = 0; i < 2; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Squash c02add...8332dd");
        gmd.Send("Enter");

        gmd.WaitFor("Squash c02add...8332dd on 'main'");
        gmd.Send("Enter");

        // With no remote there is nothing left behind, so the two rows simply become one
        gmd.WaitUntilGone("Commit number 03");
        Assert.AreEqual(
            """
            Commit number 02
            Commit number 01
            Commit number 00
            """,
            await repo.GitAsync("log --format=%s")
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

    // Renaming is menu only, so this is also the one test that drives a menu item all the way to
    // its command. The item is disabled for the main branch, hence renaming dev, and the fixture
    // has no origin, so this is the local half of a rename; the remote half is a push and a delete
    // of the old remote branch, which the integration tests cover.
    [TestMethod]
    public async Task TestRenameBranch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // Show dev and hoover it: down to the merge commit, left to hoover main, enter to open the
        // branch merged in there, right to move the hoover from main on to dev
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        gmd.Send("Right");
        gmd.WaitForStable();

        gmd.Send("m");
        gmd.WaitFor("Branch: dev");

        // Down to 'Rename Branch ...', which is six moves and not eight, since 'Rebase and push
        // on' and 'Pull/Update' are disabled here and are skipped over. One key at a time: a menu
        // redraw drops the keys sent behind it, so a single Send of six would arrive as three.
        for (var i = 0; i < 6; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        // The name is filled in and the cursor is at its end, so the rename is a matter of editing
        // it. There is no remote branch here, so the dialog says nothing about origin.
        var dialog = gmd.WaitFor("Rename Branch");
        Assert.AreEqual(
            """
                                          ╭ Rename Branch ───────────────────────────────────────────╮
                                          │ From: dev                                                │
                                          │                                                          │
                                          ││dev                                                     ││
                                          │└────────────────────────────────────────────────────────┘│
                                          │                                                          │
                                          │                                                          │
                                          │                   [◦ OK ◦] [ Cancel ]                    │
                                          ╰──────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(dialog, repo.Path, 15, 9)
        );

        gmd.SendText("2");
        gmd.WaitFor("dev2");
        gmd.Send("Enter");

        // The branch is drawn under its new name, in the same column and with the same commits
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (dev2) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣   ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮    Merge branch 'dev' into main                                                 4e73d2 Test User      24-10-15 12:05
            ┣│    Add gamma                                                                    4a15fb Test User      24-10-15 12:04
            ┃╰╊   More dev work                                                         (dev2) af3ee6 Test User      24-10-15 12:03
            ┃╭┺   Work on dev                                                                  d997ad Test User      24-10-15 12:02
            ┣╯    Add beta                                                                     dd7891 Test User      24-10-15 12:01
            ┗     Initial                                                                      9dc406 Test User      24-10-15 12:00
            """,
            gmd.WaitFor("(dev2)"),
            repo.Path
        );

        Assert.AreEqual("  dev2\n* main", await repo.GitAsync("branch"));
        Assert.AreEqual("main", await repo.GitAsync("rev-parse --abbrev-ref HEAD"), "Renaming does not check out");
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

    // 'E' is the other direction: the current branch is merged into the hoovered one. Git can only
    // merge into the branch that is checked out, so the whole point of this test is what happens
    // around the merge — the target is checked out, the commit dialog opens there, and once it is
    // committed the branch that was current at the start is checked out again.
    [TestMethod]
    public async Task TestMergeToBranch()
    {
        // Current is main, which has 'Add delta' that dev does not, so main into dev is a real
        // merge. It is TestMergeBranch's merge in the other direction and driven the other way:
        // there the target was checked out first, here gmd does that checkout itself.
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, commitTime: TempRepo.BaseTime.AddMinutes(7));
        gmd.WaitFor("Initial");

        // Show dev and hoover it: down to the merge commit, left to hoover main, enter to open the
        // branch merged in there, right to move the hoover from main on to dev
        gmd.Send("Down");
        gmd.WaitFor("Merge branch");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("Enter");
        gmd.WaitFor("More dev work");
        gmd.Send("Right");
        gmd.WaitForStable();

        gmd.Send("E");

        // The uncommitted row says dev is the current branch now, i.e. gmd switched to the target
        // on the way, and the dialog offers the message git would have used
        var screen = gmd.WaitFor("Commit 2 changes");
        Assert.AreEqual(
            """
             ╭╊  ©Merge branch 'main' into dev, 2 uncommitted changes                  (● dev)                       NN-NN-NN NN:NN
            """,
            ScreenText.MaskTimes(ScreenText.Rows(screen, repo.Path, 2, 1), "uncommitted")
        );
        StringAssert.Contains(screen, "[Merge branch 'main' into dev");

        gmd.Send("Enter");

        // Back on main, which is where it started, with the merge commit on dev
        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
             ╭╊   Merge branch 'main' into dev                                           (dev) 60e4d8 Test User      24-10-15 12:07
            ┣╯┃ ● Add delta                                                     (● main)[v1.0] 17d85b Test User      24-10-15 12:06
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

        // Switched back to where it started, and dev's tip is a real merge commit, i.e. two
        // parents: dev's old tip and main's tip
        Assert.AreEqual("main", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("af3ee69 17d85ba", await repo.GitAsync("log --format=%p -1 dev"));
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
    }

    // Nothing to merge is the outcome that has to switch back without a commit dialog, since there
    // is nothing to commit. The fixture has dev merged into main already, so it is the plain case.
    [TestMethod]
    public async Task TestMergeToBranchThatIsAlreadyUpToDate()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.GitAsync("checkout -q dev");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("Left"); // Hoovers main, the branch the cursor row is on
        gmd.WaitForStable();
        gmd.Send("E");

        Assert.AreEqual(
            """
                                                   ╭ Merge ─────────────────────────────────╮
                                                   │'main' is already up to date with 'dev'.│
                                                   │                                        │
                                                   │                [◦ OK ◦]                │
                                                   ╰────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("already up to date"), repo.Path, 17, 5)
        );

        // Left on the branch it started on, with nothing committed anywhere
        Assert.AreEqual("dev", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        Assert.AreEqual("", await repo.GitAsync("status -s"), "The working tree should be clean");
        Assert.AreEqual("17d85ba", await repo.GitAsync("rev-parse --short=7 main"));
    }

    // The other arm of 'E', mirroring TestMergeFromMenu: with the current branch hoovered there is
    // nothing to merge it out of, so it offers the branches to merge it into instead
    [TestMethod]
    public async Task TestMergeToMenu()
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

        gmd.Send("E");

        // Only the shown branches are offered, so the menu lists dev and nothing else. The stray
        // 'k' is the tail of 'More dev work' behind the menu, which is drawn over the log view.
        Assert.AreEqual(
            """
            ┣│ ╭ Merge to ─╮                                                                   4a15fb Test User      24-10-15 12:04
            ┃╰╊│ o  dev    │ork                                                          (dev) af3ee6 Test User      24-10-15 12:03
            ┃╭┺╰───────────╯v                                                                  d997ad Test User      24-10-15 12:02
            """,
            ScreenText.Rows(gmd.WaitFor("Merge to"), repo.Path, 4, 3)
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

    // The conflict resolver opens on the first conflict rather than at the top of the file. A
    // conflict is usually a long way down a file that is mostly text both sides agree on, so a view
    // that opened at the top would be showing anything except what it was opened for.
    [TestMethod]
    public async Task TestConflictResolverOpensOnTheFirstConflict()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);

        // Lines 35 to 61 of an 80 line file, i.e. the conflict, with the text around it for context
        // and the result of it in the pane below
        ScreenText.AssertEqual(
            """
            Merge  long.txt   conflict 1 of 1   1 still to resolve
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            line 35
            line 36
            line 37
            line 38
            line 39
            ─── Conflict 1 ── unresolved ───────
            HEAD                                                       │dev
            line 40 on main                                            │line 40 on dev
            line 41
            line 42
            line 43
            line 44
            line 45                                                                                                                ┃
            line 46                                                                                                                ┃
            line 47                                                                                                                ┃
            line 48                                                                                                                ┃
            line 49                                                                                                                ┃
            line 50                                                                                                                ┃
            line 51                                                                                                                ┃
            line 52                                                                                                                ┃
            line 53                                                                                                                ┃
            line 54                                                                                                                ┃
            line 55                                                                                                                ┃
            line 56                                                                                                                ┃
            line 57
            line 58
            line 59
            line 60
            line 61
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            Conflict 1 is not resolved yet — press 1, 2, 3, 4 or 0
            """,
            gmd.WaitFor("─── Conflict 1"),
            repo.Path
        );
    }

    // ']' and '[' walk to the conflict from wherever the cursor is. For a file with a single
    // conflict that is the whole of what they do — there is no second conflict to step to — and
    // stepping by conflict number left both keys dead in exactly the file where the conflict is
    // hardest to find by hand.
    [TestMethod]
    public async Task TestNextAndPreviousConflictReachTheOnlyConflict()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        // The top of the file, from where the conflict is below the screen
        gmd.Send("Home");
        StringAssert.Contains(gmd.WaitUntilGone("─── Conflict 1"), "line 1", "The top of the file");
        gmd.Send("]");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on main", "']' goes to the one conflict");

        // And the end of the file, from where it is above the screen
        gmd.Send("End");
        StringAssert.Contains(gmd.WaitUntilGone("─── Conflict 1"), "line 80", "The end of the file");
        gmd.Send("[");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on dev", "'[' goes back to it");
    }

    // The letter shortcuts are registered in both cases, which is a safety matter rather than
    // politeness: a key the resolver does not handle falls through to the log view below, where 'U'
    // pulls every branch and 'P' pushes every branch — neither of them a thing to do to a repository
    // stopped in the middle of a merge. Upper case is how the menu and the help write a shortcut, so
    // upper case is what gets pressed.
    [TestMethod]
    public async Task TestUpperCaseShortcutsActOnTheConflict()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        // 'U' is un-decide, and reaching the log view instead would leave this decided
        gmd.Send("1");
        gmd.WaitFor("all resolved");
        gmd.Send("U");
        StringAssert.Contains(gmd.WaitFor("1 still to resolve"), "── unresolved", "'U' un-decides it");

        // 'N' and 'P' are the next and previous conflict, as the menu says they are
        gmd.Send("Home");
        gmd.WaitUntilGone("─── Conflict 1");
        gmd.Send("N");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on main", "'N' goes to the conflict");

        gmd.Send("End");
        gmd.WaitUntilGone("─── Conflict 1");
        gmd.Send("P");
        StringAssert.Contains(gmd.WaitFor("─── Conflict 1"), "line 40 on dev", "'P' goes back to it");
    }

    // '0' resolves a conflict to the common ancestor, i.e. undoes what both sides did to it. The
    // ancestor is not in the file — the fixture uses git's default conflict style — so this is also
    // the test that it is recovered on demand, and it is shown as it is taken, since a decision made
    // from text that is not on the screen is one the user cannot check.
    [TestMethod]
    public async Task TestResolvingAConflictToTheCommonAncestor()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("0");

        // Three panes now, with the recovered ancestor in the middle, and the pane below showing
        // what the conflict resolves to: the line as it was before either side touched it
        ScreenText.AssertEqual(
            """
            Merge  long.txt   conflict 1 of 1   all resolved
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            line 35
            line 36
            line 37
            line 38
            line 39
            ─── Conflict 1 ── using the common ancestor
            HEAD                                   │common ancestor                        │dev
            line 40 on main                        │line 40                                │line 40 on dev
            line 41
            line 42
            line 43
            line 44
            line 45                                                                                                                ┃
            line 46                                                                                                                ┃
            line 47                                                                                                                ┃
            line 48                                                                                                                ┃
            line 49                                                                                                                ┃
            line 50                                                                                                                ┃
            line 51                                                                                                                ┃
            line 52                                                                                                                ┃
            line 53                                                                                                                ┃
            line 54                                                                                                                ┃
            line 55                                                                                                                ┃
            line 56                                                                                                                ┃
            line 57
            line 58
            line 59
            line 60
            line 61
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            line 40
            """,
            gmd.WaitFor("all resolved"),
            repo.Path
        );

        // And saving it writes the ancestor's line back into the file
        gmd.Send("S");
        gmd.WaitUntilGone("─── Conflict 1");

        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt"));
        StringAssert.Contains(text, "line 39\nline 40\nline 41", "Both sides' changes are undone");
        Assert.IsFalse(text.Contains("<<<<<<<"), "and no markers are left in it");
    }

    // Nothing is written until 'S', so closing is what throws decisions away — and the moment it is
    // most likely to happen is once every conflict has been decided and the file looks finished on
    // screen. That case used to close without a word and lose the lot, since the guard tested
    // "not fully resolved" rather than "anything decided".
    [TestMethod]
    public async Task TestClosingWithEveryConflictDecidedButUnsavedAsksFirst()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("1");
        gmd.WaitFor("all resolved");
        gmd.Send("Escape");

        StringAssert.Contains(
            gmd.WaitFor("Unsaved Decisions"),
            "1 of 1 conflicts have been decided but not saved",
            "Closing with decisions unsaved asks rather than discarding them"
        );

        // 'Stay' leaves the resolver open with the decision still made
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitUntilGone("Unsaved Decisions"), "all resolved", "Still there to save");

        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt"));
        StringAssert.Contains(text, "<<<<<<<", "and nothing has been written to the file");
    }

    // Nothing decided is nothing to lose, so that close is not about unsaved work — but leaving the
    // file conflicted is still worth a word, since the merge cannot be committed until it is not
    [TestMethod]
    public async Task TestClosingWithNothingDecidedWarnsAboutTheConflictsInstead()
    {
        using var repo = await E2eRepo.CreateWithConflictAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        gmd.Send("Escape");

        StringAssert.Contains(
            gmd.WaitFor("Unresolved Conflicts"),
            "1 of 1 conflicts are still unresolved",
            "No decisions to lose, so it is the conflicts that are warned about"
        );
    }

    // Saving with conflicts still undecided writes their markers back, so the file no longer holds
    // the conflicts the view was built from — the decided one is stable text now and the rest have
    // moved up to fill its number. The view therefore re-reads it, and this is the test that a
    // second save then works: it used to be refused with "the file has changed on disk", leaving
    // closing and re-opening the resolver as the only way to finish the file.
    [TestMethod]
    public async Task TestSavingTwiceFinishesAFileResolvedInTwoGoes()
    {
        using var repo = await E2eRepo.CreateWithTwoConflictsAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        OpenTheResolver(gmd);
        gmd.WaitFor("─── Conflict 1");

        // Decide only the first of the two, and save
        gmd.Send("1");
        gmd.WaitFor("1 still to resolve");
        gmd.Send("S");
        StringAssert.Contains(gmd.WaitFor("Saved"), "1 of its conflicts still have their markers in it");
        gmd.Send("Enter");

        // Re-read, so what is left is one conflict rather than the second of two
        StringAssert.Contains(
            gmd.WaitFor("conflict 1 of 1"),
            "1 still to resolve",
            "The view is the file as it is now, not as it was opened"
        );

        // ... and deciding it and saving again finishes the file rather than being refused
        gmd.Send("2");
        gmd.WaitFor("all resolved");
        gmd.Send("S");
        gmd.WaitUntilGone("─── Conflict 1");

        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "long.txt"));
        Assert.IsFalse(text.Contains("<<<<<<<"), "No markers are left in it");
        StringAssert.Contains(text, "line 20 on main", "The first conflict took ours");
        StringAssert.Contains(text, "line 60 on dev", "and the second theirs");
    }

    // The conflicts of the uncommitted merge, reached the way a user reaches them: the diff of the
    // uncommitted changes, its Resolve Conflicts menu, and the one conflicted file in it
    static void OpenTheResolver(TmuxSession gmd)
    {
        gmd.WaitFor("CONFLICTS");
        gmd.Send("d");
        gmd.WaitFor("Conflicts:   long.txt");
        gmd.Send("Enter");
        gmd.WaitFor("Resolve Conflicts");
        gmd.Send("Enter");
    }
}
