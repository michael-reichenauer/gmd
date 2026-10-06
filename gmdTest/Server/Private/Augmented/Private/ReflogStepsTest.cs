using gmd.Git;
using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// What a branch's reflog says its moves were, which is what Undo names and takes back. The messages
// are git's own, as the canary in GitIntegrationTest checks.
[TestClass]
public class ReflogStepsTest
{
    static string Id(string name) => RepoBuilder.Sha(name);

    [TestMethod]
    [DataRow("commit: Add login", StepKind.Commit, "Add login")]
    [DataRow("commit (initial): Initial", StepKind.Commit, "Initial")]
    [DataRow("commit (amend): Add login", StepKind.Amend, "Add login")]
    [DataRow("commit (merge): Merge branch 'feature' into dev", StepKind.Merge, "feature")]
    [DataRow("commit (merge): Merge remote-tracking branch 'origin/dev'", StepKind.Merge, "dev")]
    [DataRow("commit (merge): Merge pull request #12 from a/b", StepKind.Merge, "")]
    [DataRow("commit (cherry-pick): Fix", StepKind.CherryPick, "Fix")]
    [DataRow("cherry-pick: Fix", StepKind.CherryPick, "Fix")]
    [DataRow("revert: Revert \"Fix\"", StepKind.Revert, "Fix")]
    [DataRow("commit: Revert \"Fix\"", StepKind.Revert, "Fix")] // gmd's Revert Commit, then the commit
    [DataRow("merge feature: Fast-forward", StepKind.Merge, "feature")]
    [DataRow("merge origin/dev: Merge made by the 'ort' strategy.", StepKind.Merge, "dev")]
    [DataRow("pull: Fast-forward", StepKind.Pull, "")]
    [DataRow("pull: Merge made by the 'ort' strategy.", StepKind.Pull, "")]
    [DataRow("pull (finish): refs/heads/main onto 645b15feac8d", StepKind.Pull, "")]
    [DataRow("pull --rebase (finish): refs/heads/main onto 645b15feac8d", StepKind.Pull, "")]
    [DataRow("fetch origin dev:dev: fast-forward", StepKind.Pull, "")]
    [DataRow("rebase (finish): refs/heads/dev onto 645b15feac8d", StepKind.Rebase, "")]
    [DataRow("rebase -i (finish): refs/heads/dev onto 645b15feac8d", StepKind.Rebase, "")]
    [DataRow("reset: moving to HEAD~1", StepKind.Uncommit, "")]
    [DataRow("reset: moving to HEAD~", StepKind.Uncommit, "")]
    [DataRow("reset: moving to HEAD^", StepKind.Uncommit, "")]
    [DataRow("reset: moving to HEAD~3", StepKind.Uncommit, "")]
    [DataRow("reset: moving to 645b15feac8d", StepKind.Reset, "")]
    [DataRow("reset: moving to origin/main", StepKind.Reset, "")]
    [DataRow("undo: moving to 645b15feac8d", StepKind.Reset, "")]
    [DataRow("branch: Reset to main", StepKind.Reset, "")]
    [DataRow("branch: Created from HEAD", StepKind.Created, "")]
    [DataRow("clone: from https://example.com/repo.git", StepKind.Created, "")]
    [DataRow("Branch: renamed refs/heads/a to refs/heads/b", StepKind.Renamed, "")]
    [DataRow("Branch: copied refs/heads/a to refs/heads/b", StepKind.Renamed, "")]
    [DataRow("am: Patch", StepKind.Other, "")]
    [DataRow("", StepKind.Other, "")]
    [DataRow("something else entirely", StepKind.Other, "")]
    public void TestKindOf(string message, StepKind kind, string name)
    {
        Assert.AreEqual((kind, name), ReflogSteps.KindOf(message));
    }

    // A run of entries at the same commit is one move, made by the oldest of them; the newer ones,
    // e.g. a rename, moved nothing
    [TestMethod]
    public void TestCollapseKeepsTheOldestOfEachRun()
    {
        var entries = new[]
        {
            new ReflogEntry(Id("a2"), "refs/heads/dev", 0, "Branch: renamed refs/heads/x to refs/heads/dev"),
            new ReflogEntry(Id("a2"), "refs/heads/dev", 1, "commit: Two"),
            new ReflogEntry(Id("a1"), "refs/heads/dev", 2, "commit: One"),
        };

        CollectionAssert.AreEqual(new[] { entries[1], entries[2] }, ReflogSteps.Collapse(entries).ToArray());
    }

    // The entries are put in order by their index, however they were listed, and the list given is
    // left as it was, since the branch inference reads it too
    [TestMethod]
    public void TestCollapseOrdersByIndexWithoutChangingTheEntries()
    {
        List<ReflogEntry> entries =
        [
            new ReflogEntry(Id("a1"), "refs/heads/dev", 1, "commit: One"),
            new ReflogEntry(Id("a2"), "refs/heads/dev", 0, "commit: Two"),
        ];

        var collapsed = ReflogSteps.Collapse(entries);

        CollectionAssert.AreEqual(new[] { Id("a2"), Id("a1") }, collapsed.Select(e => e.Id).ToArray());
        Assert.AreEqual(Id("a1"), entries[0].Id);
    }

