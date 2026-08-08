using gmd.Cui.Diff;
using gmd.Server;
using CuiDiffService = gmd.Cui.Diff.DiffService;

namespace gmdTest.Cui.Diff;

[TestClass]
public class DiffServiceTest
{
    // Builds a file diff of one section whose lines are '<mode> <text>', so a test reads as the
    // hunk it renders. Line numbers start at 1 on both sides, as a first hunk's would.
    static FileDiff FileDiffOf(string path, params (DiffMode Mode, string Text)[] lines)
    {
        var lineDiffs = lines.Select(l => new LineDiff(l.Mode, l.Text)).ToList();
        var section = new SectionDiff("1,1 +1,1", 1, lines.Length, 1, lines.Length, lineDiffs);

        return new FileDiff(path, path, false, false, DiffMode.DiffModified, [section]);
    }

    static CommitDiff CommitDiffOf(params FileDiff[] fileDiffs) =>
        new CommitDiff("abc123", "Test", new DateTime(2025, 1, 1), "A commit", fileDiffs);

    // A file of unchanged lines with one modified in the middle, the shape a wider context grows
    static FileDiff ContextFileOf(string path, int lineCount)
    {
        var lines = Enumerable
            .Range(1, lineCount)
            .Select(i =>
                i == lineCount / 2 ? (DiffMode.DiffAdded, $"line {i} changed") : (DiffMode.DiffSame, $"line {i}")
            )
            .ToArray();

        return FileDiffOf(path, lines);
    }

    static string TextOfRow(DiffRow row) => row.Left.ToString().TrimEnd();

    // The file header rows, which is where the context of each file is named
    static IReadOnlyList<string> HeaderRowsOf(DiffRows rows) =>
        rows.Rows.Where(r => r.FilePath != "").Select(TextOfRow).ToList();

    // Stripping the line number gutter, which is what a copy out of the view does

    [TestMethod]
    public void TestARowWithNoLineNumberKeepsAllOfItsText()
    {
        Assert.AreEqual("=== Start of conflict", CuiDiffService.WithoutLineNbr("=== Start of conflict", 0));
    }

    [TestMethod]
    public void TestTheLineNumberGutterIsStripped()
    {
        Assert.AreEqual("some code", CuiDiffService.WithoutLineNbr("  12┃some code", 12));
    }

    // The gutter is the number right aligned in 4, so it grows once the number no longer fits,
    // which whole file context on a large file reaches
    [TestMethod]
    public void TestTheGutterOfALargeLineNumberIsWider()
    {
        Assert.AreEqual("some code", CuiDiffService.WithoutLineNbr("12345┃some code", 12345));
    }

    [TestMethod]
    public void TestARowOfOnlyAGutterYieldsAnEmptyLine()
    {
        Assert.AreEqual("", CuiDiffService.WithoutLineNbr("  12┃", 12));
    }

    // Stepping the context levels

    [TestMethod]
    public void TestContextStepsUpToWholeFileAndBackDown()
    {
        Assert.AreEqual(15, DiffContext.Step(DiffContext.Default, 1));
        Assert.AreEqual(DiffContext.WholeFile, DiffContext.Step(15, 1));
        Assert.AreEqual(15, DiffContext.Step(DiffContext.WholeFile, -1));
        Assert.AreEqual(DiffContext.Default, DiffContext.Step(15, -1));
    }

    [TestMethod]
    public void TestContextStaysPutAtEitherEnd()
    {
        Assert.AreEqual(DiffContext.Default, DiffContext.Step(DiffContext.Default, -1));
        Assert.AreEqual(DiffContext.WholeFile, DiffContext.Step(DiffContext.WholeFile, 1));
    }

    // A context that is not one of the levels, which nothing produces today, restarts at the default
    [TestMethod]
    public void TestAnUnknownContextFallsBackToTheDefault()
    {
        Assert.AreEqual(DiffContext.Default, DiffContext.Step(7, 1));
    }

    // The header says what the file is showing, the menu item says what picking it would show, and
    // the whole file is worded the same either way
    [TestMethod]
    public void TestALevelIsNamedForTheHeaderAndForAMenuItem()
    {
        Assert.AreEqual("(context 15)", DiffContext.ToText(15));
        Assert.AreEqual("15 lines", DiffContext.ToItemText(15));

        Assert.AreEqual("(whole file)", DiffContext.ToText(DiffContext.WholeFile));
        Assert.AreEqual("whole file", DiffContext.ToItemText(DiffContext.WholeFile));
    }

    // The file header, which is the only place the view says what context a file is shown with

    [TestMethod]
    public void TestAFileAtTheDefaultContextSaysNothingAboutIt()
    {
        var rows = new CuiDiffService().ToDiffRows(
            [CommitDiffOf(FileDiffOf("a.txt", (DiffMode.DiffSame, "one")))],
            new Dictionary<string, int> { ["a.txt"] = DiffContext.Default }
        );

        CollectionAssert.AreEqual(new[] { "Modified: a.txt" }, HeaderRowsOf(rows).ToArray());
    }

    [TestMethod]
    public void TestOnlyTheSteppedFileNamesItsContext()
    {
        var rows = new CuiDiffService().ToDiffRows(
            [
                CommitDiffOf(
                    FileDiffOf("a.txt", (DiffMode.DiffSame, "one")),
                    FileDiffOf("b.txt", (DiffMode.DiffSame, "x"))
                ),
            ],
            new Dictionary<string, int> { ["a.txt"] = 15 }
        );

        CollectionAssert.AreEqual(
            new[] { "Modified: a.txt  (context 15)", "Modified: b.txt" },
            HeaderRowsOf(rows).ToArray()
        );
    }

