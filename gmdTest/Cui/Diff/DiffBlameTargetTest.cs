using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;

namespace gmdTest.Cui.Diff;

// What b blames in a diff: the file the cursor is in, in the version of the cursor's line, at that line
[TestClass]
public class DiffBlameTargetTest
{
    const string Id1 = "1111111111111111111111111111111111111111";
    const string Id2 = "2222222222222222222222222222222222222222";

    // 'one' and 'three' left as they were, 'two' removed with nothing in its place, 'four' added
    static FileDiff FileOf(string before, string after, bool isBinary = false) =>
        new(
            before,
            after,
            before != after,
            isBinary,
            DiffMode.DiffModified,
            [
                new(
                    "1 +1",
                    1,
                    3,
                    1,
                    3,
                    [
                        new(DiffMode.DiffSame, "one"),
                        new(DiffMode.DiffRemoved, "two"),
                        new(DiffMode.DiffSame, "three"),
                        new(DiffMode.DiffAdded, "four"),
                    ]
                ),
            ]
        );

    static CommitDiff DiffOf(string id, params FileDiff[] files) =>
        new(id, "Test", new DateTime(2025, 1, 1), "A commit", files);

    static DiffRows RowsOf(params CommitDiff[] diffs) =>
        new DiffRowService().ToDiffRows(diffs, new Dictionary<string, int>(), [], isOneColumn: false);

    // The row of the line with the text, on whichever side it is
    static int RowOf(DiffRows rows, string text, int from = 0) =>
        rows.Rows.Select((r, i) => (r, i)).First(x => x.i >= from && (Ends(x.r.Left, text) || Ends(x.r.Right, text))).i;

    static bool Ends(Text side, string text) => side.ToString().TrimEnd().EndsWith($" {text}");

    static BlameAt At(DiffRows rows, CommitDiff[] diffs, DiffSides? sides, int index) =>
        AssertOk(DiffBlameTarget.Of(rows, diffs, sides, index));

    // A line the commit left or added is of its version, a line it removed of the one before it, its
    // first parent, at that line's number there
    [TestMethod]
    public void TestALineIsBlamedInTheVersionItIsOf()
    {
        CommitDiff[] diffs = [DiffOf(Id1, FileOf("a.txt", "a.txt"))];
        var rows = RowsOf(diffs);

        Assert.AreEqual(new BlameAt("a.txt", Id1, 2), At(rows, diffs, null, RowOf(rows, "three")));
        Assert.AreEqual(new BlameAt("a.txt", Id1, 3), At(rows, diffs, null, RowOf(rows, "four")));
        Assert.AreEqual(new BlameAt("a.txt", $"{Id1}^", 2), At(rows, diffs, null, RowOf(rows, "two")));
    }

    // A removed line of a renamed file is blamed by the name the file had before
    [TestMethod]
    public void TestARemovedLineOfARenamedFileHasItsOldName()
    {
        CommitDiff[] diffs = [DiffOf(Id1, FileOf("old.txt", "new.txt"))];
        var rows = RowsOf(diffs);

        Assert.AreEqual(new BlameAt("old.txt", $"{Id1}^", 2), At(rows, diffs, null, RowOf(rows, "two")));
        Assert.AreEqual(new BlameAt("new.txt", Id1, 1), At(rows, diffs, null, RowOf(rows, "one")));
    }

    // A file's header blames from its first line
    [TestMethod]
    public void TestAFilesHeaderBlamesFromItsFirstLine()
    {
        CommitDiff[] diffs = [DiffOf(Id1, FileOf("a.txt", "a.txt"))];
        var rows = RowsOf(diffs);
        var header = rows.Rows.Select((r, i) => (r, i)).First(x => x.r.FilePath == "a.txt").i;

        Assert.AreEqual(new BlameAt("a.txt", Id1, 1), At(rows, diffs, null, header));
    }

    // A diff of no commit is blamed in the revisions its source says, here the uncommitted changes:
    // the working tree, and HEAD for a removed line; with none, there is nothing to blame
    [TestMethod]
    public void TestADiffOfNoCommitIsBlamedInItsSides()
    {
        CommitDiff[] diffs = [DiffOf("", FileOf("a.txt", "a.txt"))];
        var rows = RowsOf(diffs);

        Assert.AreEqual(new BlameAt("a.txt", "", 3), At(rows, diffs, DiffSides.Uncommitted, RowOf(rows, "four")));
        Assert.AreEqual(new BlameAt("a.txt", "HEAD", 2), At(rows, diffs, DiffSides.Uncommitted, RowOf(rows, "two")));
        Assert.IsInstanceOfType<Notice>(DiffBlameTarget.Of(rows, diffs, null, RowOf(rows, "four")).Value);
    }

    // In a file history each line is blamed in the commit it is under
    [TestMethod]
    public void TestAFileHistoryBlamesInTheCommitOfTheLine()
    {
        CommitDiff[] diffs = [DiffOf(Id2, FileOf("a.txt", "a.txt")), DiffOf(Id1, FileOf("a.txt", "a.txt"))];
        var rows = RowsOf(diffs);
        var secondCommit = rows.Rows.Select((r, i) => (r, i)).First(x => x.r.CommitId == Id1).i;

        Assert.AreEqual(new BlameAt("a.txt", Id2, 3), At(rows, diffs, null, RowOf(rows, "four")));
        Assert.AreEqual(new BlameAt("a.txt", Id1, 3), At(rows, diffs, null, RowOf(rows, "four", secondCommit)));
    }

    // Nothing is blamed above the first file, nor in a binary file, and each says why
    [TestMethod]
    public void TestWhereThereIsNothingToBlameItSaysWhy()
    {
        CommitDiff[] diffs = [DiffOf(Id1, FileOf("a.png", "a.png", isBinary: true))];
        var rows = RowsOf(diffs);
        var header = rows.Rows.Select((r, i) => (r, i)).First(x => x.r.FilePath == "a.png").i;

        Assert.AreEqual(
            "No file here to blame: b blames the file the cursor is in",
            AssertError(DiffBlameTarget.Of(rows, diffs, null, 0)).Message
        );
        Assert.AreEqual(
            "'a.png' is binary, with no lines to blame",
            AssertError(DiffBlameTarget.Of(rows, diffs, null, header)).Message
        );
    }
}
