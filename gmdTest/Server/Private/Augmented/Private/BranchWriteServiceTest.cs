using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// A local branch and its remote branch are one branch to the user, but git has two, and they can
// point at different commits. So merging or rebasing onto 'main' has to work out whether 'main' or
// 'origin/main' is the one with the commits, which is what YoungestTipName answers. The rest of
// BranchWriteService is git commands, covered by the integration tests.
[TestClass]
public class BranchWriteServiceTest
{
    // The tips are the same, so both are the youngest — which means asking for either gives the
    // other. Harmless, since they point at the same commit, but it is why this is worth pinning.
    [TestMethod]
    public async Task TestSyncedPairGivesTheOtherOfThePair()
    {
        var repo = await Synced().AugmentedRepoAsync();

        Assert.AreEqual("origin/main", YoungestOf(repo, "main"));
        Assert.AreEqual("main", YoungestOf(repo, "origin/main"));
    }

    [TestMethod]
    public async Task TestLocalBranchAheadGivesTheLocalBranch()
    {
        var repo = await Ahead().AugmentedRepoAsync();

        Assert.AreEqual("main", YoungestOf(repo, "main"));
        Assert.AreEqual("main", YoungestOf(repo, "origin/main"));
    }

    [TestMethod]
    public async Task TestRemoteBranchAheadGivesTheRemoteBranch()
    {
        var repo = await Behind().AugmentedRepoAsync();

        Assert.AreEqual("origin/main", YoungestOf(repo, "main"));
        Assert.AreEqual("origin/main", YoungestOf(repo, "origin/main"));
    }

    // A diverged pair has commits on both sides, so the youngest tip wins rather than either side
    // being complete. Merging it is what brings the other side in.
    [TestMethod]
    public async Task TestDivergedPairGivesTheYoungestTip()
    {
        var repo = await Diverged().AugmentedRepoAsync();

        Assert.AreEqual("main", YoungestOf(repo, "main"));
        Assert.AreEqual("main", YoungestOf(repo, "origin/main"));
    }

    [TestMethod]
    public async Task TestBranchWithNoRemoteGivesItself()
    {
        var repo = await LocalOnly().AugmentedRepoAsync();

        Assert.AreEqual("main", YoungestOf(repo, "main"));
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
        var service = new BranchWriteService(git, new FakeFileMonitor(), new FakeMetaDataService(metaData));

        Assert.IsTrue(Try(out var e, await service.RenameBranchAsync("origin/dev", "dev2", "/wd")), $"{e}");

        CollectionAssert.AreEqual(new[] { "origin/dev -> dev2" }, git.RenameCalls);
        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev2", name);
        Assert.IsTrue(metaData.TryGetCommitBranch("def456", out name, out _));
        Assert.AreEqual("main", name);
    }

    static string YoungestOf(Repo repo, string name) =>
        BranchWriteService.YoungestTipName(repo, repo.BranchByName[name]);

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