    [TestMethod]
    public void TestBranchReflogsAreTheLocalBranchesOnly()
    {
        var reflogs = ReflogSteps.BranchReflogs([
            new ReflogEntry(Id("a1"), "HEAD", 0, "commit: One"),
            new ReflogEntry(Id("a1"), "refs/heads/feature/x", 0, "commit: One"),
            new ReflogEntry(Id("a1"), "worktrees/w/HEAD", 0, "commit: One"),
        ]);

        CollectionAssert.AreEqual(new[] { "feature/x" }, reflogs.Keys.ToArray());
    }

    [TestMethod]
    [DataRow(StepKind.Commit, UndoMode.Mixed)]
    [DataRow(StepKind.Amend, UndoMode.Mixed)]
    [DataRow(StepKind.Uncommit, UndoMode.KeepWhenClean)]
    [DataRow(StepKind.Reset, UndoMode.KeepWhenClean)]
    [DataRow(StepKind.Merge, UndoMode.Keep)]
    [DataRow(StepKind.Pull, UndoMode.Keep)]
    [DataRow(StepKind.Rebase, UndoMode.Keep)]
    [DataRow(StepKind.Squash, UndoMode.Keep)]
    [DataRow(StepKind.Other, UndoMode.Keep)]
    public void TestModeOfEachKind(StepKind kind, UndoMode mode)
    {
        Assert.AreEqual(mode, new UndoStep("dev", kind, "", Id("a2"), Id("a1"), false, false).Mode);
    }

    static IReadOnlyDictionary<string, UndoStep> Steps(RepoBuilder builder) =>
        ReflogSteps.StepsByBranch(builder.ToGitRepo());

    // Two commits on main and one on a feature started from it, each in the branch's own reflog
    static RepoBuilder TwoBranches() =>
        new RepoBuilder()
            .LocalBranch("main", "a2", isCurrent: true)
            .LocalBranch("feature", "b1")
            .Reflog("refs/heads/main", "a2", "commit: Two")
            .Reflog("refs/heads/main", "a1", "commit (initial): One")
            .Reflog("refs/heads/feature", "b1", "commit: Feature")
            .Reflog("refs/heads/feature", "a1", "branch: Created from main");

    // Each local branch's last change is the newest move in its own reflog, back to where it was
    // before, whether it is checked out or not
    [TestMethod]
    public void TestTheLastChangeOfEachLocalBranch()
    {
        var steps = Steps(TwoBranches());

        Assert.AreEqual(new UndoStep("main", StepKind.Commit, "Two", Id("a2"), Id("a1"), false, false), steps["main"]);
        Assert.AreEqual(
            new UndoStep("feature", StepKind.Commit, "Feature", Id("b1"), Id("a1"), false, false),
            steps["feature"]
        );
    }

    // A branch whose reflog starts where it was made has nothing before it to go back to, and one
    // with no reflog has no last change at all
    [TestMethod]
    public void TestNoStepForABranchJustMadeOrWithoutAReflog()
    {
        var steps = Steps(
            new RepoBuilder()
                .LocalBranch("main", "a1", isCurrent: true)
                .LocalBranch("feature", "a1")
                .LocalBranch("other", "a1")
                .Reflog("refs/heads/main", "a1", "commit (initial): One")
                .Reflog("refs/heads/feature", "a1", "branch: Created from main")
        );

        Assert.AreEqual(0, steps.Count, string.Join(", ", steps.Values));
    }

    // The reflog is written with the branch, so a branch that is not where its reflog says was moved
    // by something that wrote none, and its reflog says nothing about how it got there
    [TestMethod]
    public void TestNoStepWhenTheBranchIsNotWhereItsReflogSays()
    {
        var steps = Steps(
            new RepoBuilder()
                .LocalBranch("main", "a3", isCurrent: true)
                .Reflog("refs/heads/main", "a2", "commit: Two")
                .Reflog("refs/heads/main", "a1", "commit (initial): One")
        );

        Assert.AreEqual(0, steps.Count);
    }

    // A rename moved nothing, so the change before it is the last one
    [TestMethod]
    public void TestARenameIsNoChange()
    {
        var steps = Steps(
            new RepoBuilder()
                .LocalBranch("dev", "a2", isCurrent: true)
                .Reflog("refs/heads/dev", "a2", "Branch: renamed refs/heads/main to refs/heads/dev")
                .Reflog("refs/heads/dev", "a2", "commit: Two")
                .Reflog("refs/heads/dev", "a1", "commit (initial): One")
        );

        Assert.AreEqual(new UndoStep("dev", StepKind.Commit, "Two", Id("a2"), Id("a1"), false, false), steps["dev"]);
    }

