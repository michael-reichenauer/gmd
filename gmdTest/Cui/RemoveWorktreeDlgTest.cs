using gmd.Cui;
using gmd.Server;

namespace gmdTest.Cui;

// The rules of the remove worktree dialog, which read nothing but the worktree
[TestClass]
public class RemoveWorktreeDlgTest
{
    static Worktree Linked(string branch = "dev", int changes = 0) =>
        new Worktree("/home/me/repo-dev", branch, "", false, false, branch == "", false, "", false, "", changes);

    // Deleting a merged branch loses nothing, so it is offered checked; an unmerged one is named
    // as such and left unchecked, so checking it is a deliberate choice
    [TestMethod]
    public void TestDeletingTheBranchIsOfferedCheckedOnlyWhenMerged()
    {
        Assert.IsTrue(RemoveWorktreeDlg.IsDeleteBranchChecked(Linked(), isUnmerged: false));
        Assert.AreEqual("Delete branch 'dev' too", RemoveWorktreeDlg.DeleteBranchLabel(Linked(), isUnmerged: false));

        Assert.IsFalse(RemoveWorktreeDlg.IsDeleteBranchChecked(Linked(), isUnmerged: true));
        Assert.AreEqual(
            "Delete branch 'dev' too (has unmerged commits)",
            RemoveWorktreeDlg.DeleteBranchLabel(Linked(), isUnmerged: true)
        );
    }

    [TestMethod]
    public void TestADetachedWorktreeHasNoBranchToDelete()
    {
        Assert.IsFalse(RemoveWorktreeDlg.IsDeleteBranchChecked(Linked(branch: ""), isUnmerged: false));
        Assert.AreEqual(
            "Delete branch too (none, detached)",
            RemoveWorktreeDlg.DeleteBranchLabel(Linked(branch: ""), false)
        );
    }

    [TestMethod]
    public void TestForceNamesTheChangesItWouldDiscard()
    {
        Assert.AreEqual("Force (discard 3 uncommitted changes)", RemoveWorktreeDlg.ForceLabel(Linked(changes: 3)));
        Assert.AreEqual("Force (no uncommitted changes to discard)", RemoveWorktreeDlg.ForceLabel(Linked()));
    }
}
