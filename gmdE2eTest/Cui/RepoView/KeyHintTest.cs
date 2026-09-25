using System.Text.RegularExpressions;
using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// The key-hint line at the bottom of the log view: the keys that do something where the cursor
// is. What it says in each case is KeyHintsTest's; this is that it is drawn, where, and that it
// follows the cursor. The line is off in every other end-to-end test (see TempHome), so these turn
// it on, and use a short terminal so that a snapshot of the whole screen stays readable.
//
// They wait for a hint rather than for '? help', which is on the line for a moment on its own,
// while the first repo is read.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class KeyHintTest
{
    const int Height = 12;

    // The bottom row, below the log, with help at the right
    [TestMethod]
    public async Task TestTheKeyHintsAreOnTheBottomRow()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, height: Height, isKeyHints: true);

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00




             d diff  Enter details  m menu  ←→ branch  ⇧→ show branch  f search  b new branch                                ? help
            """,
            gmd.WaitFor("d diff"),
            repo.Path
        );
    }

    // ← hoovers the branch, and the hints are then about it, named first. On the merge commit, Enter shows the
    // branch that was merged in, so that is offered there and not on the row above.
    [TestMethod]
    public async Task TestTheHintsFollowTheCursor()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, height: Height, isKeyHints: true);
        gmd.WaitFor("d diff");

        gmd.Send("Left");
        Assert.AreEqual(
            "main:  e merge from  ⇧e merge to  m menu  d diff  b new branch | ? help",
            Hints(gmd.WaitFor("merge from"))
        );

        gmd.Send("Down");
        Assert.AreEqual(
            "main:  e merge from  ⇧e merge to  Enter show/hide  m menu  d diff  b new branch | ? help",
            Hints(gmd.WaitFor("show/hide"))
        );

        gmd.Send("Right");
        Assert.AreEqual(
            "d diff  Enter details  m menu  ←→ branch  ⇧→ show branch  f search  b new branch | ? help",
            Hints(gmd.WaitFor("Enter details"))
        );
    }

    // The details pane opens above the hint line rather than over it
    [TestMethod]
    public async Task TestTheDetailsOpenAboveTheHints()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, height: 20, isKeyHints: true);
        gmd.WaitFor("d diff");

        gmd.Send("Enter");

        ScreenText.AssertEqual(
            """
             Gmd {repo}, ●main                                                       (main) [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            ┣  ● Add delta                                                      (● main)[v1.0] 17d85b Test User      24-10-15 12:06
            ┣╮   Merge branch 'dev' into main                                                  4e73d2 Test User      24-10-15 12:05
            ┣    Add gamma                                                                     4a15fb Test User      24-10-15 12:04
            ┣╯   Add beta                                                                      dd7891 Test User      24-10-15 12:01
            ┗    Initial                                                                       9dc406 Test User      24-10-15 12:00

            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
            Id:         17d85ba889a1084f912c412d0ce435c9d7a36f53  ({repo})
            Branch:     main  (main)
            Author:     Test User, time: 2024-10-15 12:06:00 +00:00
            Children:
            Parents:    4e73d2
            Tags:       [v1.0]
            Tips:       (main)
            Add delta


             d diff  Enter hide details  m menu  ←→ branch  ⇧→ show branch  f search  b new branch                           ? help
            """,
            gmd.WaitFor("hide details"),
            repo.Path
        );
    }

    // With changes, committing comes first
    [TestMethod]
    public async Task TestChangesPutCommitFirst()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo, height: Height, isKeyHints: true);

        StringAssert.StartsWith(Hints(gmd.WaitFor("c commit")), "c commit  d diff  ");
    }

    // The filter has the keyboard while it is up, so the line is about the filter
    [TestMethod]
    public async Task TestTheHintsAreAboutTheFilterWhileItIsUp()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, height: Height, isKeyHints: true);
        gmd.WaitFor("d diff");

        gmd.Send("f");

        Assert.AreEqual("↑↓ select  Enter show in the log  Esc back | ? help", Hints(gmd.WaitFor("Esc back")));
    }

    // The bottom row, with the padding before the help shown as ' | '
    static string Hints(string screen) => Regex.Replace(ScreenText.Of(screen).Split('\n')[^1].Trim(), " {3,}", " | ");
}
