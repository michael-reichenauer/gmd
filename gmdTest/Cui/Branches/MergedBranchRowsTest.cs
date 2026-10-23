using gmd.Cui.Branches;
using gmd.Cui.Common;
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
        string worktreePath = "",
        bool isLocal = true,
        bool isOnOrigin = false
    ) =>
        new(
            name,
            isLocal ? name : "",
            isOnOrigin ? $"origin/{name}" : "",
            "main",
            isByContent,
            RepoBuilder.Sha("f2"),
            "Add login form",
            "Ann Lee-Svensson",
            Time,
            isCurrent,
            worktreePath
        );

    static MergedBranch OnOrigin(string name = "search", bool isLocal = false) =>
        Merged(name, isByContent: false, isLocal: isLocal, isOnOrigin: true);

    [TestMethod]
    public void TestHeaderAndRows()
    {
        var width = MergedBranchRows.MinWidth;
        Assert.AreEqual(
            "Time              Branch              On      Merged into             Author        Subject     ",
            MergedBranchRows.Header(width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  feature             here    main, squash or rebase  Ann Lee-Sven  Add login fo",
            MergedBranchRows.Row(Merged(), Color.Blue, width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  fix ●               here    main                    Ann Lee-Sven  Add login fo",
            MergedBranchRows.Row(Merged("fix", isByContent: false, isCurrent: true), Color.Blue, width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  agent ⌂             here    main, squash or rebase  Ann Lee-Sven  Add login fo",
            MergedBranchRows.Row(Merged("agent", worktreePath: "/w/agent"), Color.Blue, width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  search              origin  main                    Ann Lee-Sven  Add login fo",
            MergedBranchRows.Row(OnOrigin(), Color.Blue, width).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  search              both    main                    Ann Lee-Sven  Add login fo",
            MergedBranchRows.Row(OnOrigin(isLocal: true), Color.Blue, width).ToString()
        );
    }

    // The branch's name is drawn in its color in the log, and where it is in yellow when that is origin
    [TestMethod]
    public void TestColors()
    {
        var row = MergedBranchRows.Row(OnOrigin(), Color.Green, MergedBranchRows.MinWidth);
        Assert.AreEqual(Color.Green, ColorOf(row, "search"));
        Assert.AreEqual(Color.Yellow, ColorOf(row, "origin"));
        Assert.AreEqual(Color.Dark, ColorOf(MergedBranchRows.Row(Merged(), Color.Green, 100), "here"));
    }

    static Color ColorOf(Text text, string part) => text.Fragments.First(f => f.Text.Trim().StartsWith(part)).Color;

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
        Assert.IsTrue(MergedBranchRows.CanDelete(OnOrigin()));
        Assert.AreEqual(
            "Delete deletes 'origin/search' on origin, for everyone, after asking: its commits are in 'main'",
            MergedBranchRows.Reason(OnOrigin())
        );
        Assert.AreEqual(
            "Delete deletes 'search' here and on origin, for everyone, after asking: its commits are in 'main'",
            MergedBranchRows.Reason(OnOrigin(isLocal: true))
        );
    }

    // Deleting a branch on origin deletes it for everyone, which is asked first, whether it has a local
    // branch or not
    [TestMethod]
    public void TestDeletingOneOnOrigin()
    {
        Assert.AreEqual(
            """
            Delete 'search' on origin?

              origin/search
              search, here

            This deletes it on origin, for everyone, not only here.
            Its commits are in 'main' already, and Restore Deleted
            Branch, in the Undo menu, pushes it back.
            """,
            MergedBranchRows.DeleteOnOriginQuestion(OnOrigin(isLocal: true))
        );
        StringAssert.StartsWith(
            MergedBranchRows.DeleteOnOriginQuestion(OnOrigin()),
            "Delete 'search' on origin?\n\n  origin/search\n\nThis"
        );
    }

    // Delete All deletes those on origin there too only when asked to, which the question says
    [TestMethod]
    public void TestDeletingThemAllWithSomeOnOrigin()
    {
        Assert.AreEqual(
            """
            Delete 3 merged branches?

              feature
              search (on origin)
              list (on origin)

            2 of them are on origin: Here Only leaves those as they are,
            Also on Origin deletes them there too, for everyone.

            Their work is in main or an integration branch already.
            Restore Deleted Branch, in the Undo menu, brings each back.
            """,
            MergedBranchRows.DeleteAllQuestion([Merged(), OnOrigin(), OnOrigin("list", isLocal: true)])
        );
        StringAssert.Contains(
            MergedBranchRows.DeleteAllQuestion([OnOrigin()]),
            "\n\nIt is on origin: Delete on Origin deletes it there, for everyone.\n\n"
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