    [TestMethod]
    public void TestWholeFileContextIsNamedRatherThanNumbered()
    {
        var rows = new CuiDiffService().ToDiffRows(
            [CommitDiffOf(FileDiffOf("a.txt", (DiffMode.DiffSame, "one")))],
            new Dictionary<string, int> { ["a.txt"] = DiffContext.WholeFile }
        );

        CollectionAssert.AreEqual(new[] { "Modified: a.txt  (whole file)" }, HeaderRowsOf(rows).ToArray());
    }

    // Splicing one file of a re-fetched diff into the one on screen

    [TestMethod]
    public void TestOnlyTheNamedFileIsTakenFromTheFetchedDiff()
    {
        var shown = new[] { CommitDiffOf(ContextFileOf("a.txt", 4), ContextFileOf("b.txt", 4)) };
        var fetched = new[] { CommitDiffOf(ContextFileOf("a.txt", 20), ContextFileOf("b.txt", 20)) };

        var spliced = CuiDiffService.ReplaceFileDiff(shown, "a.txt", fetched);

        Assert.AreEqual(20, spliced[0].FileDiffs[0].SectionDiffs[0].LineDiffs.Count, "a.txt was widened");
        Assert.AreEqual(4, spliced[0].FileDiffs[1].SectionDiffs[0].LineDiffs.Count, "b.txt was left alone");
    }

    [TestMethod]
    public void TestTheOrderOfTheFilesIsKept()
    {
        var shown = new[] { CommitDiffOf(FileDiffOf("a.txt"), FileDiffOf("b.txt"), FileDiffOf("c.txt")) };
        var fetched = new[] { CommitDiffOf(FileDiffOf("a.txt"), FileDiffOf("b.txt"), FileDiffOf("c.txt")) };

        var spliced = CuiDiffService.ReplaceFileDiff(shown, "b.txt", fetched);

        CollectionAssert.AreEqual(
            new[] { "a.txt", "b.txt", "c.txt" },
            spliced[0].FileDiffs.Select(fd => fd.PathAfter).ToArray()
        );
    }

    // The working tree can change between the two fetches, so the file may simply not be there
    [TestMethod]
    public void TestAFileMissingFromTheFetchedDiffLeavesTheShownDiffAlone()
    {
        var shown = new[] { CommitDiffOf(ContextFileOf("a.txt", 4)) };
        var fetched = new[] { CommitDiffOf(ContextFileOf("b.txt", 20)) };

        var spliced = CuiDiffService.ReplaceFileDiff(shown, "a.txt", fetched);

        Assert.AreSame(shown, spliced);
    }

    // Full file history is one file over many commits, so there is no single file to pick out
    [TestMethod]
    public void TestAMultiCommitDiffIsReplacedWholesale()
    {
        var shown = new[] { CommitDiffOf(ContextFileOf("a.txt", 4)), CommitDiffOf(ContextFileOf("a.txt", 4)) };
        var fetched = new[] { CommitDiffOf(ContextFileOf("a.txt", 20)), CommitDiffOf(ContextFileOf("a.txt", 20)) };

        Assert.AreSame(fetched, CuiDiffService.ReplaceFileDiff(shown, "a.txt", fetched));
    }

    // Finding the row to put the cursor back on after the file was redrawn

    [TestMethod]
    public void TestTheRowOfTheSameLineIsFound()
    {
        var rows = new CuiDiffService().ToDiffRows([CommitDiffOf(ContextFileOf("a.txt", 6))]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        var index = CuiDiffService.FindClosestLineIndex(rows.Rows, fileIndex, 4);

        Assert.AreEqual(4, rows.Rows[index].LineNbr);
    }

    // Narrowing drops lines, so the line the cursor was on may no longer be drawn at all
    [TestMethod]
    public void TestTheNearestShownLineIsUsedWhenTheLineIsGone()
    {
        var lines = Enumerable
            .Range(20, 6)
            .Select(i => (i == 22 ? DiffMode.DiffAdded : DiffMode.DiffSame, $"line {i}"))
            .ToArray();
        var section = new SectionDiff(
            "20,6 +20,6",
            20,
            6,
            20,
            6,
            lines.Select(l => new LineDiff(l.Item1, l.Item2)).ToList()
        );
        var file = new FileDiff("a.txt", "a.txt", false, false, DiffMode.DiffModified, [section]);

        var rows = new CuiDiffService().ToDiffRows([CommitDiffOf(file)]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        var index = CuiDiffService.FindClosestLineIndex(rows.Rows, fileIndex, 5);

        Assert.AreEqual(20, rows.Rows[index].LineNbr, "The first line still shown");
    }

    // Above the first source line, e.g. on the file header itself, there is no line to go back to
    [TestMethod]
    public void TestNoLineNumberFallsBackToTheFileHeader()
    {
        var rows = new CuiDiffService().ToDiffRows([CommitDiffOf(ContextFileOf("a.txt", 6))]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        Assert.AreEqual(fileIndex, CuiDiffService.FindClosestLineIndex(rows.Rows, fileIndex, 0));
    }

    // The search must stop at the next file, or the cursor could land in the file below
    [TestMethod]
    public void TestTheSearchDoesNotRunIntoTheNextFile()
    {
        var rows = new CuiDiffService().ToDiffRows([
            CommitDiffOf(ContextFileOf("a.txt", 4), ContextFileOf("b.txt", 40)),
        ]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        var index = CuiDiffService.FindClosestLineIndex(rows.Rows, fileIndex, 30);

        Assert.AreEqual("a.txt", rows.Rows.Take(index + 1).Last(r => r.FilePath != "").FilePath);
        Assert.AreEqual(4, rows.Rows[index].LineNbr, "The last line of a.txt, not line 30 of b.txt");
    }
}
