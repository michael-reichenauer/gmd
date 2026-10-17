using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// A branch rewritten here after it was pushed, told by its own reflog: it was where origin is now,
// and something moved it off that commit, while origin still has the old version
[TestClass]
public class LocalRewritesTest
{
    static string Id(string name) => RepoBuilder.Sha(name);

    static IReadOnlyDictionary<string, LocalRewrite> Find(RepoBuilder builder) =>
        LocalRewrites.Find(builder.ToGitRepo(), new Dictionary<string, RemoteRewrite>());

    // feature had a1 <- a2 <- a3, pushed, and the three were squashed here into s1, on the same base
    static RepoBuilder Squashed(RepoBuilder builder) =>
        builder
            .Commit("s1", "Add login", "m1")
            .Commit("a3", "Validate", "a2")
            .Commit("a2", "Fix typo", "a1")
            .Commit("a1", "Add login", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "s1", isCurrent: true, remoteName: "origin/feature", ahead: 1, behind: 3)
            .RemoteBranch("origin/feature", "a3")
            .Reflog("refs/heads/feature", "s1", "rebase (finish): refs/heads/feature onto " + Id("s1"))
            .Reflog("refs/heads/feature", "a3", "commit: Validate")
            .Reflog("refs/heads/feature", "a2", "commit: Fix typo")
            .Reflog("refs/heads/feature", "a1", "commit: Add login")
            .Reflog("refs/heads/feature", "m1", "branch: Created from main");

    // gmd's squash, which it records, since the reflog says rebase
    [TestMethod]
    public void TestASquashOfPushedCommits()
    {
        var rewrite = Find(Squashed(new RepoBuilder()).RecordedStep("feature", StepKind.Squash, "Add login", "a3"))[
            "feature"
        ];

        Assert.AreEqual(
            new LocalRewrite(
                "feature",
                "origin/feature",
                Id("s1"),
                Id("a3"),
                StepKind.Squash,
                "Add login",
                1,
                rewrite.OldIds
            ),
            rewrite
        );
        CollectionAssert.AreEqual(new[] { Id("a3"), Id("a2"), Id("a1") }, rewrite.OldIds.ToArray());
    }

    // The same made outside gmd, e.g. by an agent's 'git rebase -i', is named as the reflog has it
    [TestMethod]
    public void TestARebaseMadeOutsideGmd()
    {
        var rewrite = Find(Squashed(new RepoBuilder()))["feature"];

        Assert.AreEqual((StepKind.Rebase, ""), (rewrite.Kind, rewrite.Name));
    }

    [TestMethod]
    public void TestAnAmendOfThePushedCommit()
    {
        var builder = new RepoBuilder()
            .Commit("b1", "Validate input", "a2")
            .Commit("a3", "Validate", "a2")
            .Commit("a2", "Fix typo", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "b1", isCurrent: true, remoteName: "origin/feature", ahead: 1, behind: 1)
            .RemoteBranch("origin/feature", "a3")
            .Reflog("refs/heads/feature", "b1", "commit (amend): Validate input")
            .Reflog("refs/heads/feature", "a3", "commit: Validate")
            .Reflog("refs/heads/feature", "a2", "commit: Fix typo")
            .Reflog("refs/heads/feature", "m1", "branch: Created from main");

        var rewrite = Find(builder)["feature"];

        Assert.AreEqual((StepKind.Amend, "Validate input", 1), (rewrite.Kind, rewrite.Name, rewrite.NewCount));
        CollectionAssert.AreEqual(new[] { Id("a3") }, rewrite.OldIds.ToArray());
    }

    // The rewrite is the move that took the branch off origin's tip, not a commit made on that tip
    // before it, which was squashed with the others, nor one made on the squash after it
    [TestMethod]
    public void TestTheMoveOffOriginsTipIsTheRewrite()
    {
        var builder = new RepoBuilder()
            .Commit("t1", "On top", "s1")
            .Commit("s1", "Add login", "m1")
            .Commit("a3", "Validate", "a2")
            .Commit("a2", "Fix typo", "a1")
            .Commit("a1", "Add login", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "t1", isCurrent: true, remoteName: "origin/feature", ahead: 2, behind: 3)
            .RemoteBranch("origin/feature", "a3")
            .Reflog("refs/heads/feature", "t1", "commit: On top")
            .Reflog("refs/heads/feature", "s1", "rebase (finish): refs/heads/feature onto " + Id("s1"))
            .Reflog("refs/heads/feature", "c1", "commit: Local, squashed in")
            .Reflog("refs/heads/feature", "a3", "commit: Validate")
            .Reflog("refs/heads/feature", "a2", "commit: Fix typo")
            .Reflog("refs/heads/feature", "a1", "commit: Add login")
            .Reflog("refs/heads/feature", "m1", "branch: Created from main")
            .RecordedStep("feature", StepKind.Squash, "Add login", "c1");

        var rewrite = Find(builder)["feature"];

        // c1 is no commit the log has, the squash replaced it, and gmd's record of the squash started
        // there, so the squash it is
        Assert.AreEqual((StepKind.Squash, "Add login", 2), (rewrite.Kind, rewrite.Name, rewrite.NewCount));
    }

