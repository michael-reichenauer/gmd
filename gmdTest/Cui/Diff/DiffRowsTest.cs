using gmd.Cui.Diff;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Diff;

// Where the cursor is as content, and back: what keeps it on the same line when the rows are built
// again with more or less context, or refreshed
[TestClass]
public class DiffRowsTest
{
    [TestMethod]
    public void TestAnAnchorIsTheCommitTheFileAndTheLine()
    {
        var rows = FileHistory();
        var index = LineIndex(rows, "c2", 2);

        Assert.AreEqual(new DiffAnchor(Id("c2"), "a.txt", 2), rows.AnchorAt(index));
    }

    // A file history shows the same path once per commit, and the anchor's file was looked for from
    // the top, so + and - on an older commit put the cursor back on the newest
    [TestMethod]
    public void TestAnAnchorInAnOlderCommitOfAFileHistoryIsFoundThere()
    {
        var rows = FileHistory();
        var index = LineIndex(rows, "c2", 2);

        Assert.AreEqual(index, rows.IndexOf(rows.AnchorAt(index)));
        Assert.AreNotEqual(LineIndex(rows, "c1", 2), index, "Not the same line of the newer commit");
    }

    [TestMethod]
    public void TestAnAnchorOfAFileNotThereIsNowhere()
    {
        Assert.AreEqual(-1, FileHistory().IndexOf(new DiffAnchor(Id("c2"), "gone.txt", 1)));
        Assert.AreEqual(-1, FileHistory().IndexOf(DiffAnchor.None));
    }

    // How far the diff scrolls sideways: to the end of its longest line, not of the filler of a side
    // that has no line there
    [TestMethod]
    public void TestTheLongestLineIsOfTextNotOfTheFiller()
    {
        var added = new FileDiff(
            "a.txt",
            "a.txt",
            false,
            false,
            DiffMode.DiffModified,
            [
                new SectionDiff(
                    "1,1 +1,2",
                    1,
                    1,
                    1,
                    2,
                    [new LineDiff(DiffMode.DiffSame, "one"), new LineDiff(DiffMode.DiffAdded, "two")]
                ),
            ]
        );
        var rows = new DiffRowService().ToDiffRows([
            new CommitDiff(Id("c1"), "Test", new DateTime(2025, 1, 1), "Add two", [added]),
        ]);
        Assert.IsTrue(rows.Rows.Any(r => r.Left == DiffRowService.NoLine), "The added line has nothing beside it");

        Assert.IsTrue(rows.MaxLength < 80, $"{rows.MaxLength}");
    }

    // ] and [ go to where a change starts, a run of changed lines or a conflict, from wherever the
    // cursor is; from inside a change, [ is its own start
    [TestMethod]
    public void TestTheNextAndPreviousChange()
    {
        var rows = TwoFiles();
        var (two, eleven, conflict, added) = (
            Row(rows, "TWO"),
            Row(rows, "ELEVEN"),
            ConflictRow(rows),
            Row(rows, "new")
        );

        Assert.AreEqual(two, rows.NextChange(0, 1), "From the top, a.txt's first change");
        Assert.AreEqual(eleven, rows.NextChange(two, 1), "Past the unchanged line, the next run");
        Assert.AreEqual(eleven, rows.NextChange(eleven + 1, -1), "From inside a change, its own start");
        Assert.AreEqual(conflict, rows.NextChange(eleven + 1, 1), "A conflict, from its marker");
        Assert.AreEqual(added, rows.NextChange(conflict, 1), "On into b.txt, a new file");
        Assert.AreEqual(-1, rows.NextChange(added, 1), "None below the last");
        Assert.AreEqual(-1, rows.NextChange(two, -1), "None above the first");
        Assert.AreEqual((3, 4), rows.ChangeNumber(conflict));
    }

    // } and { go to the header of a file, and say which of how many it is
    [TestMethod]
    public void TestTheNextAndPreviousFile()
    {
        var rows = TwoFiles();
        var (a, b) = (Header(rows, "a.txt"), Header(rows, "b.txt"));

        Assert.AreEqual(a, rows.NextFile(0, 1));
        Assert.AreEqual(b, rows.NextFile(Row(rows, "TWO"), 1));
        Assert.AreEqual(a, rows.NextFile(Row(rows, "TWO"), -1), "From inside a file, its own header");
        Assert.AreEqual(-1, rows.NextFile(b, 1));
        Assert.AreEqual((2, 2), rows.FileNumber(b));
    }

