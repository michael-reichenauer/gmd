using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// The rules of the branch commands that are plain functions of the shown repo
[TestClass]
public class BranchCommandsTest
{
    // Rebase and Push onto pushes with force, leased on origin's tip as shown. A commit someone pushed
    // and the fetch brought in moved that tip, so the lease let the push through and dropped the
    // commit from origin. Such a branch is pulled first.
    [TestMethod]
    public async Task TestABranchBehindOriginIsNotRebasedAndPushed()
    {
        var repo = await Feature(behind: 1).ViewRepoAsync("feature");

        Assert.AreEqual(
            "'origin/feature' has commits 'feature' has not, which the push would drop: pull them first",
            BranchCommands.WhyNotRebaseAndPush(repo, repo.CurrentBranch())
        );
    }

    [TestMethod]
    public async Task TestABranchOnOriginWithNothingToPullIsRebasedAndPushed()
    {
        var repo = await Feature(behind: 0).ViewRepoAsync("feature");

        Assert.AreEqual("", BranchCommands.WhyNotRebaseAndPush(repo, repo.CurrentBranch()));
    }

    // Nothing is pushed for a branch that is not on origin, so nothing can be dropped there
    [TestMethod]
    public async Task TestABranchNotOnOriginIsRebased()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .LocalBranch("feature", "f1", isCurrent: true)
            .ViewRepoAsync("feature");

        Assert.AreEqual("", BranchCommands.WhyNotRebaseAndPush(repo, repo.CurrentBranch()));
    }

    // feature started from main's c1 and is on origin; with 'behind', origin's has one commit more
    static RepoBuilder Feature(int behind) =>
        new RepoBuilder()
            .Commit("f2", "Someone's feature work", "f1")
            .Commit("c2", "Second", "c1")
            .Commit("f1", "Feature", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .BranchWithRemote(
                "feature",
                "f1",
                isCurrent: true,
                remoteTipCommit: behind > 0 ? "f2" : "f1",
                behind: behind
            );
}
