using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;
using GitOp = gmd.Git.GitOperation;

namespace gmdTest.Cui.RepoView;

// What Undo is called and when it can act, for the last change of a branch
[TestClass]
public class BranchUndoTest
{
    static string Id(string name) => RepoBuilder.Sha(name);

    static UndoStep Step(StepKind kind, string name = "", bool isPushed = false, bool isRedo = false) =>
        new UndoStep("dev", kind, name, Id("a2"), Id("a1"), isPushed, isRedo);

    [TestMethod]
    [DataRow(StepKind.Commit, "Fix the login", "Undo Commit 'Fix the login'")]
    [DataRow(StepKind.Amend, "Fix the login", "Undo Amend 'Fix the login'")]
    [DataRow(StepKind.Merge, "feature", "Undo Merge of 'feature'")]
    [DataRow(StepKind.Merge, "", "Undo Merge")]
    [DataRow(StepKind.CherryPick, "Fix", "Undo Cherry Pick 'Fix'")]
    [DataRow(StepKind.Revert, "Fix", "Undo Revert 'Fix'")]
    [DataRow(StepKind.Pull, "", "Undo Pull")]
    [DataRow(StepKind.Rebase, "", "Undo Rebase")]
    [DataRow(StepKind.Uncommit, "", "Undo Uncommit")]
    [DataRow(StepKind.Reset, "", "Undo Reset")]
    [DataRow(StepKind.Squash, "Squashed", "Undo Squash")]
    [DataRow(StepKind.Other, "", "Undo Last Change")]
    [DataRow(StepKind.Commit, "", "Undo Commit")]
    [DataRow(
        StepKind.Commit,
        "A subject that is far too long for a menu",
        "Undo Commit 'A subject that is far too lon…'"
    )]
    public void TestLabel(StepKind kind, string name, string label)
    {
        Assert.AreEqual(label, BranchUndo.Label(Step(kind, name)));
    }

    [TestMethod]
    public void TestLabelOfARedoAndOfNothing()
    {
        Assert.AreEqual("Redo Rebase", BranchUndo.Label(Step(StepKind.Rebase, isRedo: true)));
        Assert.AreEqual("Undo Last Change", BranchUndo.Label(null));
    }

    [TestMethod]
    public void TestWhatTheStatusLineSays()
    {
        Assert.AreEqual("Undoing the rebase on 'dev'", BranchUndo.Doing(Step(StepKind.Rebase)));
        Assert.AreEqual(
            "Undid the commit 'Fix' on 'dev': Undo again redoes it",
            BranchUndo.Done(Step(StepKind.Commit, "Fix"))
        );
        Assert.AreEqual(
            "Undid the merge of 'feature' on 'dev', origin still has it: Undo again redoes it",
            BranchUndo.Done(Step(StepKind.Merge, "feature", isPushed: true))
        );
        Assert.AreEqual(
            "Redid the amend of 'Fix' on 'dev'",
            BranchUndo.Done(Step(StepKind.Amend, "Fix", isRedo: true))
        );
    }

    // main with two commits, and the last change of each branch as its reflog has it
    static RepoBuilder Builder(string mainMessage = "commit: Two", int ahead = 1) =>
        new RepoBuilder()
            .Commit("a2", "Two", "a1")
            .Commit("a1", "One")
            .LocalBranch("main", "a2", isCurrent: true, remoteName: "origin/main", ahead: ahead)
            .RemoteBranch("origin/main", ahead == 0 ? "a2" : "a1")
            .Reflog("refs/heads/main", "a2", mainMessage)
            .Reflog("refs/heads/main", "a1", "commit (initial): One");

    static async Task<string> WhyNotAsync(RepoBuilder builder, string branchName = "main")
    {
        var repo = await builder.ViewRepoAsync();
        var branch = repo.BranchByName[branchName];
        return BranchUndo.WhyNot(repo, branch, BranchUndo.StepOf(repo, branch));
    }

    [TestMethod]
    public async Task TestUndoCanActOnTheCurrentBranch()
    {
        Assert.AreEqual("", await WhyNotAsync(Builder()));
    }

    // A remote branch is undone as its local branch
    [TestMethod]
    public async Task TestARemoteBranchIsUndoneAsItsLocalBranch()
    {
        var repo = await Builder().ViewRepoAsync();

        Assert.AreEqual("main", BranchUndo.StepOf(repo, repo.BranchByName["origin/main"])?.BranchName);
    }

    [TestMethod]
    public async Task TestNothingToUndo()
    {
        var builder = new RepoBuilder()
            .Commit("a1", "One")
            .LocalBranch("main", "a1", isCurrent: true)
            .Reflog("refs/heads/main", "a1", "commit (initial): One");

        Assert.AreEqual("Nothing to undo on 'main'", await WhyNotAsync(builder));
    }

    [TestMethod]
    public async Task TestNotWhileAnOperationIsInProgress()
    {
        Assert.AreEqual(Why.InProgress, await WhyNotAsync(Builder().WithStatus(conflicted: 1, operation: GitOp.Merge)));
    }

    // A change whose files go back too waits for a clean tree; a commit's undo leaves the files as
    // they are, so changes are no reason
    [TestMethod]
    public async Task TestChangesStopOnlyAnUndoThatMovesTheFiles()
    {
        Assert.AreEqual(Why.Changes, await WhyNotAsync(Builder("merge feature: Fast-forward").WithStatus(modified: 1)));
        Assert.AreEqual("", await WhyNotAsync(Builder().WithStatus(modified: 1)));
        Assert.AreEqual("", await WhyNotAsync(Builder("reset: moving to HEAD~1").WithStatus(modified: 1)));
    }

    // As Uncommit, a commit already pushed is not undone; anything else is, and so is a redo
    [TestMethod]
    public async Task TestAPushedCommitIsNotUndone()
    {
        Assert.AreEqual("Only a commit not yet pushed can be undone", await WhyNotAsync(Builder(ahead: 0)));
        Assert.AreEqual("", await WhyNotAsync(Builder("pull: Fast-forward", ahead: 0)));
        Assert.AreEqual(
            "",
            await WhyNotAsync(
                Builder("reset: moving to a1", ahead: 0)
                    .RecordedStep("main", StepKind.Commit, "Two", "a1", afterCommit: "a2", isRedo: true)
            )
        );
    }

    // A branch checked out in another worktree is moved only there
    [TestMethod]
    public async Task TestNotABranchInAnotherWorktree()
    {
        var builder = Builder()
            .LocalBranch("dev", "a2")
            .Reflog("refs/heads/dev", "a2", "commit: Two")
            .Reflog("refs/heads/dev", "a1", "branch: Created from main")
            .Worktree("/test/repo-dev", "dev");

        Assert.AreEqual("'dev' is checked out in another worktree", await WhyNotAsync(builder, "dev"));
    }
}
