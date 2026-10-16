using gmd.Cui.Recovery;
using gmd.Server;

namespace gmdTest.Cui.Recovery;

// The rows of Restore Discarded Changes, and what the status line says of a restore
[TestClass]
public class DiscardedRowsTest
{
    static readonly DateTime Time = new(2026, 10, 16, 14, 2, 0);
    static readonly DiscardedChange Lines = new("1111", "3 lines of a.txt", Time, false);
    static readonly DiscardedChange All = new("2222", "All changes", Time, false);
    static readonly DiscardedChange Stash = new("3333", "On main: experiment", Time, true);

    // The time and what was discarded, a stash said to be one
    [TestMethod]
    public void TestARowIsTheTimeAndWhatWasDiscarded()
    {
        Assert.AreEqual(
            """
            Time              Discarded
            2026-10-16 14:02  3 lines of a.txt
            2026-10-16 14:02  Stash  On main: experiment
            """,
            string.Join(
                "\n",
                new[] { DiscardedRows.Header(50), DiscardedRows.Row(Lines, 50), DiscardedRows.Row(Stash, 50) }.Select(
                    t => t.ToString().TrimEnd()
                )
            )
        );
    }

    [TestMethod]
    public void TestARestoreIsSaid()
    {
        Assert.AreEqual("Restored 3 lines of a.txt", DiscardedRows.Restored(Lines));
        Assert.AreEqual("Restored all changes", DiscardedRows.Restored(All));
        Assert.AreEqual("Restored the stash 'On main: experiment'", DiscardedRows.Restored(Stash));
        StringAssert.Contains(DiscardedRows.Reason(Stash), "list of stashes");
    }
}
