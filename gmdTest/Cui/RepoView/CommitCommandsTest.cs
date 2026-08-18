using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// The predicates the commit menu greys its items out with. They are plain functions of the shown
// repo, so they are testable without a view — and worth testing, since Menu.OnCursorDown skips a
// disabled item, so a wrong answer here silently moves every item below it.
//
// 'Uncommit last commit' is the destructive one: it runs 'git reset HEAD~1', so being offered it
// when it should not be is what these are mostly about.
[TestClass]
public class CommitCommandsTest
{
    [TestMethod]
    public async Task TestACommitAheadOfTheRemoteCanBeUncommitted()
    {
        Assert.IsTrue(Commands(await Ahead().ViewRepoAsync()).CanUncommitLastCommit());
    }

    [TestMethod]
    public async Task TestUncommittedChangesBlockUncommittingACommitThatIsAhead()
    {
        Assert.IsFalse(Commands(await Ahead().WithStatus(modified: 1).ViewRepoAsync()).CanUncommitLastCommit());
    }

    // The top commit is on the remote branch, so there is nothing local to take back
    [TestMethod]
    public async Task TestACommitAlreadyOnTheRemoteCannotBeUncommitted()
    {
        Assert.IsFalse(Commands(await Synced().ViewRepoAsync()).CanUncommitLastCommit());
    }

    // A branch that was never pushed has no remote to compare against, so its top commit is always
    // local and can always be taken back
    [TestMethod]
    public async Task TestACommitOnABranchWithNoRemoteCanBeUncommitted()
    {
        Assert.IsTrue(Commands(await LocalOnly().ViewRepoAsync()).CanUncommitLastCommit());
    }

    // Regression test. This used to be offered, because of the operator precedence in
    // CanUncommitLastCommit:
    //
    //     Status.IsOk && c.IsAhead || (!b.IsRemote && b.RemoteName == "")
    //
    // '&&' binds tighter than '||', so the clean working tree was only required of the 'is ahead'
    // half and a branch that was never pushed skipped the check entirely — while its sibling
    // CanUndoCommit, a plain Status.IsOk, refused on the same repo.
    //
    // With changes in the tree the top row is not even a commit, it is the virtual uncommitted one,
    // so the reset would have taken back a row the user was not pointing at.
    [TestMethod]
    public async Task TestUncommittedChangesBlockUncommittingOnABranchWithNoRemoteToo()
    {
        var repo = await LocalOnly().WithStatus(modified: 1).ViewRepoAsync();

        Assert.IsFalse(Commands(repo).CanUncommitLastCommit());
        Assert.IsFalse(Commands(repo).CanUndoCommit(), "As its sibling always has");
    }

    // Characterization, and a separate wart of the same predicate: a repo with no commits still
    // offers it. The 'no commits in view' guard does not catch this, because the empty repo has a
    // virtual commit standing in for the ones it has not got, on a branch with no remote. Left
    // alone — git refuses the reset, so the cost is an item that should have been grey.
    [TestMethod]
    public async Task TestARepoWithNoCommitsStillOffersUncommit()
    {
        Assert.IsTrue(Commands(await new RepoBuilder().EmptyRepo().ViewRepoAsync()).CanUncommitLastCommit());
    }

    // 'Undo commit' rewrites the tree, so it needs one with nothing in it to lose

    [TestMethod]
    public async Task TestUndoCommitNeedsACleanWorkingTree()
    {
        Assert.IsTrue(Commands(await Synced().ViewRepoAsync()).CanUndoCommit());
        Assert.IsFalse(Commands(await Synced().WithStatus(modified: 1).ViewRepoAsync()).CanUndoCommit());
    }

    // 'Undo uncommitted' is the other way round: it needs something to undo
    [TestMethod]
    public async Task TestUndoUncommittedNeedsSomethingToUndo()
    {
        Assert.IsFalse(Commands(await Synced().ViewRepoAsync()).CanUndoUncommitted());
        Assert.IsTrue(Commands(await Synced().WithStatus(modified: 1).ViewRepoAsync()).CanUndoUncommitted());
    }

    // The predicates read nothing but repo.Repo, so the progress, server, dialogs and views the
    // constructor takes are never reached
    static CommitCommands Commands(Repo repo) =>
        new CommitCommands(null!, new FakeViewRepo(repo), null!, null!, null!, null!, null!, null!, null!, null!);

    // A local branch one commit ahead of its remote
    static RepoBuilder Ahead() =>
        new RepoBuilder()
            .Commit("l1", "Local work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "c1", ahead: 1);

    static RepoBuilder Synced() =>
        new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true);

    static RepoBuilder LocalOnly() =>
        new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("main", "c2", isCurrent: true);
}
