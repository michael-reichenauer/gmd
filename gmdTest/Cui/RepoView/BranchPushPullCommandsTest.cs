using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What can be pushed and pulled, and which branches 'push all branches' and 'pull all branches'
// act on. These are plain functions of the shown repo, i.e. of the HasLocalOnly / HasRemoteOnly
// flags ViewRepoCreater sets, so they are testable without a view.
//
// The case that matters most is the diverged branch, which has both flags: it can be pulled but
// not pushed, since git rejects a push that is not a fast-forward.
[TestClass]
public class BranchPushPullCommandsTest
{
    [TestMethod]
    public async Task TestSyncedBranchCanNeitherBePushedNorPulled()
    {
        var repo = await Synced().ViewRepoAsync();

        Assert.IsFalse(BranchPushPullCommands.CanPush(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPushCurrentBranch(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPull(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPullCurrentBranch(repo));
    }

    [TestMethod]
    public async Task TestBranchAheadCanBePushed()
    {
        var repo = await Ahead().ViewRepoAsync();

        Assert.IsTrue(BranchPushPullCommands.CanPush(repo));
        Assert.IsTrue(BranchPushPullCommands.CanPushCurrentBranch(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPull(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPullCurrentBranch(repo));
    }

    [TestMethod]
    public async Task TestBranchBehindCanBePulled()
    {
        var repo = await Behind().ViewRepoAsync();

        Assert.IsFalse(BranchPushPullCommands.CanPush(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPushCurrentBranch(repo));
        Assert.IsTrue(BranchPushPullCommands.CanPull(repo));
        Assert.IsTrue(BranchPushPullCommands.CanPullCurrentBranch(repo));
    }

    // The reason for the '&& !b.HasRemoteOnly' in CanPush: a diverged branch has to be pulled
    // first, so offering a push would only produce a rejected push
    [TestMethod]
    public async Task TestDivergedBranchCanBePulledButNotPushed()
    {
        var repo = await Diverged().ViewRepoAsync();

        Assert.IsFalse(BranchPushPullCommands.CanPush(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPushCurrentBranch(repo));
        Assert.IsTrue(BranchPushPullCommands.CanPull(repo));
        Assert.IsTrue(BranchPushPullCommands.CanPullCurrentBranch(repo));
    }

    // Uncommitted changes block both, since a pull would fail and a push would leave the local
    // work behind
    [TestMethod]
    public async Task TestUncommittedChangesBlockPushAndPull()
    {
        Assert.IsFalse(BranchPushPullCommands.CanPush(await Ahead().WithStatus(modified: 1).ViewRepoAsync()));
        Assert.IsFalse(
            BranchPushPullCommands.CanPushCurrentBranch(await Ahead().WithStatus(modified: 1).ViewRepoAsync())
        );
        Assert.IsFalse(BranchPushPullCommands.CanPull(await Behind().WithStatus(modified: 1).ViewRepoAsync()));
        Assert.IsFalse(
            BranchPushPullCommands.CanPullCurrentBranch(await Behind().WithStatus(modified: 1).ViewRepoAsync())
        );
    }

    // A branch with no remote is not published yet, so there is nothing to push it to
    [TestMethod]
    public async Task TestBranchWithNoRemoteCanNotBePushedOrPulled()
    {
        var repo = await LocalOnly().ViewRepoAsync();

        Assert.IsFalse(BranchPushPullCommands.CanPushCurrentBranch(repo));
        Assert.IsFalse(BranchPushPullCommands.CanPullCurrentBranch(repo));
    }

    // 'Push all branches' takes one row per branch, i.e. a branch and its remote count once, and
    // it is the remote name that ends up being pushed
    [TestMethod]
    public async Task TestPushAllTakesTheBranchesThatAreAheadOnlyOnce()
    {
        var repo = await Mixed().ViewRepoAsync(ShowBranches.AllActive);

        CollectionAssert.AreEqual(
            new[] { "origin/dev", "origin/feat" },
            BranchPushPullCommands.BranchesToPush(repo).Select(b => b.Name).ToArray()
        );
    }

    // 'Pull all branches' does take a diverged branch, unlike push, since pulling is what resolves
    // it. It leaves the current branch out, which PullAllBranches has already pulled by then.
    [TestMethod]
    public async Task TestPullAllTakesTheRemoteBranchesThatAreBehindExceptTheCurrent()
    {
        var repo = await Mixed().ViewRepoAsync(ShowBranches.AllActive);

        CollectionAssert.AreEqual(
            new[] { "origin/div" },
            BranchPushPullCommands.BranchesToPull(repo, "origin/main").Select(b => b.Name).ToArray()
        );
    }

    // The status is the callers' check, not the branch lists'
    [TestMethod]
    public async Task TestPushAllBranchListIgnoresUncommittedChanges()
    {
        var repo = await Ahead().WithStatus(modified: 1).ViewRepoAsync();

        CollectionAssert.AreEqual(
            new[] { "origin/main" },
            BranchPushPullCommands.BranchesToPush(repo).Select(b => b.Name).ToArray()
        );
    }

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
            .Commit("r1", "Remote 1", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "r1", behind: 1);

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

    // main is the current branch and behind, dev and feat are ahead, and div is diverged, i.e. one
    // branch for each of the four cases push all and pull all have to tell apart
    static RepoBuilder Mixed() =>
        new RepoBuilder()
            .Commit("v2", "Div local", "c2")
            .Commit("v1", "Div remote", "c2")
            .Commit("f1", "Feat work", "c2")
            .Commit("d1", "Dev work", "c2")
            .Commit("r1", "Remote 1", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "r1", behind: 1)
            .BranchWithRemote("dev", "d1", remoteTipCommit: "c2", ahead: 1)
            .BranchWithRemote("feat", "f1", remoteTipCommit: "c2", ahead: 1)
            .BranchWithRemote("div", "v2", remoteTipCommit: "v1", ahead: 1, behind: 1);
}
