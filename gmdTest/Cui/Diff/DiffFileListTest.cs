using gmd.Cui.Diff;
using gmd.Server;

namespace gmdTest.Cui.Diff;

// The list of files beside a wide diff: what it lists, which item a row of the diff is in, how wide it
// is, and how its rows are drawn
[TestClass]
public class DiffFileListTest
{
    static FileDiff File(string path, DiffMode mode = DiffMode.DiffModified, int added = 1, int removed = 0)
    {
        var lines = Enumerable
            .Repeat(new LineDiff(DiffMode.DiffAdded, "new"), added)
            .Concat(Enumerable.Repeat(new LineDiff(DiffMode.DiffRemoved, "old"), removed))
            .ToList();
        return new FileDiff(path, path, false, false, mode, [new("", 1, removed, 1, added, lines)]);
    }

    static CommitDiff[] Diff(params FileDiff[] files) =>
        [new("c1", "Test", new DateTime(2025, 1, 1), "Subject", files)];

    static (DiffFileList, DiffRows) ListOf(CommitDiff[] diffs)
    {
        var rows = new DiffRowService().ToDiffRows(diffs);
        return (DiffFileList.Of(diffs, rows), rows);
    }

    static string Text(IReadOnlyList<gmd.Cui.Common.Text> rows) => string.Join("\n", rows.Select(r => r.ToString()));

    [TestMethod]
    public void TestTheFilesAreListedWithTheRowsTheyStartAt()
    {
        var (list, rows) = ListOf(Diff(File("a.txt"), File("src/b.cs", DiffMode.DiffAdded, 2)));

        CollectionAssert.AreEqual(
            new[] { "M a.txt  +1", "A src/b.cs  +2" },
            list.Items.Select(i => i.Text.ToString()).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "a.txt", "src/b.cs" },
            list.Items.Select(i => rows.Rows[i.Row].FilePath).ToArray(),
            "Each item goes to its file's header"
        );
        Assert.IsFalse(list.IsCommits);
    }

    // A row is in the item whose header is at or above it, and above the first in none
    [TestMethod]
    public void TestARowIsInTheFileAboveIt()
    {
        var (list, _) = ListOf(Diff(File("a.txt"), File("b.txt")));
        var (a, b) = (list.Items[0].Row, list.Items[1].Row);

        Assert.AreEqual(-1, list.IndexAt(0), "The commit's summary");
        Assert.AreEqual(0, list.IndexAt(a));
        Assert.AreEqual(0, list.IndexAt(b - 1));
        Assert.AreEqual(1, list.IndexAt(b + 1));
    }

    // Drawn on a terminal 150 wide or more, where the diff beside it is side by side still, and only
    // for more than one file; as wide as its widest file, within bounds
    [TestMethod]
    public void TestTheListIsDrawnOnAWideTerminalForMoreThanOneFile()
    {
        var (two, _) = ListOf(Diff(File("a.txt"), File("b.txt")));
        var (one, _) = ListOf(Diff(File("a.txt")));
        var (wide, _) = ListOf(Diff(File("a.txt"), File(new string('x', 60) + ".txt")));

        Assert.AreEqual(0, two.WidthFor(149));
        Assert.AreEqual(24, two.WidthFor(150), "At least");
        Assert.AreEqual(0, one.WidthFor(200));
        Assert.AreEqual(40, wide.WidthFor(200), "At most");
        Assert.AreEqual(37, wide.WidthFor(150), "A quarter of the terminal at most");
    }

    // The title, then a row per file, the one the cursor is in marked, a long path cut from the
    // front to keep its name, and the lines left out of the commit said with the diff's '╎'
    [TestMethod]
    public void TestTheRowsMarkTheCurrentFileAndCutALongPath()
    {
        var (list, _) = ListOf(Diff(File("a.txt"), File("some/long/folder/name/file.cs", added: 3, removed: 1)));

        Assert.AreEqual(
            """
            Files 2                 │
             M a.txt  +1 ╎2         │
            ▌M …name/file.cs  +3 -1 │
            """,
            Text(list.Rows(25, 1, false, path => path == "a.txt" ? 2 : 0))
        );
    }

    // Drawn at the width it asks for, less the view's scrollbar column, which the rows are given, the
    // widest file is whole: the scrollbar was not counted, and the widest was always cut
    [TestMethod]
    public void TestTheWidestFileIsWholeAtTheWidthOfTheList()
    {
        var (list, _) = ListOf(Diff(File("a.txt"), File("some/longer/path/file.cs")));

        var rows = Text(list.Rows(list.WidthFor(200) - 1, 0, false, _ => 0)).Split('\n');

        Assert.AreEqual(" M some/longer/path/file.cs  +1 │", rows[2]);
    }

    // Lines left out of the commit are counted whole however many there are, the path cut for them
    [TestMethod]
    public void TestManyLinesLeftOutAreCountedWhole()
    {
        var (list, _) = ListOf(Diff(File("a.txt"), File("some/longer/path/file.cs")));

        var rows = Text(list.Rows(list.WidthFor(200) - 1, 0, false, _ => 12)).Split('\n');

        Assert.AreEqual(" M …longer/path/file.cs  +1 ╎12 │", rows[2]);
    }

    // A file history lists its commits, each going to its commit's title
    [TestMethod]
    public void TestAFileHistoryListsItsCommits()
    {
        CommitDiff[] history =
        [
            new("c2", "Test", new DateTime(2025, 1, 2), "Second", [File("a.txt")]),
            new("c1", "Test", new DateTime(2025, 1, 1), "First", [File("a.txt")]),
        ];
        var (list, rows) = ListOf(history);

        Assert.IsTrue(list.IsCommits);
        CollectionAssert.AreEqual(
            new[] { "c2 Second", "c1 First" },
            list.Items.Select(i => i.Text.ToString()).ToArray()
        );
        CollectionAssert.AreEqual(new[] { "c2", "c1" }, list.Items.Select(i => rows.Rows[i.Row].CommitId).ToArray());
    }
}