    // A commit made here on origin's tip, which is on it still, is passed over to the rewrite after it
    [TestMethod]
    public void TestACommitOnOriginsTipIsPassedOver()
    {
        var builder = new RepoBuilder()
            .Commit("b2", "Local", "b1")
            .Commit("b1", "Validate, reset", "a2")
            .Commit("c1", "Local, reset away", "a3")
            .Commit("a3", "Validate", "a2")
            .Commit("a2", "Fix typo", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "b2", isCurrent: true, remoteName: "origin/feature", ahead: 2, behind: 1)
            .RemoteBranch("origin/feature", "a3")
            .LocalBranch("kept", "c1")
            .Reflog("refs/heads/feature", "b2", "commit: Local")
            .Reflog("refs/heads/feature", "b1", "commit: Validate, reset")
            .Reflog("refs/heads/feature", "a2", "reset: moving to " + Id("a2"))
            .Reflog("refs/heads/feature", "c1", "commit: Local, reset away")
            .Reflog("refs/heads/feature", "a3", "commit: Validate")
            .Reflog("refs/heads/feature", "m1", "branch: Created from main");

        Assert.AreEqual(StepKind.Reset, Find(builder)["feature"].Kind);
    }

    // New commits on both sides, e.g. someone pushed while a commit was made here: origin's tip is one
    // the branch never was at, so it is diverged as before
    [TestMethod]
    public void TestNewCommitsOnBothSidesAreNoRewrite()
    {
        var builder = new RepoBuilder()
            .Commit("c1", "Mine", "a2")
            .Commit("b1", "Theirs", "a2")
            .Commit("a2", "Fix typo", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "c1", isCurrent: true, remoteName: "origin/feature", ahead: 1, behind: 1)
            .RemoteBranch("origin/feature", "b1")
            .Reflog("refs/heads/feature", "c1", "commit: Mine")
            .Reflog("refs/heads/feature", "a2", "commit: Fix typo")
            .Reflog("refs/heads/feature", "m1", "branch: Created from main");

        Assert.AreEqual(0, Find(builder).Count);
    }

    // Origin moved on since the rewrite, e.g. someone pushed on the old version: its tip is then a
    // commit the branch never was at, and a force push would drop theirs, so nothing is told
    [TestMethod]
    public void TestCommitsPushedOnTheOldVersionSinceAreNoRewriteHere()
    {
        var builder = new RepoBuilder()
            .Commit("o1", "Theirs, on the old version", "a3")
            .Commit("s1", "Add login", "m1")
            .Commit("a3", "Validate", "a2")
            .Commit("a2", "Fix typo", "a1")
            .Commit("a1", "Add login", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "s1", isCurrent: true, remoteName: "origin/feature", ahead: 1, behind: 4)
            .RemoteBranch("origin/feature", "o1")
            .Reflog("refs/heads/feature", "s1", "rebase (finish): refs/heads/feature onto " + Id("s1"))
            .Reflog("refs/heads/feature", "a3", "commit: Validate")
            .Reflog("refs/heads/feature", "m1", "branch: Created from main");

        Assert.AreEqual(0, Find(builder).Count);
    }

    // With no reflog, e.g. an expired one, or one that does not say where the branch is, there is
    // nothing to tell by
    [TestMethod]
    public void TestNothingWithoutTheBranchsReflog()
    {
        var builder = new RepoBuilder()
            .Commit("s1", "Add login", "m1")
            .Commit("a3", "Validate", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "s1", isCurrent: true, remoteName: "origin/feature", ahead: 1, behind: 1)
            .RemoteBranch("origin/feature", "a3");

        Assert.AreEqual(0, Find(builder).Count);
        Assert.AreEqual(
            0,
            Find(builder.Reflog("refs/heads/feature", "a3", "commit: Validate")).Count,
            "The reflog does not say where the branch is"
        );
    }

    // A branch only behind was not rewritten, or its rewrite only dropped commits, which a pull brings
    // back as before
    [TestMethod]
    public void TestABranchOnlyBehindIsNoRewrite()
    {
        var builder = new RepoBuilder()
            .Commit("a3", "Validate", "a2")
            .Commit("a2", "Fix typo", "m1")
            .Commit("m1", "Initial")
            .BranchWithRemote("main", "m1")
            .LocalBranch("feature", "a2", isCurrent: true, remoteName: "origin/feature", behind: 1)
            .RemoteBranch("origin/feature", "a3")
            .Reflog("refs/heads/feature", "a2", "reset: moving to HEAD~1")
            .Reflog("refs/heads/feature", "a3", "commit: Validate")
            .Reflog("refs/heads/feature", "a2", "commit: Fix typo");

        Assert.AreEqual(0, Find(builder).Count);
    }

    // A branch whose remote branch a force push rewrote is that, e.g. gmd's Rebase pushed and then
    // undone here, which Restore origin takes back
    [TestMethod]
    public void TestARemoteRewriteIsNotAlsoOneHere()
    {
        var builder = Squashed(new RepoBuilder());
        var remote = new RemoteRewrite("feature", "origin/feature", "", "", "", "", 0, 0, 0, [], true, true);

        Assert.AreEqual(
            0,
            LocalRewrites
                .Find(builder.ToGitRepo(), new Dictionary<string, RemoteRewrite> { ["feature"] = remote })
                .Count
        );
    }
}