    // The header of the file a row is in, which the top row names once it has scrolled out of sight
    [TestMethod]
    public void TestTheFileHeaderARowIsUnder()
    {
        var rows = TwoFiles();
        var a = Header(rows, "a.txt");

        Assert.AreEqual(-1, rows.FileHeaderOf(1), "Above the first file");
        Assert.AreEqual(a, rows.FileHeaderOf(a), "On a file's header");
        Assert.AreEqual(a, rows.FileHeaderOf(Row(rows, "ELEVEN")), "In the file");
        Assert.AreEqual(Header(rows, "b.txt"), rows.FileHeaderOf(Row(rows, "new")), "In the next file");
    }

    // In a file history a commit's rows above its first file are in no file: the file above them is
    // the commit before's, which the top row named with this commit's title, and its counts
    [TestMethod]
    public void TestACommitsRowsAboveItsFilesAreInNoFile()
    {
        var rows = FileHistory();
        var c2 = rows.Rows.FindIndexBy(r => r.CommitId == Id("c2"));
        var c2File = c2 + rows.Rows.Skip(c2).FindIndexBy(r => r.FilePath != "");

        Assert.AreNotEqual(-1, rows.FileHeaderOf(c2 - 1), "The end of c1, in its file");
        Assert.AreEqual(-1, rows.FileHeaderOf(c2), "c2's title");
        Assert.AreEqual(-1, rows.FileHeaderOf(c2File - 1), "Below c2's title, above its file");
        Assert.AreEqual(c2File, rows.FileHeaderOf(c2File + 1), "In c2's file");
    }

    // a.txt with two changes and a conflict, the second change in two rows, and b.txt, a new file
    static DiffRows TwoFiles()
    {
        LineDiff[] lines =
        [
            new(DiffMode.DiffSame, "one"),
            new(DiffMode.DiffRemoved, "two"),
            new(DiffMode.DiffAdded, "TWO"),
            new(DiffMode.DiffSame, "three"),
            new(DiffMode.DiffRemoved, "eleven"),
            new(DiffMode.DiffAdded, "ELEVEN"),
            new(DiffMode.DiffAdded, "extra"),
            new(DiffMode.DiffSame, "twelve"),
            new(DiffMode.DiffConflictStart, "<<<<<<< HEAD"),
            new(DiffMode.DiffSame, "ours"),
            new(DiffMode.DiffConflictSplit, "======="),
            new(DiffMode.DiffSame, "theirs"),
            new(DiffMode.DiffConflictEnd, ">>>>>>> dev"),
        ];
        var a = new FileDiff("a.txt", "a.txt", false, false, DiffMode.DiffConflicts, [new("", 1, 9, 1, 13, lines)]);
        var b = new FileDiff(
            "b.txt",
            "b.txt",
            false,
            false,
            DiffMode.DiffAdded,
            [new("", 0, 0, 1, 2, [new(DiffMode.DiffAdded, "new"), new(DiffMode.DiffAdded, "lines")])]
        );
        return new DiffRowService().ToDiffRows(
            new CommitDiff(Id("c1"), "Test", new DateTime(2025, 1, 1), "Two files", [a, b])
        );
    }

    static int Row(DiffRows rows, string text) => rows.Rows.FindIndexBy(r => r.AddedText == text);

    static int Header(DiffRows rows, string path) => rows.Rows.FindIndexBy(r => r.FilePath == path);

    static int ConflictRow(DiffRows rows) => rows.Rows.FindIndexBy(r => r.IsConflictStart);

    // c1 is the newest, so it is shown first, as git log lists them
    static DiffRows FileHistory() =>
        new DiffRowService().ToDiffRows([CommitOf("c1", "newer"), CommitOf("c2", "older")]);

    static CommitDiff CommitOf(string name, string text) =>
        new(
            Id(name),
            "Test",
            new DateTime(2025, 1, 1),
            $"The {text} commit",
            [
                new FileDiff(
                    "a.txt",
                    "a.txt",
                    false,
                    false,
                    DiffMode.DiffModified,
                    [
                        new SectionDiff(
                            "1,3 +1,3",
                            1,
                            3,
                            1,
                            3,
                            [
                                new LineDiff(DiffMode.DiffSame, "one"),
                                new LineDiff(DiffMode.DiffRemoved, "two"),
                                new LineDiff(DiffMode.DiffAdded, $"two {text}"),
                                new LineDiff(DiffMode.DiffSame, "three"),
                            ]
                        ),
                    ]
                ),
            ]
        );

    static string Id(string name) => RepoBuilder.Sha(name);

    // The row of a line of the file, in the diff of a commit
    static int LineIndex(DiffRows rows, string commit, int lineNbr)
    {
        var start = rows.Rows.FindIndexBy(r => r.CommitId == Id(commit));
        return rows.Rows.Skip(start).FindIndexBy(r => r.LineNbr == lineNbr) + start;
    }
}
