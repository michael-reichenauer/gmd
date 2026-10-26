using gmd.Cui.Common;
using gmd.Server;

namespace gmdTest.Cui.Common;

[TestClass]
public class ChangedFileTextTest
{
    // What Discard All Changes lists: every uncommitted file, a conflicted and a renamed one too, by
    // path, each with its letter
    [TestMethod]
    public void TestTheRowsAreEveryChangedFileByPath()
    {
        var status = new Status(
            1,
            1,
            1,
            1,
            1,
            GitOperation.Merge,
            "",
            "",
            "",
            0,
            0,
            true,
            ["d.txt"],
            ["a.txt"],
            ["c.txt"],
            [new ConflictedFile("e.txt", ConflictKind.BothModified)],
            ["b.txt"],
            ["f.txt"]
        );

        var rows = ChangedFileText.Rows(status).Select(r => r.ToString());

        CollectionAssert.AreEqual(
            new[] { "A a.txt", "R b.txt → f.txt", "D c.txt", "M d.txt", "C e.txt" },
            rows.ToArray()
        );
    }
}
