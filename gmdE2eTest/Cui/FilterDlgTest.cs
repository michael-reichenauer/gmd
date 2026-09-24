// The filter test types a word that must match nothing
// cspell:ignore zzzznothing

using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// Filtering the log for commits: the dialog, what it shows when nothing matches, and where it
// leaves the cursor.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class FilterDlgTest
{
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

    // The filter dialog is one instance for the whole session, and it used to return the commit
    // chosen in an earlier session when a later one was closed with nothing chosen, so the log view
    // jumped back to that commit. Here the cursor is on the top row when the filter is closed, and
    // has to stay there.
    [TestMethod]
    public async Task TestFilterClosedWithoutChoosingLeavesTheCursorAlone()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        // Choose a commit, which moves the cursor onto it, then move it back to the top row. Only
        // then is the top row compared, since choosing it showed its branch, which widens the graph.
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        gmd.SendText("More dev");
        gmd.WaitFor("Search: More dev");
        gmd.Send("Enter");
        gmd.WaitUntilGone("Filter Commits");
        var onChosen = ScreenText.BackgroundRows(gmd.CaptureColors(), 2, 4);
        gmd.Send("Home");
        gmd.WaitForStable();
        var atTop = ScreenText.BackgroundRows(gmd.CaptureColors(), 2, 4);
        Assert.AreNotEqual(onChosen, atTop, "The chosen commit is not the top row");

        // The filter again, closed with nothing chosen
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        gmd.Send("Escape");
        gmd.WaitUntilGone("Filter Commits");

        Assert.AreEqual(atTop, ScreenText.BackgroundRows(gmd.CaptureColors(), 2, 4), "Still on the top row");
    }
}
