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
            "'feature' was deleted on origin, e.g. its pull request was merged: Publish in its branch menu puts it back",
            DeletedOnOrigin.WhyNoPush(repo, feature)
        );
        Assert.AreEqual(
            """
            Publish 'feature' to origin again?

            'origin/feature' was deleted on origin, e.g. when its pull
            request was merged. Publishing makes it again there, for everyone.
            """,
            DeletedOnOrigin.PublishAgainQuestion(repo, feature)
        );
    }

    // Each branch found merged on origin is told once, when a read first finds it, and again only once
    // it moved, i.e. it is another branch of the same name
    [TestMethod]
    public void TestEachBranchMergedOnOriginIsToldOnce()
    {
        var notes = new MergedOnOriginNotes();
        gmd.Server.Repo RepoWith(string tip, params string[] names) =>
            gmd.Server.Repo.Empty with
            {
                AllBranches = names.Select(n => Branch(n, tip)).ToList(),
                MergedOnOrigin = names.ToDictionary(n => n, _ => "main"),
            };

        Assert.AreEqual(
            "Delete Merged Branches in the repo menu (⇧m) deletes 'feature', merged into 'main' on origin",
            notes.Untold(RepoWith("f2", "feature"))
        );
        Assert.IsNull(notes.Untold(RepoWith("f2", "feature")));
        Assert.IsNotNull(notes.Untold(RepoWith("f3", "feature")), "Another branch of the name");
        Assert.AreEqual(
            "Delete Merged Branches in the repo menu (⇧m) deletes the 2 branches merged on origin",
            notes.Untold(RepoWith("f2", "a", "b"))
        );
    }

    static gmd.Server.Branch Branch(string name, string tip) =>
        new(
            name,
            name,
            name,
            name,
            name,
            RepoBuilder.Sha(tip),
            RepoBuilder.Sha(tip),
            false,
            false,
            false,
            "",
            "",
            "",
            false,
            true,
            false,
            true,
            false,
            "",
            "",
            false,
            false,
            "",
            [],
            [],
            [],
            false,
            0,
            false,
            false
        );
}
