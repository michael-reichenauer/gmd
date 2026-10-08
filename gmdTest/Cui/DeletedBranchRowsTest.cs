using gmd.Cui;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui;

// The rows of the deleted branches dialog, as text, what is said of the selected one, and what the
// status line says of a delete and a restore
[TestClass]
public class DeletedBranchRowsTest
{
    static readonly DateTime Time = new(2024, 10, 15, 12, 3, 0);

    static DeletedBranch Deleted(bool isLocal, bool isRemote) =>
        new DeletedBranch(
            "feature",
            isLocal ? RepoBuilder.Sha("a2") : "",
            "origin/feature",
            isRemote ? RepoBuilder.Sha("b1") : "",
            "Feature work",
            Time
        );

    [TestMethod]
    public void TestHeaderAndRows()
    {
        Assert.AreEqual(
            "Time              Branch              Deleted        Subject             ",
            DeletedBranchRows.Header(DeletedBranchRows.MinWidth).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  feature             local, remote  Feature work        ",
            DeletedBranchRows.Row(Deleted(true, true), DeletedBranchRows.MinWidth).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  feature             local          Feature work        ",
            DeletedBranchRows.Row(Deleted(true, false), DeletedBranchRows.MinWidth).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  feature             remote         Feature work        ",
            DeletedBranchRows.Row(Deleted(false, true), DeletedBranchRows.MinWidth).ToString()
        );
    }

    [TestMethod]
    [DataRow(true, true, "Restore creates 'feature' at a20000 again, and pushes 'origin/feature' back to b10000")]
    [DataRow(true, false, "Restore creates 'feature' at a20000 again")]
    [DataRow(false, true, "Restore pushes 'origin/feature' back to b10000")]
    public void TestReason(bool isLocal, bool isRemote, string expected)
    {
        Assert.AreEqual(expected, DeletedBranchRows.Reason(Deleted(isLocal, isRemote)));
    }

    [TestMethod]
    public void TestDeleted()
    {
        Assert.AreEqual(
            "Deleted 'feature': Restore Deleted Branch in the Undo menu brings it back",
            DeletedBranchRows.Deleted("feature", false)
        );
        Assert.AreEqual(
            "Deleted 'feature' here and on origin: Restore Deleted Branch in the Undo menu brings it back",
            DeletedBranchRows.Deleted("feature", true)
        );
    }

    [TestMethod]
    [DataRow(true, true, "Restored 'feature' here and on origin")]
    [DataRow(true, false, "Restored 'feature'")]
    [DataRow(false, true, "Restored 'origin/feature'")]
    public void TestRestored(bool isLocal, bool isRemote, string expected)
    {
        Assert.AreEqual(expected, DeletedBranchRows.Restored(Deleted(true, true), isLocal, isRemote));
    }
}
