using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// The line at the top: the repository, the current branch and the markers after it.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class ApplicationBarTest
{
    // Work pushed to a branch that is not shown used to have no sign on screen at all, ▼ counting
    // only the shown branches. ▽ counts it, and opens the branches it is on, where picking one shows
    // it, which is seeing it, so the ▽ goes.
    [TestMethod]
    public async Task TestNewCommitsOnAHiddenBranchAreCountedAndShownFromTheBar()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        await repo.GitAsync("push -q origin dev");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add zeta"); // Read once, which takes every tip there is as seen

        // A commit pushed to dev from elsewhere, made with commit-tree so that nothing here changes
        var tree = (await repo.GitAsync("rev-parse dev^{tree}")).Trim();
        var commit = (await repo.GitAsync("commit-tree " + tree + " -p dev -m \"Remote work\"")).Trim();
        await repo.GitAsync($"push -q origin {commit}:refs/heads/dev");
        // Left to the file monitor, which is also the regression test for a lost change: this fetch
        // lands right after gmd's first read, and a change within half a second of a read used to
        // be taken as seen by it (RepoView.OnRefreshRepo), so the ▽ never came
        await repo.GitAsync("fetch -q origin");

        var withNews = gmd.WaitFor("▽1");
        Assert.IsFalse(withNews.Contains("Remote work"), "The branch is still hidden");
        var (x, y) = TmuxSession.PositionOf(withNews, "▽1");
        gmd.Click(x, y);
        gmd.WaitFor("New on Hidden Branches");
        StringAssert.Contains(gmd.WaitFor("dev (1 new)"), "Mark All as Seen");

        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("▽1"), "Remote work", "Shown, and so seen");
    }
}
