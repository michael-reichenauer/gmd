using gmd.Cui.Diff;
using gmd.Server;
using gmdTest.Fixtures;
using CuiDiffService = gmd.Cui.Diff.DiffService;

// The color pictures below are one letter per rune, so they spell nothing
// cSpell:ignore DCWWW DRYYYY DGYYYYYY

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

    // The general form, for the cases FileDiffOf's defaults do not cover: a whole file added or
    // removed (drawn in one column), a rename, a binary file, or a hunk that does not start at
    // line 1. No lines at all means no section, which is what git reports for a pure rename or a
    // binary file and what ToDiffModeText/ToColorText key their special cases on.
    static FileDiff FileOf(
        string path,
        DiffMode fileMode = DiffMode.DiffModified,
        (DiffMode Mode, string Text)[]? lines = null,
        string pathBefore = "",
        bool isBinary = false,
        int leftLine = 1,
        int rightLine = 1
    )
    {
        lines ??= [];
        IReadOnlyList<SectionDiff> sections = lines.Any()
            ?
            [
                new SectionDiff(
                    $"{leftLine},{lines.Length} +{rightLine},{lines.Length}",
                    leftLine,
                    lines.Length,
                    rightLine,
                    lines.Length,
                    lines.Select(l => new LineDiff(l.Mode, l.Text)).ToList()
                ),
            ]
            : [];

        return new FileDiff(pathBefore == "" ? path : pathBefore, path, pathBefore != "", isBinary, fileMode, sections);
    }

    static CommitDiff CommitDiffOf(params FileDiff[] fileDiffs) =>
        new CommitDiff("abc123", "Test", new DateTime(2025, 1, 1), "A commit", fileDiffs);

    // The rows of one file's diff, which is what most of the tests below are about
    static DiffRows RowsOf(FileDiff file) => new CuiDiffService().ToDiffRows(CommitDiffOf(file));

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

    // The rows above the file list, i.e. the diff's own header
    static string SummeryOf(DiffRows rows) =>
        string.Join("\n", DiffText.Of(rows).Split('\n').TakeWhile(l => !l.EndsWith("Files:"))).TrimEnd();

    // The 'N Files:' row and the one row per file below it. Sliced by where they sit in the text
    // picture, so the color picture lines up with it.
    static string FileListOf(DiffRows rows, bool colors = false)
    {
        var text = DiffText.Of(rows).Split('\n');
        var first = text.FindIndexBy(l => l.EndsWith("Files:"));
        var count = text.Skip(first).TakeWhile(l => l != "").Count();
        var picture = (colors ? DiffText.ColorsOf(rows) : DiffText.Of(rows)).Split('\n');

        return string.Join("\n", picture.Skip(first).Take(count));
    }

    // The rows of the first file's diff, i.e. the ones BodyOf draws
    static IReadOnlyList<DiffRow> BodyRowsOf(DiffRows rows) => DiffText.BodyRowsOf(rows);

    // A file with one conflict recorded in diff3 style, i.e. with the common ancestor kept
    static readonly (DiffMode Mode, string Text)[] ConflictLines =
    [
        (DiffMode.DiffConflictStart, "<<<<<<< HEAD"),
        (DiffMode.DiffSame, "ours"),
        (DiffMode.DiffConflictBase, "||||||| base"),
        (DiffMode.DiffSame, "base line"),
        (DiffMode.DiffConflictSplit, "======="),
        (DiffMode.DiffSame, "theirs"),
        (DiffMode.DiffConflictEnd, ">>>>>>> other"),
    ];

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

    // Rendering the diff itself, i.e. what the diff view draws. DiffText flattens the styled rows
    // into a picture, so an expected value can be reviewed by looking at it. NoLine, the filler
    // drawn where one side has no line, is collapsed to a single '░'.

    // The commit summary the diff heads itself with

    [TestMethod]
    public void TestADiffIsDrawnAsSummeryFileListAndThenTheFileItself()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")]));

        Assert.AreEqual(
            """
            ═
            Commit:  abc123
            Author:  Test
            Date:    2025-01-01 00:00:00
            Message: A commit

            1 Files:
              Modified:    a.txt

            ━
            Modified: a.txt

            ─
               1 one │    1 one
            ─

            ━
            """,
            DiffText.Of(rows)
        );
    }

    // The uncommitted diff has no commit of its own, so DiffService fabricates a header with
    // every field empty; each one is left out rather than drawn blank
    [TestMethod]
    public void TestAnEmptyCommitFieldIsLeftOutOfTheSummery()
    {
        var rows = new CuiDiffService().ToDiffRows(
            new CommitDiff("", "", DateTime.MinValue, "", [FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")])])
        );

        Assert.AreEqual("═", SummeryOf(rows));
    }

    // The row carries the id so the view can open the commit from the diff
    [TestMethod]
    public void TestOnlyTheCommitRowCarriesTheCommitId()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")]));

        CollectionAssert.AreEqual(
            new[] { "abc123" },
            rows.Rows.Where(r => r.CommitId != "").Select(r => r.CommitId).ToArray()
        );
    }

    // The file list, i.e. what the diff says it is about before showing any of it

    [TestMethod]
    public void TestEveryFileIsListedWithItsMode()
    {
        var rows = new CuiDiffService().ToDiffRows(
            CommitDiffOf(
                FileOf("m.txt", lines: [(DiffMode.DiffSame, "m")]),
                FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "a")]),
                FileOf("r.txt", DiffMode.DiffRemoved, [(DiffMode.DiffRemoved, "r")]),
                FileOf("c.txt", DiffMode.DiffConflicts, [(DiffMode.DiffSame, "c")])
            )
        );

        Assert.AreEqual(
            """
            4 Files:
              Modified:    m.txt
              Added:       a.txt
              Removed:     r.txt
              Conflicts:   c.txt
            """,
            FileListOf(rows)
        );
    }

    // The mode is what colors the row, which is the only thing telling the four kinds apart at a
    // glance: white modified, green added, red removed and bright yellow conflicted
    [TestMethod]
    public void TestTheModeColorsTheFilesRow()
    {
        var rows = new CuiDiffService().ToDiffRows(
            CommitDiffOf(
                FileOf("m.txt", lines: [(DiffMode.DiffSame, "m")]),
                FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "a")]),
                FileOf("r.txt", DiffMode.DiffRemoved, [(DiffMode.DiffRemoved, "r")]),
                FileOf("c.txt", DiffMode.DiffConflicts, [(DiffMode.DiffSame, "c")])
            )
        );

        Assert.AreEqual(
            """
            W WWWWWW
              WWWWWWWWW    WWWWW
              GGGGGG       GGGGG
              RRRRRRRR     RRRRR
              yyyyyyyyyy   yyyyy
            """,
            FileListOf(rows, colors: true)
        );
    }

    // A rename git found no content change in names both paths and is called a rename rather than
    // a modification, which is the one case the mode does not come from DiffMode
    [TestMethod]
    public void TestARenameWithNoContentChangeNamesBothPaths()
    {
        var rows = RowsOf(FileOf("b.txt", pathBefore: "a.txt"));

        Assert.AreEqual(
            """
            1 Files:
              Renamed:     a.txt => b.txt (Renamed)
            """,
            FileListOf(rows)
        );
        Assert.AreEqual("Renamed: a.txt => b.txt  (Renamed)", HeaderRowsOf(rows).Single());
    }

    // A binary file is still called modified — only the '(Binary)' suffix and the dark color say
    // there is nothing to show
    [TestMethod]
    public void TestABinaryFileIsMarkedButStillCalledModified()
    {
        var rows = RowsOf(FileOf("a.bin", isBinary: true));

        Assert.AreEqual(
            """
            1 Files:
              Modified:    a.bin (Binary)
            """,
            FileListOf(rows)
        );
        Assert.AreEqual(
            """
            W WWWWWW
              DDDDDDDDD    DDDDD DDDDDDDD
            """,
            FileListOf(rows, colors: true)
        );
    }

    // No section means no hunk rules and no lines at all, i.e. the file is named and nothing else
    [TestMethod]
    public void TestAFileWithNoContentChangeHasNoBody()
    {
        Assert.AreEqual("", DiffText.BodyOf(RowsOf(FileOf("b.txt", pathBefore: "a.txt"))));
        Assert.AreEqual("", DiffText.BodyOf(RowsOf(FileOf("a.bin", isBinary: true))));
    }

    // Pairing the two sides, which is what makes the diff side by side

    [TestMethod]
    public void TestAChangedLineIsDrawnOnBothSides()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "one"), (DiffMode.DiffAdded, "uno")]));

        Assert.AreEqual("   1┃one │    1┃uno", DiffText.BodyOf(rows));
        Assert.AreEqual(DiffRowMode.SideBySide, BodyRowsOf(rows).Single().Mode);
    }

    // An unchanged line is on both sides with the plain space margin, not the '┃' a change gets
    [TestMethod]
    public void TestAnUnchangedLineIsDrawnOnBothSidesWithNoMargin()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "one"), (DiffMode.DiffSame, "two")]));

        Assert.AreEqual(
            """
               1 one │    1 one
               2 two │    2 two
            """,
            DiffText.BodyOf(rows)
        );
    }

    // More removed than added, so the surplus removed lines have nothing to pair with and the
    // right side is filled instead
    [TestMethod]
    public void TestASurplusRemovedLineLeavesTheRightSideFilled()
    {
        var rows = RowsOf(
            FileOf(
                "a.txt",
                lines: [(DiffMode.DiffRemoved, "one"), (DiffMode.DiffRemoved, "two"), (DiffMode.DiffAdded, "uno")]
            )
        );

        Assert.AreEqual(
            """
               1┃one │    1┃uno
               2┃two │ ░
            """,
            DiffText.BodyOf(rows)
        );

        var surplus = BodyRowsOf(rows)[1];
        Assert.AreSame(CuiDiffService.NoLine, surplus.Right, "The right side has no line of its own");
        Assert.AreEqual(2, surplus.LeftLineNbr);
        Assert.AreEqual(0, surplus.RightLineNbr, "The filled side is numbered 0, i.e. it is not a source line");
    }

    [TestMethod]
    public void TestASurplusAddedLineLeavesTheLeftSideFilled()
    {
        var rows = RowsOf(
            FileOf(
                "a.txt",
                lines: [(DiffMode.DiffRemoved, "one"), (DiffMode.DiffAdded, "uno"), (DiffMode.DiffAdded, "dos")]
            )
        );

        Assert.AreEqual(
            """
               1┃one │    1┃uno
            ░        │    2┃dos
            """,
            DiffText.BodyOf(rows)
        );

        var surplus = BodyRowsOf(rows)[1];
        Assert.AreSame(CuiDiffService.NoLine, surplus.Left);
        Assert.AreEqual(0, surplus.LeftLineNbr);
        Assert.AreEqual(2, surplus.RightLineNbr);
    }

    // The two sides count their own lines, so a removal and an addition move them apart. The
    // numbers are the gutter, what a copy strips and what the cursor is put back on after a reload
    [TestMethod]
    public void TestEachSideKeepsItsOwnLineNumbers()
    {
        var rows = RowsOf(
            FileOf(
                "a.txt",
                lines:
                [
                    (DiffMode.DiffSame, "one"),
                    (DiffMode.DiffRemoved, "two"),
                    (DiffMode.DiffRemoved, "three"),
                    (DiffMode.DiffAdded, "dos"),
                    (DiffMode.DiffSame, "four"),
                ]
            )
        );

        CollectionAssert.AreEqual(
            new[] { (1, 1), (2, 2), (3, 0), (4, 3) },
            BodyRowsOf(rows).Select(r => (r.LeftLineNbr, r.RightLineNbr)).ToArray()
        );
    }

    // A whole file added or removed has only one side to show, so it is drawn across both columns
    // rather than against an empty half

    [TestMethod]
    public void TestAWholeFileAddedIsDrawnInOneColumn()
    {
        var rows = RowsOf(
            FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one"), (DiffMode.DiffAdded, "two")])
        );

        Assert.AreEqual(
            """
               1┃one
               2┃two
            """,
            DiffText.BodyOf(rows)
        );
        CollectionAssert.AreEqual(
            new[] { DiffRowMode.SpanBoth, DiffRowMode.SpanBoth },
            BodyRowsOf(rows).Select(r => r.Mode).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { (0, 1), (0, 2) },
            BodyRowsOf(rows).Select(r => (r.LeftLineNbr, r.RightLineNbr)).ToArray()
        );
    }

    [TestMethod]
    public void TestAWholeFileRemovedIsDrawnInOneColumn()
    {
        var rows = RowsOf(FileOf("a.txt", DiffMode.DiffRemoved, [(DiffMode.DiffRemoved, "one")]));

        Assert.AreEqual("   1┃one", DiffText.BodyOf(rows));
        Assert.AreEqual(DiffRowMode.SpanBoth, BodyRowsOf(rows).Single().Mode);
        Assert.AreEqual(1, BodyRowsOf(rows).Single().LeftLineNbr);
    }

    // Marking what changed within a line, which is the only DiffPlex use in the codebase

    [TestMethod]
    public void TestOnlyTheChangedWordOfALineIsMarked()
    {
        var rows = RowsOf(
            FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "the quick fox"), (DiffMode.DiffAdded, "the slow fox")])
        );

        Assert.AreEqual("   1┃the quick fox │    1┃the slow fox", DiffText.BodyOf(rows));
        Assert.AreEqual(
            """
               DCWWW ----- WWW │    DCWWW ++++ WWW
            """,
            DiffText.BodyColorsOf(rows),
            "Only 'quick' and 'slow' are marked, the text either side of them is not"
        );
    }

    // Past four separate differences the line is marked whole, since a dozen scattered marks read
    // as noise rather than as a diff
    [TestMethod]
    public void TestALineWithManyDifferencesIsMarkedWhole()
    {
        var rows = RowsOf(
            FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "a b c d e f"), (DiffMode.DiffAdded, "1 2 3 4 5 6")])
        );

        Assert.AreEqual(
            """
               DC----------- │    DC+++++++++++
            """,
            DiffText.BodyColorsOf(rows)
        );
    }

    // A changed indent is the case the character diff cannot show, since the two lines are the
    // same once trimmed. Only the spaces one side has and the other does not are marked.
    [TestMethod]
    public void TestAChangedIndentIsMarkedOnTheIndentAlone()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "  x"), (DiffMode.DiffAdded, "    x")]));

        Assert.AreEqual("   1┃  x │    1┃    x", DiffText.BodyOf(rows));
        Assert.AreEqual(
            """
               DC  W │    DC  ++W
            """,
            DiffText.BodyColorsOf(rows),
            "The two spaces the right side has over the left are marked, the shared two are not"
        );
    }

    // A trailing space cannot be seen in the text at all, so the mark is the only thing that says
    // the line changed — which is why the two pictures below are different lengths
    [TestMethod]
    public void TestATrailingSpaceChangeIsMarkedThoughTheTextLooksIdentical()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "x"), (DiffMode.DiffAdded, "x  ")]));

        Assert.AreEqual("   1┃x │    1┃x", DiffText.BodyOf(rows));
        Assert.AreEqual(
            """
               DCW │    DCW++
            """,
            DiffText.BodyColorsOf(rows)
        );
    }

    // Conflicts, which are drawn ours left and theirs right between magenta markers

    [TestMethod]
    public void TestAConflictIsMarkedOnBothSidesWithOursLeftAndTheirsRight()
    {
        var rows = RowsOf(FileOf("a.txt", DiffMode.DiffConflicts, ConflictLines));

        Assert.AreEqual(
            """
            === Start of conflict │ === Start of conflict
               1┃ours             │ ░
            === Common ancestor
            base line
            ░                     │    3┃theirs
            === End of conflict   │ === End of conflict
            """,
            DiffText.BodyOf(rows)
        );
    }

    // The common ancestor of a diff3 conflict belongs to neither side, so it is drawn dark across
    // both columns rather than folded into ours, which is where it used to end up
    [TestMethod]
    public void TestTheCommonAncestorIsDrawnDarkAcrossBothColumns()
    {
        var rows = RowsOf(FileOf("a.txt", DiffMode.DiffConflicts, ConflictLines));

        Assert.AreEqual(
            """
            mmm mmmmm mm mmmmmmmm │ mmm mmmmm mm mmmmmmmm
               DRYYYY             │ D
            DDD DDDDDD DDDDDDDD
            DDDD DDDD
            D                     │    DGYYYYYY
            mmm mmm mm mmmmmmmm   │ mmm mmm mm mmmmmmmm
            """,
            DiffText.BodyColorsOf(rows)
        );

        var ancestor = BodyRowsOf(rows).Skip(2).Take(2).ToList();
        CollectionAssert.AreEqual(
            new[] { DiffRowMode.SpanBoth, DiffRowMode.SpanBoth },
            ancestor.Select(r => r.Mode).ToArray()
        );
        Assert.IsTrue(ancestor.All(r => r.LineNbr == 0), "The ancestor is in neither file, so it has no line number");
    }

    // Marking the files git reports as unmerged. The diff alone cannot see a conflict git wrote no
    // markers for, so a modify/delete would otherwise be headed 'Added:' as if there were nothing
    // to resolve.

    [TestMethod]
    public void TestAFileGitReportsAsUnmergedIsHeadedConflicts()
    {
        var rows = new CuiDiffService().ToDiffRows(
            [CommitDiffOf(FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one")]))],
            new Dictionary<string, int>(),
            ["a.txt"]
        );

        Assert.AreEqual("Conflicts: a.txt", HeaderRowsOf(rows).Single());
    }

    // Re-heading it also takes it out of the one column arm, since the file is no longer 'added'
    [TestMethod]
    public void TestAnUnmergedFileIsNoLongerDrawnInOneColumn()
    {
        var added = FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one")]);

        Assert.AreEqual("   1┃one", DiffText.BodyOf(RowsOf(added)));
        Assert.AreEqual(
            "░ │    1┃one",
            DiffText.BodyOf(
                new CuiDiffService().ToDiffRows([CommitDiffOf(added)], new Dictionary<string, int>(), ["a.txt"])
            )
        );
    }

    [TestMethod]
    public void TestAFileNotReportedAsUnmergedIsLeftAlone()
    {
        var rows = new CuiDiffService().ToDiffRows(
            [CommitDiffOf(FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one")]))],
            new Dictionary<string, int>(),
            ["other.txt"]
        );

        Assert.AreEqual("Added: a.txt", HeaderRowsOf(rows).Single());
    }

    // Full file history is one file over many commits, so the diff is headed with the list of them
    [TestMethod]
    public void TestAMultiCommitDiffIsHeadedWithACommitList()
    {
        var rows = new CuiDiffService().ToDiffRows([
            CommitDiffOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")])),
            CommitDiffOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "two")])),
        ]);

        Assert.AreEqual("2 Commits:", DiffText.Of(rows).Split('\n')[1]);
        Assert.AreEqual(
            2,
            DiffText.Of(rows).Split('\n').Count(l => l.StartsWith("Commit:  abc123")),
            "Each commit still gets its own summery below the list"
        );
    }
}
