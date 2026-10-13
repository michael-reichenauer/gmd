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
