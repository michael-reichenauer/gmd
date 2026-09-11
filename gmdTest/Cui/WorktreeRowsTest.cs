using gmd.Cui;
using gmd.Cui.Common;
using gmd.Server;

namespace gmdTest.Cui;

// The rows of the worktrees dialog and its enable rules, which read nothing but the worktrees
[TestClass]
public class WorktreeRowsTest
{
    const string Sha = "3f2a1c0000000000000000000000000000000000";

    static Worktree MainWorktree(bool isCurrent = true) =>
        new Worktree("/home/me/repo", "main", Sha, true, isCurrent, false, false, "", false, "", 0);

    static Worktree LinkedWorktree(
        string path = "/home/me/repo-dev",
        string branch = "dev",
        int changes = 0,
        bool isCurrent = false,
        bool isLocked = false,
        string lockReason = "",
        bool isPrunable = false,
        bool isDetached = false
    ) =>
        new Worktree(
            path,
            isDetached ? "" : branch,
            Sha,
            false,
            isCurrent,
            isDetached,
            isLocked,
            lockReason,
            isPrunable,
            isPrunable ? "gitdir file points to non-existent location" : "",
            changes
        );

    static string Row(Worktree w, bool isUnmerged = false, int width = 80) =>
        WorktreeRows.Row(w, Color.Green, isUnmerged, width).ToString();

    [TestMethod]
    public void TestTheHeaderNamesTheColumns()
    {
        var header = WorktreeRows.Header(80).ToString();

        Assert.AreEqual("  Kind    Branch                Changes State    Merged    Path", header.TrimEnd());
        Assert.AreEqual(80, header.Length, "Padded to the width, so the highlight covers the row");
    }

    [TestMethod]
    public void TestTheCurrentMainWorktreeIsMarked()
    {
        Assert.AreEqual(
            "● main    main                   -                         /home/me/repo",
            Row(MainWorktree())
        );
    }

    [TestMethod]
    public void TestALinkedWorktreeWithChangesInUseAndUnmerged()
    {
        Assert.AreEqual(
            "  linked  feature/login         ©2      in use   unmerged  /home/me/repo-feature",
            Row(LinkedWorktree("/home/me/repo-feature", "feature/login", changes: 2, isLocked: true), isUnmerged: true)
        );
    }

    [TestMethod]
    public void TestAMissingWorktreeHasNoChangesToShow()
    {
        Assert.AreEqual(
            "  linked  hotfix-1.2                    missing  merged    /home/me/repo-hot",
            Row(LinkedWorktree("/home/me/repo-hot", "hotfix-1.2", changes: -1, isPrunable: true))
        );
    }

    [TestMethod]
    public void TestADetachedWorktreeNamesItsCommitAndHasNoMergedColumn()
    {
        Assert.AreEqual(
            "  linked  (detached 3f2a1c)      -                         /tmp/review",
            Row(LinkedWorktree("/tmp/review", isDetached: true))
        );
    }

    // The end of the path is what tells worktrees apart, so that is what is kept
    [TestMethod]
    public void TestALongPathIsCutFromTheStart()
    {
        var row = Row(LinkedWorktree("/home/someone/projects/repository/.claude/worktrees/dev"), width: 80);

        StringAssert.EndsWith(row, "┅claude/worktrees/dev");
        Assert.AreEqual(80, row.Length);
    }

    [TestMethod]
    public void TestTheReasonIsWhyAWorktreeIsInUseOrMissing()
    {
        Assert.AreEqual("", WorktreeRows.Reason(LinkedWorktree()));
        Assert.AreEqual(
            "In use: claude session 42",
            WorktreeRows.Reason(LinkedWorktree(isLocked: true, lockReason: "claude session 42"))
        );
        Assert.AreEqual("In use: locked", WorktreeRows.Reason(LinkedWorktree(isLocked: true)));
        Assert.AreEqual(
            "Missing: gitdir file points to non-existent location",
            WorktreeRows.Reason(LinkedWorktree(isPrunable: true))
        );
    }

    // What can be done: the current one is already open, a missing one cannot be opened; the main
    // and the current cannot be removed, nor one in use; prune is for when something is missing
    [TestMethod]
    public void TestWhatCanBeDoneWithEachWorktree()
    {
        Assert.IsFalse(WorktreeRows.CanOpen(MainWorktree()));
        Assert.IsTrue(WorktreeRows.CanOpen(MainWorktree(isCurrent: false)));
        Assert.IsTrue(WorktreeRows.CanOpen(LinkedWorktree()));
        Assert.IsFalse(WorktreeRows.CanOpen(LinkedWorktree(isPrunable: true)));

        Assert.IsFalse(WorktreeRows.CanRemove(MainWorktree()));
        Assert.IsFalse(WorktreeRows.CanRemove(MainWorktree(isCurrent: false)));
        Assert.IsTrue(WorktreeRows.CanRemove(LinkedWorktree()));
        Assert.IsFalse(WorktreeRows.CanRemove(LinkedWorktree(isCurrent: true)));
        Assert.IsFalse(WorktreeRows.CanRemove(LinkedWorktree(isLocked: true)));
        Assert.IsFalse(
            WorktreeRows.CanRemove(LinkedWorktree(isPrunable: true)),
            "A missing one is pruned, not removed"
        );

        Assert.IsFalse(WorktreeRows.CanPrune([MainWorktree(), LinkedWorktree()]));
        Assert.IsTrue(WorktreeRows.CanPrune([MainWorktree(), LinkedWorktree(isPrunable: true)]));
    }
}
