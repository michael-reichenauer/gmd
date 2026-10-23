using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// Which branches deleted on origin were squash merged or rebase merged there: which ones are asked
// about, into which branch, and that each answer is asked of git once
[TestClass]
public class MergedBranchServiceTest
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
        var service = new MergedBranchService(git, new FakeRepoConfig());

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
        var service = new MergedBranchService(git, new FakeRepoConfig());

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

        Assert.IsFalse(new MergedBranchService(new FakeGit(), new FakeRepoConfig()).HasUnknown(repo));
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

        AssertOk(await new MergedBranchService(git, new FakeRepoConfig()).GetUpdatedRepoAsync(repo));

        CollectionAssert.AreEqual(new[] { (Id("d1"), Id("f1")), (Id("m1"), Id("f1")) }, git.MergedByContentAsked);
    }

    // Delete Merged Branches lists the local branches with no branch on origin whose work is in main
    // already, newest first: merged as git counts it, or squash or rebase merged on origin. Not one just
    // made, which git counts merged but which has no commit of its own, nor one origin has, nor main.
    [TestMethod]
    public async Task TestTheMergedBranchesListed()
    {
        var repo = await new RepoBuilder()
            .Commit("m3", "Merge branch 'merged' into main", "m2", "g1")
            .Commit("m2", "Login (#12)", "m1")
            .Commit("g1", "Merged work", "m1")
            .Commit("p1", "Pushed work", "m1")
            .Commit("f2", "Validate", "f1")
            .Commit("f1", "Add login", "m1")
            .Commit("w1", "Work", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m3", isCurrent: true)
            .LocalBranch("feature", "f2", remoteName: "origin/feature")
            .LocalBranch("merged", "g1")
            .LocalBranch("fresh", "m1")
            .LocalBranch("work", "w1")
            .BranchWithRemote("pushed", "p1")
            .ViewRepoAsync();
        var git = new FakeGit();
        git.MergedByContent.Add(Id("f2"));
        git.MergedBranchNames[Id("m3")] = ["main", "merged", "fresh", "pushed"];

        var merged = AssertOk(await new MergedBranchService(git, new FakeRepoConfig()).GetMergedBranchesAsync(repo));

        Assert.AreEqual(
            "merged into main, feature into main by content",
            string.Join(", ", merged.Select(m => $"{m.Name} into {m.IntoName}{(m.IsByContent ? " by content" : "")}"))
        );
        Assert.AreEqual(
            ("Validate", Id("f2"), false, ""),
            (merged[1].Subject, merged[1].TipId, merged[1].IsCurrent, merged[1].WorktreePath)
        );
    }

    // A branch origin has is listed when origin's is merged as git counts it, with its local branch,
    // which is merged too, or alone when there is none, and with the author of its last commit. Not one
    // whose local branch has commits not merged, nor one not merged, nor origin's main. A local branch
    // that does not track origin's of its name is a branch of its own, and listed so.
    [TestMethod]
    public async Task TestTheMergedBranchesOnOriginListed()
    {
        var repo = await new RepoBuilder()
            .Commit("m5", "Merge branch 'solo' into main", "m4", "s1")
            .Commit("m4", "Merge branch 'old' into main", "m3", "o1")
            .Commit("m3", "Merge branch 'done' into main", "m2", "d1")
            .Commit("m2", "Merge branch 'ahead' into main", "m1", "a1")
            .Commit("a2", "Ahead work", "a1")
            .Commit("a1", "Pushed ahead work", "m1")
            .Commit("o1", "Old work", "m1")
            .Commit("s1", "Solo work", "m1")
            .Commit("d1", "Done work", "m1")
            .Commit("n1", "Open work", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m5", isCurrent: true)
            .BranchWithRemote("done", "d1")
            .RemoteBranch("origin/old", "o1")
            .LocalBranch("solo", "s1")
            .RemoteBranch("origin/solo", "s1")
            .BranchWithRemote("ahead", "a2", remoteTipCommit: "a1", ahead: 1)
            .BranchWithRemote("open", "n1")
            .ViewRepoAsync(ShowBranches.AllActive);
        var git = new FakeGit();
        git.MergedBranchNames[Id("m5")] =
        [
            "main",
            "origin/main",
            "done",
            "origin/done",
            "origin/old",
            "origin/ahead",
            "solo",
            "origin/solo",
        ];

        var merged = AssertOk(await new MergedBranchService(git, new FakeRepoConfig()).GetMergedBranchesAsync(repo));

        Assert.AreEqual(
            "old (, origin/old) into main, solo (solo, ) into main, solo (, origin/solo) into main, done (done, origin/done) into main",
            string.Join(", ", merged.Select(m => $"{m.Name} ({m.LocalName}, {m.RemoteName}) into {m.IntoName}"))
        );
        Assert.AreEqual(("Done work", Id("d1"), "Test Author"), (merged[3].Subject, merged[3].TipId, merged[3].Author));
    }

    // A local branch is deleted with origin's branch only when that is origin's of its own name: one
    // made from 'origin/develop', which it tracks, is listed as a branch here, and 'origin/develop'
    // never; someone else's branch of another name is listed alone, with no local branch, whose commits
    // are drawn as that one's; and no branch of another remote, which deleting on origin cannot delete
    [TestMethod]
    public async Task TestOnlyOriginsBranchOfItsNameIsDeletedWithALocalBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("m3", "Merge branch 'up'", "m2", "u1")
            .Commit("m2", "Merge branch 'mine'", "m1", "y1")
            .Commit("y1", "Mine", "x1")
            .Commit("u1", "Upstream work", "m1")
            .Commit("x1", "Fix work", "d1")
            .Commit("d1", "Develop", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m3", isCurrent: true)
            .BranchWithRemote("develop", "d1")
            .LocalBranch("fix", "x1", remoteName: "origin/develop")
            .LocalBranch("mine", "y1", remoteName: "origin/theirs")
            .RemoteBranch("origin/theirs", "y1")
            .RemoteBranch("upstream/up", "u1")
            .ViewRepoAsync();
        var git = new FakeGit();
        git.MergedBranchNames[Id("m3")] =
        [
            "main",
            "origin/main",
            "develop",
            "origin/develop",
            "fix",
            "mine",
            "origin/theirs",
            "upstream/up",
        ];

        var merged = AssertOk(await new MergedBranchService(git, new FakeRepoConfig()).GetMergedBranchesAsync(repo));

        CollectionAssert.AreEquivalent(
            new[] { "fix (fix, )", "theirs (, origin/theirs)" },
            merged.Select(m => $"{m.Name} ({m.LocalName}, {m.RemoteName})").ToArray()
        );
    }
}
