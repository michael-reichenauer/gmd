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
}
