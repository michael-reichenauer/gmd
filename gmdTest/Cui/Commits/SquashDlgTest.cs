using gmd.Cui.Commits;
using gmd.Cui.RepoView;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Commits;

// What the squash dialog starts with: the oldest commit's message, with the newer ones listed below
// it as GitHub lists the commits of a squash merge, and a heading counting them, oldest to newest
[TestClass]
public class SquashDlgTest
{
    static async Task<IReadOnlyList<gmd.Server.Commit>> CommitsAsync()
    {
        var repo = await new RepoBuilder()
            .Commit("f3", "Validate login input\n\nEmpty names are refused.\nSo are spaces.", "f2")
            .Commit("f2", "Fix typo in login", "f1")
            .Commit("f1", "Add login form\n\nWith a name and a password.", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f3", isCurrent: true)
            .ViewRepoAsync();
        var c = (string name) => repo.CommitById[RepoBuilder.Sha(name)];
        return CommitRewrite.Squashed(repo, c("f3"), c("f1"));
    }

    [TestMethod]
    public async Task TestTheMessageIsTheOldestsWithTheNewerOnesListed()
    {
        Assert.AreEqual(
            """
            Add login form

            With a name and a password.

            * Fix typo in login
            * Validate login input
              Empty names are refused.
              So are spaces.
            """,
            SquashDlg.CombinedMessage(await CommitsAsync())
        );
    }

    [TestMethod]
    public async Task TestTheHeadingCountsThemOldestToNewest()
    {
        Assert.AreEqual(
            "Squash 3 commits on 'feature', f1000000 to f3000000:",
            SquashDlg.Heading(await CommitsAsync())
        );
    }
}
