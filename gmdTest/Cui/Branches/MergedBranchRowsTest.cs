using gmd.Cui.Branches;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Branches;

// The rows of Delete Merged Branches, as text, what is said of the selected one, and what is asked and
// said of deleting them all
[TestClass]
public class MergedBranchRowsTest
{
    static readonly DateTime Time = new(2024, 10, 15, 12, 3, 0);

    static MergedBranch Merged(
        string name = "feature",
        bool isByContent = true,
        bool isCurrent = false,
        string worktreePath = ""
    ) => new(name, "main", isByContent, RepoBuilder.Sha("f2"), "Add login form", Time, isCurrent, worktreePath);

    [TestMethod]
    public void TestHeaderAndRows()
    {
        var width = MergedBranchRows.MinWidth;
        Assert.AreEqual(
            "Time              Branch                Merged into               Subject             ",
            MergedBranchRows.Header(width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  feature               main, squash or rebase    Add login form      ",
            MergedBranchRows.Row(Merged(), width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  fix ●                 main                      Add login form      ",
            MergedBranchRows.Row(Merged("fix", isByContent: false, isCurrent: true), width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  agent ⌂               main, squash or rebase    Add login form      ",
            MergedBranchRows.Row(Merged("agent", worktreePath: "/w/agent"), width).ToString()
        );
    }

    // Git deletes no branch checked out, here or in another worktree, which the line below the list
    // says what to do about
    [TestMethod]
    public void TestWhatDeleteDoesOrWhyNot()
    {
        Assert.IsTrue(MergedBranchRows.CanDelete(Merged()));
        Assert.AreEqual(
            "Delete deletes 'feature' here: squash or rebase merged into 'main' on origin",
            MergedBranchRows.Reason(Merged())
        );
        Assert.AreEqual(
            "Delete deletes 'feature' here: its commits are in 'main'",
            MergedBranchRows.Reason(Merged(isByContent: false))
        );
        Assert.IsFalse(MergedBranchRows.CanDelete(Merged(isCurrent: true)));
        Assert.AreEqual(
            "'feature' is checked out: switch to another branch, and then delete it",
            MergedBranchRows.Reason(Merged(isCurrent: true))
        );
        Assert.IsFalse(MergedBranchRows.CanDelete(Merged(worktreePath: "/w/feature")));
        Assert.AreEqual(
            "'feature' is checked out in another worktree: remove that in Worktrees (w), with the branch",
            MergedBranchRows.Reason(Merged(worktreePath: "/w/feature"))
        );
    }

    [TestMethod]
    public void TestDeletingThemAll()
    {
        Assert.AreEqual(
            """
            Delete 2 merged branches?

              feature
              fix

            1 branch checked out is left as it is.

            Their work is in main or an integration branch already.
            Restore Deleted Branch, in the Undo menu, brings each back.
            """,
            MergedBranchRows.DeleteAllQuestion([Merged(), Merged("fix"), Merged("agent", worktreePath: "/w/agent")])
        );
        StringAssert.StartsWith(
            MergedBranchRows.DeleteAllQuestion([Merged()]),
            "Delete 1 merged branch?\n\n  feature\n\nTheir"
        );
        Assert.AreEqual(
            "Deleted 3 merged branches: Restore Deleted Branch in the Undo menu brings them back",
            MergedBranchRows.Deleted(3)
        );
    }
}
