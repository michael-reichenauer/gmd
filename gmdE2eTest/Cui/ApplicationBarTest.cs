using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// The line at the top: the repository, the current branch and the markers after it.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class ApplicationBarTest
{
    // Work pushed to a branch that is not shown used to have no sign on screen at all, ▼ counting
    // only the shown branches. ✦ counts the branches it is on, and lists them, which is seeing them,
    // so the ✦ goes as the list opens; picking one shows it.
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
        // be taken as seen by it (RepoView.OnRefreshRepo), so the ✦ never came
        await repo.GitAsync("fetch -q origin");

        var withNews = gmd.WaitFor("✦1");
        Assert.IsFalse(withNews.Contains("Remote work"), "The branch is still hidden");
        var (x, y) = TmuxSession.PositionOf(withNews, "✦1");
        gmd.Click(x, y);
        gmd.WaitFor("New on Hidden Branches");
        Assert.IsFalse(gmd.WaitFor("dev (1 new)").Contains("✦1"), "Seen once listed");

        gmd.Send("Enter");

        gmd.WaitFor("Remote work");
    }

    // A branch made by another tool, e.g. Claude Code, which makes one in a worktree of its own, is
    // never pushed and has no commits of its own at first. It is new all the same. Listed, it is
    // seen, and stays hidden: knowing of it is what the user needed.
    [TestMethod]
    public async Task TestANewLocalBranchIsCountedAndSeenOnceListed()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial"); // Read once, which takes every tip there is as seen

        await repo.AddWorktreeAsync("agent");

        var withNews = gmd.WaitFor("✦1");
        var (x, y) = TmuxSession.PositionOf(withNews, "✦1");
        gmd.Click(x, y);
        gmd.WaitFor("agent (new branch)");

        gmd.Send("Escape");

        var closed = gmd.WaitUntilGone("New on Hidden Branches");
        Assert.IsFalse(closed.Contains("✦"), "Seen");
        Assert.IsFalse(closed.Contains("(⌂ agent)"), "and still hidden");
    }

    // The same news is the New group at the top of Show Branch, and stepping into it is looking
    [TestMethod]
    public async Task TestTheNewGroupOfShowBranchIsSeenOnOpening()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        await repo.AddWorktreeAsync("agent");
        gmd.WaitFor("✦1");

        gmd.Send("S-Right");
        gmd.WaitFor("type to find");
        gmd.Send("Right");

        Assert.IsFalse(gmd.WaitFor("agent (new branch)").Contains("✦1"), "Seen once listed");
    }

    // The line under the bar spans the terminal however wide it is. It was a label of 200 line chars,
    // which stopped short of the right edge on a wide terminal.
    [TestMethod]
    public async Task TestTheLineUnderTheBarSpansAWideTerminal()
    {
        const int Width = 260;
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, width: Width);

        var line = gmd.WaitFor("Initial").Split('\n')[1];

        Assert.AreEqual(new string('─', Width), line);
    }
}
