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

    // After a commit is picked, n and Shift-N step through the rest of what the search found, in the
    // log, where each is seen among the commits around it. Each is shown as a pick is, its branch
    // too, as a show Backspace undoes.
    [TestMethod]
    public async Task TestNextAndPreviousMatchStepThroughTheSearch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        gmd.SendText("dev");
        gmd.WaitFor("3 commits");
        gmd.Send("Enter"); // The first, the merge, which is on main
        gmd.WaitUntilGone("Filter Commits");

        gmd.Send("n");
        var second = gmd.WaitFor("Match 2 of 3");
        Assert.AreEqual("Match 2 of 3 for 'dev'", ScreenText.LastLine(second));
        StringAssert.Contains(second, "More dev work", "Its branch is shown");

        gmd.Send("n");
        gmd.WaitFor("Match 3 of 3");
        gmd.Send("n");
        Assert.AreEqual(
            "No more matches for 'dev' below: Shift-N goes back up",
            ScreenText.LastLine(gmd.WaitFor("No more matches"))
        );
        gmd.Send("N");
        gmd.WaitFor("Match 2 of 3");

        gmd.Send("BSpace");
        gmd.WaitFor("Undid Show 'dev'");
    }

    // 'file:' searches the files the commits changed, which git is asked for, anywhere in the path
    // and in any case
    [TestMethod]
    public async Task TestFileSearchFindsTheCommitsChangingAFile()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");

        gmd.SendText("file:DEV.txt");

        var screen = gmd.WaitFor("2 commits");
        StringAssert.Contains(screen, "More dev work");
        StringAssert.Contains(screen, "Work on dev");
        Assert.IsFalse(screen.Contains("Merge branch"), "The merge brought the change in, but made none");
    }

    // A 'file:' search still waiting for git when the search closes is dropped. It used to be shown
    // once git answered, replacing the log the user had gone back to with the results, and the next
    // refresh then showed every branch they were on.
    [TestMethod]
    public async Task TestAFileSearchStillRunningWhenTheSearchClosesIsDropped()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");

        // In one go, so that the search closes while it waits for the typing to pause. Git is asked
        // anyway if it closes later, so either line in the log means the answer has come or never will.
        gmd.Send("file:dev.txt", "Escape");
        gmd.WaitUntilGone("Filter Commits");
        gmd.WaitForLogAny("Search dropped", "--full-history");

        var screen = gmd.WaitForStable();
        StringAssert.Contains(screen, "Add delta");
        Assert.IsFalse(screen.Contains("More dev work"), "The results of the search are not shown after it closed");
    }

    // A change that comes while the search is up is shown once it closes. It used to be dropped:
    // the refresh it set off read nothing while the search had the log, and nothing read again after.
    [TestMethod]
    public async Task TestAChangeMadeDuringASearchIsShownAfterIt()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        var changes = gmd.LogCount("Repo changed event");

        await repo.CommitFileAtAsync("search.txt", "x\n", "Made during the search", TempRepo.BaseTime.AddMinutes(20));
        gmd.WaitForLogTimes("Repo changed event", changes + 1); // Told of it, with the search still up
        gmd.Send("Escape");

        gmd.WaitFor("Made during the search");
    }

    // '/' opens the search as 'f' does, it being the search key of most other tools
    [TestMethod]
    public async Task TestSlashOpensTheSearch()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");

        gmd.Send("/");

        gmd.WaitFor("Filter Commits");
    }

    // A click on a result picks it, as Enter does: the search closes and the log shows the commit.
    // It used to do nothing, the dialog having the mouse and no handler for it.
    [TestMethod]
    public async Task TestClickingAResultPicksIt()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("f");
        gmd.WaitFor("Filter Commits");
        gmd.SendText("dev");
        var (x, y) = TmuxSession.PositionOf(gmd.WaitFor("3 commits"), "More dev work");

        gmd.Click(x, y);

        StringAssert.Contains(gmd.WaitUntilGone("Filter Commits"), "More dev work", "dev is shown with the commit");
    }
}