    // A tip on the remote branch is pushed, i.e. undoing it here leaves it there; one with commits
    // to push is not, and neither is one whose remote branch is gone
    [TestMethod]
    [DataRow(0, true, true)]
    [DataRow(1, true, false)]
    [DataRow(0, false, false)]
    public void TestWhetherTheTipIsPushed(int ahead, bool hasRemote, bool isPushed)
    {
        var builder = new RepoBuilder()
            .LocalBranch("main", "a2", isCurrent: true, remoteName: "origin/main", ahead: ahead)
            .Reflog("refs/heads/main", "a2", "commit: Two")
            .Reflog("refs/heads/main", "a1", "commit (initial): One");
        if (hasRemote)
            builder.RemoteBranch("origin/main", ahead == 0 ? "a2" : "a1");

        Assert.AreEqual(isPushed, Steps(builder)["main"].IsPushed);
    }

    // A squash is several moves; gmd records it, and the record names the last change for as long
    // as those moves are the newest in the reflog
    [TestMethod]
    public void TestARecordedSquashIsTheLastChangeWhileItIsTheLatest()
    {
        var builder = new RepoBuilder()
            .LocalBranch("main", "a3", isCurrent: true)
            .Reflog("refs/heads/main", "a3", "commit: Squashed")
            .Reflog("refs/heads/main", "a0", "reset: moving to a0")
            .Reflog("refs/heads/main", "a2", "commit: Two")
            .Reflog("refs/heads/main", "a1", "commit: One")
            .Reflog("refs/heads/main", "a0", "commit (initial): Zero")
            .RecordedStep("main", StepKind.Squash, "Squashed", "a2", moves: 2);

        Assert.AreEqual(
            new UndoStep("main", StepKind.Squash, "Squashed", Id("a3"), Id("a2"), false, false),
            Steps(builder)["main"]
        );
    }

    // Anything that moved the branch since shifts the moves, and the reflog's own latest is taken
    [TestMethod]
    public void TestARecordedSquashIsStaleOnceTheBranchMoved()
    {
        var builder = new RepoBuilder()
            .LocalBranch("main", "a4", isCurrent: true)
            .Reflog("refs/heads/main", "a4", "commit: Later")
            .Reflog("refs/heads/main", "a3", "commit: Squashed")
            .Reflog("refs/heads/main", "a0", "reset: moving to a0")
            .Reflog("refs/heads/main", "a2", "commit: Two")
            .RecordedStep("main", StepKind.Squash, "Squashed", "a2", moves: 2);

        Assert.AreEqual(
            new UndoStep("main", StepKind.Commit, "Later", Id("a4"), Id("a3"), false, false),
            Steps(builder)["main"]
        );
    }

    // An undo is a reset, which gmd records as the change it took back, so Undo again redoes it
    [TestMethod]
    public void TestARecordedUndoIsRedone()
    {
        var builder = new RepoBuilder()
            .LocalBranch("main", "a1", isCurrent: true)
            .Reflog("refs/heads/main", "a1", "reset: moving to a1")
            .Reflog("refs/heads/main", "a2", "commit: Two")
            .Reflog("refs/heads/main", "a1", "commit (initial): One")
            .RecordedStep("main", StepKind.Commit, "Two", "a2", afterCommit: "a1", isRedo: true);

        Assert.AreEqual(
            new UndoStep("main", StepKind.Commit, "Two", Id("a1"), Id("a2"), false, true),
            Steps(builder)["main"]
        );
    }

    // A record for where the branch is not is stale, however many moves it says
    [TestMethod]
    public void TestARecordWithAnotherEndIsStale()
    {
        var builder = new RepoBuilder()
            .LocalBranch("main", "a1", isCurrent: true)
            .Reflog("refs/heads/main", "a1", "reset: moving to a1")
            .Reflog("refs/heads/main", "a2", "commit: Two")
            .RecordedStep("main", StepKind.Commit, "Two", "a2", afterCommit: "a9", isRedo: true);

        Assert.AreEqual(StepKind.Reset, Steps(builder)["main"].Kind);
    }

    // The steps are carried all the way to the repo the UI renders, filtered or not
    [TestMethod]
    public async Task TestTheStepsReachTheViewRepo()
    {
        var builder = new RepoBuilder()
            .Commit("a2", "Two", "a1")
            .Commit("a1", "One")
            .LocalBranch("main", "a2", isCurrent: true)
            .Reflog("refs/heads/main", "a2", "commit: Two")
            .Reflog("refs/heads/main", "a1", "commit (initial): One");

        Assert.AreEqual(StepKind.Commit, (await builder.ViewRepoAsync()).UndoSteps["main"].Kind);
        Assert.AreEqual(StepKind.Commit, (await builder.FilteredViewRepoAsync("Two")).UndoSteps["main"].Kind);
    }
}
