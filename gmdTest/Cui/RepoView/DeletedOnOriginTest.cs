using gmd.Cui.RepoView;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What is said of a branch deleted on origin, e.g. once its pull request was merged there
[TestClass]
public class DeletedOnOriginTest
{
    static async Task<gmd.Server.Repo> RepoAsync() =>
        await new RepoBuilder()
            .Commit("f1", "Feature", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f1", isCurrent: true, remoteName: "origin/feature")
            .ViewRepoAsync();

    [TestMethod]
    public async Task TestItIsNoBranchToPublish()
    {
        var repo = await RepoAsync();
        var feature = repo.BranchByName["feature"];

        Assert.IsTrue(DeletedOnOrigin.IsGone(feature));
        Assert.AreEqual(
            "'feature' was deleted on origin, e.g. its pull request was merged: Push in its branch menu puts it back",
            DeletedOnOrigin.WhyNoPush(repo, feature)
        );
        Assert.AreEqual(
            """
            Push 'feature' to origin again?

            'origin/feature' was deleted on origin, e.g. when its pull
            request was merged. Pushing makes it again there, for everyone.
            """,
            DeletedOnOrigin.PushAgainQuestion(repo, feature)
        );
    }
}
