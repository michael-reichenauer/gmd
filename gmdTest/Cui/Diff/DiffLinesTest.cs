using gmd.Cui.Diff;
using gmd.Server;

namespace gmdTest.Cui.Diff;

// Which changed lines a discard of lines is of: the change the cursor is on, or the rows selected
[TestClass]
public class DiffLinesTest
{
    // a.txt with two changes, two made TWO, and eleven made ELEVEN with a line added after it, and
    // b.txt, a new file
    static readonly DiffRows Rows = new DiffRowService().ToDiffRows(
        new CommitDiff(
            "",
            "",
            DateTime.MinValue,
            "",
            [
                File(
                    "a.txt",
                    DiffMode.DiffModified,
                    (DiffMode.DiffSame, "one"),
                    (DiffMode.DiffRemoved, "two"),
                    (DiffMode.DiffAdded, "TWO"),
                    (DiffMode.DiffSame, "three"),
                    (DiffMode.DiffRemoved, "eleven"),
                    (DiffMode.DiffAdded, "ELEVEN"),
                    (DiffMode.DiffAdded, "extra"),
                    (DiffMode.DiffSame, "twelve")
                ),
                File("b.txt", DiffMode.DiffAdded, (DiffMode.DiffAdded, "new"), (DiffMode.DiffAdded, "lines")),
            ]
        )
    );

    static FileDiff File(string path, DiffMode mode, params (DiffMode Mode, string Text)[] lines)
    {
        var (left, right) = (
            lines.Count(l => l.Mode != DiffMode.DiffAdded),
            lines.Count(l => l.Mode != DiffMode.DiffRemoved)
        );
        var section = new SectionDiff(
            "",
            mode == DiffMode.DiffAdded ? 0 : 1,
            left,
            1,
            right,
            [.. lines.Select(l => new LineDiff(l.Mode, l.Text))]
        );
        return new FileDiff(path, path, false, false, mode, [section]);
    }

    static int RowOf(string text) =>
        Rows.Rows.FindIndexBy(r => r.Right.ToString().Contains(text) || r.Left.ToString().Contains(text));

    // The row of a file's header, which names it
    static int HeaderOf(string path) => Rows.Rows.FindIndexBy(r => r.FilePath == path);

    static int ChangeRowOf(string text) => Rows.Rows.FindIndexBy(r => r.RemovedText == text || r.AddedText == text);

    static string Lines(IReadOnlyList<FileLines> files) =>
        string.Join(" | ", files.Select(f => $"{f.Path}: {string.Join(", ", f.Lines)}"));

    // The change is the run of changed rows around the cursor, its removed and added lines paired as
    // they are shown, the first removed line beside the first added one
    [TestMethod]
    public void TestTheChangeAtTheCursorIsItsRunOfRows()
    {
        var (first, count) = DiffLines.ChangeAt(Rows.Rows, ChangeRowOf("extra"));

        Assert.AreEqual((ChangeRowOf("ELEVEN"), 2), (first, count));
        Assert.AreEqual("a.txt: -4 eleven, +4 ELEVEN, +5 extra", Lines(DiffLines.Of(Rows, first, count)));
    }

    // A row of no change is on no change
    [TestMethod]
    public void TestAnUnchangedRowIsOnNoChange()
    {
        Assert.AreEqual(0, DiffLines.ChangeAt(Rows.Rows, RowOf("three")).Count);
        Assert.AreEqual(0, DiffLines.ChangeAt(Rows.Rows, HeaderOf("a.txt")).Count);
    }

    // On a file's header, the file is its rows up to the next one, and each row knows its file
    [TestMethod]
    public void TestAFileIsItsRowsFromItsHeader()
    {
        var (first, count) = DiffLines.FileAt(Rows.Rows, HeaderOf("a.txt"));

        Assert.AreEqual(HeaderOf("b.txt"), first + count, "Up to the next file");
        Assert.AreEqual(
            "a.txt: -2 two, +2 TWO, -4 eleven, +4 ELEVEN, +5 extra",
            Lines(DiffLines.Of(Rows, first, count))
        );
        Assert.AreEqual(0, DiffLines.FileAt(Rows.Rows, ChangeRowOf("TWO")).Count, "Not on a header");
        Assert.AreEqual("a.txt, b.txt", $"{Rows.FileAt(ChangeRowOf("extra"))}, {Rows.FileAt(ChangeRowOf("lines"))}");
    }

    // Rows selected have their changed lines, by file, and the unchanged ones and the headers between
    // them nothing; a new file's lines are drawn in one column, and are added lines
    [TestMethod]
    public void TestTheSelectedRowsHaveTheirChangedLinesByFile()
    {
        var first = ChangeRowOf("extra");
        var last = ChangeRowOf("lines");

        Assert.AreEqual(
            "a.txt: +5 extra | b.txt: +1 new, +2 lines",
            Lines(DiffLines.Of(Rows, first, last - first + 1))
        );
        Assert.AreEqual("", Lines(DiffLines.Of(Rows, RowOf("three"), 1)));
    }
}
