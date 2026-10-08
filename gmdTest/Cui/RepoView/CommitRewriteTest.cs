using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;
using GitOp = gmd.Git.GitOperation;

namespace gmdTest.Cui.RepoView;

// Which commits Amend and Drop can rewrite: only ones not pushed yet, on the current branch, with no
// merge after them for the rebase to flatten and nothing else on them to be left on the old ones
[TestClass]
public class CommitRewriteTest
{
    static Commit C(Repo repo, string name) => repo.CommitById[RepoBuilder.Sha(name)];

    // main is on origin up to c2, and has c3 and c4 of its own
    static RepoBuilder Ahead() =>
        new RepoBuilder()
            .Commit("c4", "Fourth", "c3")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true, remoteTipCommit: "c2", ahead: 2);

    [TestMethod]
    public async Task TestOnlyACommitNotPushedYetIsRewritten()
    {
        var repo = await Ahead().ViewRepoAsync();

        Assert.IsTrue(CommitRewrite.IsNotPushed(repo, C(repo, "c3")));
        Assert.IsFalse(CommitRewrite.IsNotPushed(repo, C(repo, "c2")));
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "c3")));
        Assert.AreEqual("", CommitRewrite.WhyNotDrop(repo, C(repo, "c3")));
        Assert.AreEqual("Only a commit not yet pushed can be amended", CommitRewrite.WhyNotAmend(repo, C(repo, "c2")));
        Assert.AreEqual("Only a commit not yet pushed can be dropped", CommitRewrite.WhyNotDrop(repo, C(repo, "c2")));
    }

    // A branch never pushed has no commit ahead of a remote branch, and all of its commits are not
    // pushed yet. Amend used to go by 'ahead' alone, so it refused the last commit of such a branch,
    // which Uncommit took back.
    [TestMethod]
    public async Task TestTheCommitsOfABranchNeverPushedAreNotPushed()
    {
        var repo = await new RepoBuilder()
            .Commit("f2", "Feature two", "f1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f2", isCurrent: true)
            .ViewRepoAsync();

        Assert.IsFalse(C(repo, "f2").IsAhead);
        Assert.IsTrue(CommitRewrite.IsNotPushed(repo, C(repo, "f2")));
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "f1")));
        Assert.AreEqual("Only a commit not yet pushed can be amended", CommitRewrite.WhyNotAmend(repo, C(repo, "c1")));
    }

    [TestMethod]
    public async Task TestOnlyACommitOnTheCurrentBranchIsRewritten()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
            .LocalBranch("feature", "f1")
            .ViewRepoAsync("feature");

        Assert.AreEqual(
            "Only a commit on the current branch can be amended",
            CommitRewrite.WhyNotAmend(repo, C(repo, "f1"))
        );
    }

    // The rebase replays the commits after it as plain commits, which would flatten a merge among them
    [TestMethod]
    public async Task TestNotPastAMerge()
    {
        var repo = await new RepoBuilder()
            .Commit("f3", "Merge branch 'main' into feature", "f2", "c2")
            .Commit("f2", "Feature two", "f1")
            .Commit("c2", "Second", "c1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .LocalBranch("feature", "f3", isCurrent: true)
            .ViewRepoAsync();

        Assert.AreEqual(
            "There is a merge after this commit, which the rebase would flatten",
            CommitRewrite.WhyNotAmend(repo, C(repo, "f1"))
        );
        Assert.AreEqual(
            "A merge cannot be dropped: the rebase would flatten it",
            CommitRewrite.WhyNotDrop(repo, C(repo, "f3"))
        );
    }

    // Another branch on the commit or one after it would stay on the old commits: one built on it,
    // or one with no commits of its own at it
    [TestMethod]
    public async Task TestNotWithAnotherBranchOnIt()
    {
        static RepoBuilder Feature() =>
            new RepoBuilder()
                .Commit("s1", "Stacked", "f1")
                .Commit("f2", "Feature two", "f1")
                .Commit("f1", "Feature one", "c1")
                .Commit("c1", "Initial")
                .BranchWithRemote("main", "c1")
                .LocalBranch("feature", "f2", isCurrent: true);

        var stacked = await Feature().LocalBranch("stacked", "s1").ViewRepoAsync();
        Assert.AreEqual(
            "'stacked' is on this commit or one after it, and would keep the old ones",
            CommitRewrite.WhyNotAmend(stacked, C(stacked, "f1"))
        );
        Assert.AreEqual("", CommitRewrite.WhyNotAmend(stacked, C(stacked, "f2")), "Nothing is on f2 but feature");

        var copy = await new RepoBuilder()
            .Commit("f2", "Feature two", "f1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f2", isCurrent: true)
            .LocalBranch("copy", "f2")
            .ViewRepoAsync();
        Assert.AreEqual(
            "'copy' is on this commit or one after it, and would keep the old ones",
            CommitRewrite.WhyNotDrop(copy, C(copy, "f1"))
        );
    }

    [TestMethod]
    public async Task TestNotWithATagOnIt()
    {
        var repo = await new RepoBuilder()
            .Commit("f2", "Feature two", "f1")
            .Commit("f1", "Feature one", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("feature", "f2", isCurrent: true)
            .Tag("v1", "f2")
            .ViewRepoAsync();

        Assert.AreEqual(
            "The tag 'v1' is on this commit or one after it, and would keep the old ones",
            CommitRewrite.WhyNotAmend(repo, C(repo, "f1"))
        );
    }

    // The first commit can be amended, which rebases from the start, but there is nothing to drop it
    // onto. Changes in the working tree are put aside for an amend, but a drop moves the files.
    [TestMethod]
    public async Task TestTheFirstCommitAndChanges()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("main", "c2", isCurrent: true)
            .WithStatus(modified: 1)
            .ViewRepoAsync();

        Assert.AreEqual("", CommitRewrite.WhyNotAmend(repo, C(repo, "c1")));
        Assert.AreEqual("The first commit cannot be dropped", CommitRewrite.WhyNotDrop(repo, C(repo, "c1")));
        Assert.AreEqual(Why.Changes, CommitRewrite.WhyNotDrop(repo, C(repo, "c2")));
        Assert.AreEqual(
            "A commit is dropped: move to one first",
            CommitRewrite.WhyNotDrop(repo, repo.CommitById[Repo.UncommittedId])
        );
    }

    [TestMethod]
    public async Task TestNotWhileAnOperationIsInProgress()
    {
        var repo = await Ahead().WithStatus(conflicted: 1, operation: GitOp.Rebase).ViewRepoAsync();

        Assert.AreEqual(Why.InProgress, CommitRewrite.WhyNotAmend(repo, C(repo, "c3")));
    }
}
