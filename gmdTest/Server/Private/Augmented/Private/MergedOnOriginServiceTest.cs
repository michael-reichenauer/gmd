using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// Which branches deleted on origin were squash merged or rebase merged there: which ones are asked
// about, into which branch, and that each answer is asked of git once
[TestClass]
public class MergedOnOriginServiceTest
{
    static string Id(string name) => RepoBuilder.Sha(name);

    // feature, made from main and pushed, was squash merged into main on origin as m2 and deleted
    // there, which a fetch pruned here; main here is behind
    static RepoBuilder Squashed(string mainTip = "m2") =>
        new RepoBuilder()
            .Commit("m3", "Later", "m2")
            .Commit("m2", "Login (#12)", "m1")
            .Commit("f2", "Validate", "f1")
            .Commit("f1", "Add login", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1", remoteTipCommit: mainTip, behind: 1)
            .LocalBranch("feature", "f2", isCurrent: true, remoteName: "origin/feature");

    [TestMethod]
    public async Task TestASquashMergedBranchIsMergedIntoMain()
    {
        var builder = Squashed();
        var repo = await builder.ViewRepoAsync();
        var git = new FakeGit();
        git.MergedByContent.Add(Id("f2"));
        var service = new MergedOnOriginService(git);

        Assert.IsTrue(service.HasUnknown(repo));
        Assert.AreEqual(0, service.WithKnown(repo).MergedOnOrigin.Count, "Not known before it is asked");

        var updated = AssertOk(await service.GetUpdatedRepoAsync(repo));

        Assert.AreEqual("main", updated.MergedOnOrigin["feature"]);
        CollectionAssert.AreEqual(new[] { (Id("m2"), Id("f2")) }, git.MergedByContentAsked, "Into origin's main");
        Assert.IsFalse(service.HasUnknown(updated));
        Assert.AreEqual(
            "main",
            service.WithKnown(await builder.ViewRepoAsync()).MergedOnOrigin["feature"],
            "Known to the next read"
        );
        Assert.AreEqual(
            "main",
            service.WithKnown(await Squashed("m3").ViewRepoAsync()).MergedOnOrigin["feature"],
            "Merged for good, once main moves on too"
        );
    }

    // One not merged is asked again when the branch it may have gone into moves, e.g. once the pull
    // request is merged, and not before
    [TestMethod]
    public async Task TestABranchNotMergedIsAskedAgainOnceMainMoves()
    {
        var git = new FakeGit();
        var service = new MergedOnOriginService(git);

        var updated = AssertOk(await service.GetUpdatedRepoAsync(await Squashed().ViewRepoAsync()));

        Assert.AreEqual(0, updated.MergedOnOrigin.Count);
        Assert.IsFalse(service.HasUnknown(await Squashed().ViewRepoAsync()));
        Assert.IsTrue(service.HasUnknown(await Squashed("m3").ViewRepoAsync()));
    }

    // Only a branch deleted on origin and merged nowhere as git sees it: not one origin has, nor one
    // never pushed, nor one merged with a merge commit, which the graph shows merged already
    [TestMethod]
    public async Task TestOnlyBranchesDeletedOnOriginAndNotMergedAsGitSeesItAreAsked()
    {
        var repo = await new RepoBuilder()
            .Commit("m2", "Merge branch 'merged'", "m1", "g1")
            .Commit("g1", "Merged with a merge commit", "m1")
            .Commit("p1", "Pushed", "m1")
            .Commit("l1", "Local", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m2", isCurrent: true)
            .BranchWithRemote("pushed", "p1")
            .LocalBranch("local", "l1")
            .LocalBranch("merged", "g1", remoteName: "origin/merged")
            .ViewRepoAsync("pushed", "local", "merged");

        Assert.IsFalse(new MergedOnOriginService(new FakeGit()).HasUnknown(repo));
    }

    // A branch made from another than main may have gone into that one, which is asked first
    [TestMethod]
    public async Task TestTheBranchItWasMadeFromIsAskedFirst()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature", "d1")
            .Commit("d1", "Develop", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .BranchWithRemote("develop", "d1")
            .LocalBranch("feature", "f1", isCurrent: true, remoteName: "origin/feature")
            .ViewRepoAsync("develop");
        var git = new FakeGit();

        AssertOk(await new MergedOnOriginService(git).GetUpdatedRepoAsync(repo));

        CollectionAssert.AreEqual(new[] { (Id("d1"), Id("f1")), (Id("m1"), Id("f1")) }, git.MergedByContentAsked);
    }
}
