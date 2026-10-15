using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// A local branch and its remote branch are one branch to the user, but git has two, and they can
// point at different commits. So merging or rebasing onto 'main' has to work out whether 'main' or
// 'origin/main' is the one with the commits, which is what MergeTipName answers. The rest of
// BranchWriteService is git commands, covered by the integration tests.
[TestClass]
public class BranchWriteServiceTest
{
    // The tips are the same, so either would do, and the local branch is taken whichever is asked
    // for. It used to be the other of the two, by equal author times.
    [TestMethod]
    public async Task TestSyncedPairGivesTheLocalBranch()
    {
        var repo = await Synced().AugmentedRepoAsync();

        Assert.AreEqual("main", MergeTipOf(repo, "main"));
        Assert.AreEqual("main", MergeTipOf(repo, "origin/main"));
    }

    [TestMethod]
    public async Task TestLocalBranchAheadGivesTheLocalBranch()
    {
        var repo = await Ahead().AugmentedRepoAsync();

        Assert.AreEqual("main", MergeTipOf(repo, "main"));
        Assert.AreEqual("main", MergeTipOf(repo, "origin/main"));
    }

    [TestMethod]
    public async Task TestRemoteBranchAheadGivesTheRemoteBranch()
    {
        var repo = await Behind().AugmentedRepoAsync();

        Assert.AreEqual("origin/main", MergeTipOf(repo, "main"));
        Assert.AreEqual("origin/main", MergeTipOf(repo, "origin/main"));
    }

    // A diverged pair has commits on both sides, so the youngest tip wins rather than either side
    // being complete. Merging it is what brings the other side in.
    [TestMethod]
    public async Task TestDivergedPairGivesTheYoungestTip()
    {
        var repo = await Diverged().AugmentedRepoAsync();

        Assert.AreEqual("main", MergeTipOf(repo, "main"));
        Assert.AreEqual("main", MergeTipOf(repo, "origin/main"));
    }

    // A cherry picked or rebased commit keeps its author time, so a local branch ahead by one has the
    // older tip. It is ahead all the same, which is what decides, and not the times; by the times
    // the remote branch was merged, without the local commit.
    [TestMethod]
    public async Task TestLocalBranchAheadByAnOlderCopyGivesTheLocalBranch()
    {
        var repo = await new RepoBuilder()
            .CopyOf("l1", "f1", "r1")
            .Commit("r1", "Remote 1", "c1")
            .Commit("f1", "Feature 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "r1", ahead: 1)
            .LocalBranch("feature", "f1")
            .AugmentedRepoAsync();

        Assert.AreEqual("main", MergeTipOf(repo, "main"));
        Assert.AreEqual("main", MergeTipOf(repo, "origin/main"));
    }

    [TestMethod]
    public async Task TestRemoteBranchAheadByAnOlderCopyGivesTheRemoteBranch()
    {
        var repo = await new RepoBuilder()
            .CopyOf("r2", "f1", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("f1", "Feature 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "r2", behind: 1)
            .LocalBranch("feature", "f1")
            .AugmentedRepoAsync();

        Assert.AreEqual("origin/main", MergeTipOf(repo, "main"));
        Assert.AreEqual("origin/main", MergeTipOf(repo, "origin/main"));
    }

    [TestMethod]
    public async Task TestBranchWithNoRemoteGivesItself()
    {
        var repo = await LocalOnly().AugmentedRepoAsync();

        Assert.AreEqual("main", MergeTipOf(repo, "main"));
    }

    // Renaming a branch renames it in the branch choices as well, or the old name would come back
    // as a branch of its own (see BranchStructureServiceTest). The choices store nice names, so the
    // remote prefix of the name being renamed has to be trimmed off first.
    [TestMethod]
    public async Task TestRenameBranchRenamesTheBranchChoices()
    {
        var metaData = new MetaData();
        metaData.SetCommitBranch("abc123", "dev");
        metaData.SetBranched("def456", "main");
        var git = new FakeGit();
        var service = new BranchWriteService(git, new FakeMetaDataService(metaData));

        AssertOk(await service.RenameBranchAsync("origin/dev", "dev2", "/wd"));

        CollectionAssert.AreEqual(new[] { "origin/dev -> dev2" }, git.RenameCalls);
        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev2", name);
        Assert.IsTrue(metaData.TryGetCommitBranch("def456", out name, out _));
        Assert.AreEqual("main", name);
    }

    // Creating a branch from another branch remembers that the commit it starts at belongs to the
    // other branch, which is what keeps that commit off the new one when both point at it, under the
    // commit's full id, which is what the entries are looked up by.
    [TestMethod]
    public async Task TestCreateBranchFromBranchRemembersWhereItStarted()
    {
        var repo = await new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .AugmentedRepoAsync();
        var metaData = new MetaData();
        var git = new FakeGit();
        var service = new BranchWriteService(git, new FakeMetaDataService(metaData));

        AssertOk(await service.CreateBranchFromBranchAsync(repo, "feature", "origin/dev", false));

        CollectionAssert.AreEqual(new[] { $"feature at {RepoBuilder.Sid("d1")}" }, git.CreateBranchCalls);
        CollectionAssert.AreEqual(new[] { RepoBuilder.Sha("d1") }, metaData.CommitBranchById.Keys.ToArray());
        Assert.IsTrue(metaData.TryGetCommitBranch(RepoBuilder.Sha("d1"), out var name, out var isSetByUser));
        Assert.AreEqual("dev", name);
        Assert.IsFalse(isSetByUser, "Where a branch started is not a choice of the user");
    }

    static string MergeTipOf(Repo repo, string name) => BranchWriteService.MergeTipName(repo, repo.BranchByName[name]);

    static RepoBuilder Synced() =>
        new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true);

    static RepoBuilder Ahead() =>
        new RepoBuilder()
            .Commit("l1", "Local 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "c1", ahead: 1);

    static RepoBuilder Behind() =>
        new RepoBuilder()
            .Commit("r1", "Remote 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true, remoteTipCommit: "r1", behind: 1);

    static RepoBuilder Diverged() =>
        new RepoBuilder()
            .Commit("l2", "Local 2", "l1")
            .Commit("l1", "Local 1", "c2")
            .Commit("r1", "Remote 1", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l2", isCurrent: true, remoteTipCommit: "r1", ahead: 2, behind: 1);

    static RepoBuilder LocalOnly() =>
        new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("main", "c2", isCurrent: true);
}
