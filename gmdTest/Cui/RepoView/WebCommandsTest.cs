using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What the browser items open: a branch by its name on the remote, a pull request into the branch
// it was made from, and nothing gmd knows is not on the remote, since that page is not there
[TestClass]
public class WebCommandsTest
{
    [TestMethod]
    public async Task TestABranchIsOpenedByItsNameOnTheRemote()
    {
        var repo = await Fixture().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("feature", WebCommands.RemoteName(repo, "feature"));
        Assert.AreEqual("feature", WebCommands.RemoteName(repo, "origin/feature"));
        Assert.AreEqual("", WebCommands.RemoteName(repo, "wip"), "Never pushed");
    }

    // The base is the branch it was made from, which gmd knows and the service does not
    [TestMethod]
    public async Task TestAPullRequestGoesIntoTheBranchItWasMadeFrom()
    {
        var repo = await Fixture().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("dev", WebCommands.PullRequestBase(repo, "feature"));
        Assert.AreEqual("main", WebCommands.PullRequestBase(repo, "dev"));
        Assert.IsTrue(WebCommands.CanOpenNewPullRequest(repo, "feature"));
    }

    [TestMethod]
    public async Task TestWhatIsNotOnTheRemoteSaysSo()
    {
        var repo = await Fixture().ViewRepoAsync(ShowBranches.AllActive);

        Assert.IsFalse(WebCommands.CanOpenBranch(repo, "wip"));
        Assert.AreEqual("'wip' is not on the remote: push it with p first", WebCommands.WhyNoOpenBranch(repo, "wip"));
        Assert.IsFalse(WebCommands.CanOpenNewPullRequest(repo, "main"));
        Assert.AreEqual("The main branch is what pull requests go into", WebCommands.WhyNoNewPullRequest(repo, "main"));
    }

    // A commit not yet pushed would open a page that is not there, the uncommitted changes have none
    [TestMethod]
    public async Task TestOnlyAPushedCommitCanBeOpened()
    {
        var repo = await Fixture().WithStatus(modified: 1).ViewRepoAsync(ShowBranches.AllActive);

        Assert.IsTrue(WebCommands.CanOpenCommit(repo, repo.CommitById[RepoBuilder.Sha("d1")]));
        Assert.IsFalse(WebCommands.CanOpenCommit(repo, repo.CommitById[RepoBuilder.Sha("l1")]));
        Assert.AreEqual(
            "The commit is not pushed yet: push it with p first",
            WebCommands.WhyNoOpenCommit(repo, repo.CommitById[RepoBuilder.Sha("l1")])
        );
        Assert.IsFalse(WebCommands.CanOpenCommit(repo, repo.CommitById[Repo.UncommittedId]));
    }

    [TestMethod]
    public async Task TestARepoWithNoRemoteSaysThat()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", "Initial")
            .LocalBranch("main", "c1", isCurrent: true)
            .ViewRepoAsync();

        Assert.AreEqual("The repository has no remote branches", WebCommands.WhyNoOpenBranch(repo, "main"));
        Assert.AreEqual(
            "The repository has no remote branches",
            WebCommands.WhyNoOpenCommit(repo, repo.CommitById[RepoBuilder.Sha("c1")])
        );
    }

    // 'main' with one commit not yet pushed, 'dev' made from main and 'feature' from dev, both
    // pushed, and 'wip', which never was
    static RepoBuilder Fixture() =>
        new RepoBuilder()
            .Commit("w1", "Wip", "c1")
            .Commit("f1", "Feature work", "d1")
            .Commit("l1", "Local 1", "c1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
            .BranchWithRemote("dev", "d1")
            .BranchWithRemote("feature", "f1")
            .LocalBranch("wip", "w1");
}
