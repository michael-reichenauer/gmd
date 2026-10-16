using System.Text.RegularExpressions;
using gmd.Cui.Diff;
using gmd.Server;
using gmdTest.Fixtures;

// The color pictures below are one letter per rune, so they spell nothing
// cSpell:ignore DCWWW DRYYYY DGYYYYYY

namespace gmdTest.Cui.Diff;

[TestClass]
public class DiffRowServiceTest
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
    static DiffRows RowsOf(FileDiff file) => new DiffRowService().ToDiffRows(CommitDiffOf(file));

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
        string.Join("\n", DiffText.Of(rows).Split('\n').TakeWhile(l => !IsFilesRow(l))).TrimEnd();

    // '3 files changed' and what is counted after it, or '1 file changed'
    static bool IsFilesRow(string line) => Regex.IsMatch(line, @"^\d+ files? changed");

    // The 'N files changed' row and the one row per file below it. Sliced by where they sit in the text
    // picture, so the color picture lines up with it.
    static string FileListOf(DiffRows rows, bool colors = false)
    {
        var text = DiffText.Of(rows).Split('\n');
        var first = text.FindIndexBy(IsFilesRow);
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
        Assert.AreEqual("=== Start of conflict", DiffRowService.WithoutLineNbr("=== Start of conflict", 0));
    }

    [TestMethod]
    public void TestTheLineNumberGutterIsStripped()
    {
        Assert.AreEqual("some code", DiffRowService.WithoutLineNbr("▌  12 some code", 12));
    }

    // The gutter is the bar, the number right aligned in 4 and a space, so it grows once the number no longer fits,
    // which whole file context on a large file reaches
    [TestMethod]
    public void TestTheGutterOfALargeLineNumberIsWider()
    {
        Assert.AreEqual("some code", DiffRowService.WithoutLineNbr("▌12345 some code", 12345));
    }

    [TestMethod]
    public void TestARowOfOnlyAGutterYieldsAnEmptyLine()
    {
        Assert.AreEqual("", DiffRowService.WithoutLineNbr("▌  12 ", 12));
    }

    // A file added whole is drawn in one column, with the new file's line numbers, and the copy read
    // the left side's number, none, so the gutter came with every line
    [TestMethod]
    public void TestCopyOfAFileAddedWholeLeavesOutTheGutter()
    {
        var rows = RowsOf(
            FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one"), (DiffMode.DiffAdded, "two")])
        );

        Assert.AreEqual("one\ntwo", DiffRowService.CopyText(rows.Rows.Where(r => r.LineNbr != 0), true));
    }

    // Side by side, the side in focus is copied, and a side with no line there is left out
    [TestMethod]
    public void TestCopyOfSideBySideRowsIsTheSideInFocus()
    {
        var rows = RowsOf(
            FileDiffOf(
                "a.txt",
                (DiffMode.DiffSame, "same"),
                (DiffMode.DiffRemoved, "old"),
                (DiffMode.DiffAdded, "new"),
                (DiffMode.DiffAdded, "more")
            )
        );
        var lines = rows.Rows.Where(r => r.LineNbr != 0).ToList();

        Assert.AreEqual("same\nold", DiffRowService.CopyText(lines, true));
        Assert.AreEqual("same\nnew\nmore", DiffRowService.CopyText(lines, false));
    }

    // With whitespace hidden the summary says so, a file of whitespace changes alone is listed as such,
    // with a row saying why it has no lines, and the new indent of a changed line is not marked,
    // where it is otherwise, with the spaces on the green background of an added change, '+'; the
    // word that changed is marked either way
    [TestMethod]
    public void TestADiffWithWhitespaceHiddenSaysSoAndMarksNone()
    {
        var reindented = FileDiffOf("a.c", (DiffMode.DiffRemoved, "bar();"), (DiffMode.DiffAdded, "    baz();"));
        var spacesOnly = FileOf("b.txt") with { IsWhitespaceOnly = true };
        var diff = CommitDiffOf(reindented, spacesOnly);

        var hidden = new DiffRowService().ToDiffRows(
            [diff],
            new Dictionary<string, int>(),
            [],
            isIgnoreWhitespace: true
        );
        var shown = new DiffRowService().ToDiffRows([diff], new Dictionary<string, int>(), []);

        Assert.AreEqual(
            """
            2 files changed  2 modified  +1 -1  whitespace hidden
              M a.c    +1 -1
              M b.txt  whitespace only
            """,
            FileListOf(hidden)
        );
        Assert.AreEqual(
            """
            Test  2025-01-01 00:00:00

            2 files changed  2 modified  +1 -1  whitespace hidden
              M a.c    +1 -1
              M b.txt  whitespace only

            ▌ M a.c  +1 -1 ─
            ▌   1 bar(); │ ▌   1     baz();

            ▌ M b.txt  whitespace only ─
                  Only whitespace changed, which is hidden
            """,
            DiffText.Of(hidden)
        );
        Assert.AreEqual("R   R ---WWW │ G   G     +++WWW", DiffText.BodyColorsOf(hidden), "The indent not marked");
        Assert.AreEqual("R   R ---WWW │ G   G +++++++WWW", DiffText.BodyColorsOf(shown));
    }

    // A search looks in each side's line without its line number, which '12' would otherwise find on
    // every twelfth line, and in the headers, e.g. a file's path; not in a divider, nor in the filler
    // of a side with no line
    [TestMethod]
    public void TestASearchLooksInTheLinesWithoutTheirNumbers()
    {
        var rows = RowsOf(
            FileDiffOf(
                "a.txt",
                (DiffMode.DiffSame, "same"),
                (DiffMode.DiffRemoved, "old"),
                (DiffMode.DiffAdded, "new"),
                (DiffMode.DiffAdded, "more")
            )
        );

        var texts = rows
            .Rows.Select(DiffRowService.SearchText)
            .Where(t => t != ("", ""))
            .Select(t => $"{t.Left.TrimEnd()}|{t.Right.TrimEnd()}");

        Assert.AreEqual(
            """
            Test  2025-01-01 00:00:00|
            1 file changed  1 modified  +2 -1|
              M a.txt  +2 -1|
            ▌ M a.txt  +2 -1|
            same|same
            old|new
            |more
            """,
            string.Join("\n", texts)
        );
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
        var rows = new DiffRowService().ToDiffRows(
            [CommitDiffOf(FileDiffOf("a.txt", (DiffMode.DiffSame, "one")))],
            new Dictionary<string, int> { ["a.txt"] = DiffContext.Default }
        );

        CollectionAssert.AreEqual(new[] { "▌ M a.txt" }, HeaderRowsOf(rows).ToArray());
    }

    [TestMethod]
    public void TestOnlyTheSteppedFileNamesItsContext()
    {
        var rows = new DiffRowService().ToDiffRows(
            [
                CommitDiffOf(
                    FileDiffOf("a.txt", (DiffMode.DiffSame, "one")),
                    FileDiffOf("b.txt", (DiffMode.DiffSame, "x"))
                ),
            ],
            new Dictionary<string, int> { ["a.txt"] = 15 }
        );

        CollectionAssert.AreEqual(new[] { "▌ M a.txt  (context 15)", "▌ M b.txt" }, HeaderRowsOf(rows).ToArray());
    }

    [TestMethod]
    public void TestWholeFileContextIsNamedRatherThanNumbered()
    {
        var rows = new DiffRowService().ToDiffRows(
            [CommitDiffOf(FileDiffOf("a.txt", (DiffMode.DiffSame, "one")))],
            new Dictionary<string, int> { ["a.txt"] = DiffContext.WholeFile }
        );

        CollectionAssert.AreEqual(new[] { "▌ M a.txt  (whole file)" }, HeaderRowsOf(rows).ToArray());
    }

    // Splicing one file of a re-fetched diff into the one on screen

    [TestMethod]
    public void TestOnlyTheNamedFileIsTakenFromTheFetchedDiff()
    {
        var shown = new[] { CommitDiffOf(ContextFileOf("a.txt", 4), ContextFileOf("b.txt", 4)) };
        var fetched = new[] { CommitDiffOf(ContextFileOf("a.txt", 20), ContextFileOf("b.txt", 20)) };

        var spliced = DiffRowService.ReplaceFileDiff(shown, "a.txt", fetched);

        Assert.AreEqual(20, spliced[0].FileDiffs[0].SectionDiffs[0].LineDiffs.Count, "a.txt was widened");
        Assert.AreEqual(4, spliced[0].FileDiffs[1].SectionDiffs[0].LineDiffs.Count, "b.txt was left alone");
    }

    [TestMethod]
    public void TestTheOrderOfTheFilesIsKept()
    {
        var shown = new[] { CommitDiffOf(FileDiffOf("a.txt"), FileDiffOf("b.txt"), FileDiffOf("c.txt")) };
        var fetched = new[] { CommitDiffOf(FileDiffOf("a.txt"), FileDiffOf("b.txt"), FileDiffOf("c.txt")) };

        var spliced = DiffRowService.ReplaceFileDiff(shown, "b.txt", fetched);

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

        var spliced = DiffRowService.ReplaceFileDiff(shown, "a.txt", fetched);

        Assert.AreSame(shown, spliced);
    }

    // Full file history is one file over many commits, so there is no single file to pick out
    [TestMethod]
    public void TestAMultiCommitDiffIsReplacedWholesale()
    {
        var shown = new[] { CommitDiffOf(ContextFileOf("a.txt", 4)), CommitDiffOf(ContextFileOf("a.txt", 4)) };
        var fetched = new[] { CommitDiffOf(ContextFileOf("a.txt", 20)), CommitDiffOf(ContextFileOf("a.txt", 20)) };

        Assert.AreSame(fetched, DiffRowService.ReplaceFileDiff(shown, "a.txt", fetched));
    }

    // Finding the row to put the cursor back on after the file was redrawn

    [TestMethod]
    public void TestTheRowOfTheSameLineIsFound()
    {
        var rows = new DiffRowService().ToDiffRows([CommitDiffOf(ContextFileOf("a.txt", 6))]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        var index = DiffRowService.FindClosestLineIndex(rows.Rows, fileIndex, 4);

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

        var rows = new DiffRowService().ToDiffRows([CommitDiffOf(file)]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        var index = DiffRowService.FindClosestLineIndex(rows.Rows, fileIndex, 5);

        Assert.AreEqual(20, rows.Rows[index].LineNbr, "The first line still shown");
    }

    // Above the first source line, e.g. on the file header itself, there is no line to go back to
    [TestMethod]
    public void TestNoLineNumberFallsBackToTheFileHeader()
    {
        var rows = new DiffRowService().ToDiffRows([CommitDiffOf(ContextFileOf("a.txt", 6))]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        Assert.AreEqual(fileIndex, DiffRowService.FindClosestLineIndex(rows.Rows, fileIndex, 0));
    }

    // The search must stop at the next file, or the cursor could land in the file below
    [TestMethod]
    public void TestTheSearchDoesNotRunIntoTheNextFile()
    {
        var rows = new DiffRowService().ToDiffRows([
            CommitDiffOf(ContextFileOf("a.txt", 4), ContextFileOf("b.txt", 40)),
        ]);
        var fileIndex = rows.Rows.FindIndexBy(r => r.FilePath == "a.txt");

        var index = DiffRowService.FindClosestLineIndex(rows.Rows, fileIndex, 30);

        Assert.AreEqual("a.txt", rows.Rows.Take(index + 1).Last(r => r.FilePath != "").FilePath);
        Assert.AreEqual(4, rows.Rows[index].LineNbr, "The last line of a.txt, not line 30 of b.txt");
    }

    // Rendering the diff itself, i.e. what the diff view draws. DiffText flattens the styled rows
    // into a picture, so an expected value can be reviewed by looking at it. NoLine, the side with no
    // line, is blank, and the rule after a file's header is one '─'.

    // The commit summary the diff heads itself with: who made it when, the id and the subject being
    // the view's top row, where they stay in sight

    [TestMethod]
    public void TestADiffIsDrawnAsSummeryFileListAndThenTheFileItself()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")]));

        Assert.AreEqual(
            """
            Test  2025-01-01 00:00:00

            1 file changed  1 modified
              M a.txt

            ▌ M a.txt ─
                1 one │     1 one
            """,
            DiffText.Of(rows)
        );
    }

    // The uncommitted diff has no commit of its own, so DiffRowService fabricates a header with
    // every field empty; each one is left out rather than drawn blank, and so are the rows they are on
    [TestMethod]
    public void TestAnEmptyCommitFieldIsLeftOutOfTheSummery()
    {
        var rows = new DiffRowService().ToDiffRows(
            new CommitDiff("", "", DateTime.MinValue, "", [FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")])])
        );

        Assert.AreEqual("", SummeryOf(rows));
    }

    // In a file history each commit's title row carries its id, which is how the view finds where a
    // commit starts; a diff of one commit has no such row, its title being the view's top row
    [TestMethod]
    public void TestOnlyTheCommitRowsOfAHistoryCarryTheCommitIds()
    {
        var file = FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")]);
        var history = new DiffRowService().ToDiffRows([
            new CommitDiff("c2", "Test", new DateTime(2025, 1, 2), "Second", [file]),
            new CommitDiff("c1", "Test", new DateTime(2025, 1, 1), "First", [file]),
        ]);

        CollectionAssert.AreEqual(
            new[] { "c2", "c1" },
            history.Rows.Where(r => r.CommitId != "").Select(r => r.CommitId).ToArray()
        );
        Assert.IsFalse(RowsOf(file).Rows.Any(r => r.CommitId != ""));
    }

    // The title of the top row: the id and the subject, or what there is of them
    [TestMethod]
    public void TestTheTitleIsTheIdAndTheSubject()
    {
        Assert.AreEqual("abc123  A commit", DiffRowService.TitleOf(CommitDiffOf()).ToString());
        Assert.AreEqual(
            "Uncommitted changes",
            DiffRowService.TitleOf(new CommitDiff("", "", DateTime.MinValue, "Uncommitted changes", [])).ToString()
        );
    }

    // The file list, i.e. what the diff says it is about before showing any of it

    [TestMethod]
    public void TestEveryFileIsListedWithItsMode()
    {
        var rows = new DiffRowService().ToDiffRows(
            CommitDiffOf(
                FileOf("m.txt", lines: [(DiffMode.DiffSame, "m")]),
                FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "a")]),
                FileOf("r.txt", DiffMode.DiffRemoved, [(DiffMode.DiffRemoved, "r")]),
                FileOf("c.txt", DiffMode.DiffConflicts, [(DiffMode.DiffSame, "c")])
            )
        );

        Assert.AreEqual(
            """
            4 files changed  1 added, 1 modified, 1 deleted, 1 conflicted  +1 -1
              M m.txt
              A a.txt  +1
              D r.txt  -1
              C c.txt
            """,
            FileListOf(rows)
        );
    }

    // The kind is a letter in its color, as the commit details have it: yellow modified, green added,
    // red deleted and bright red conflicted, and the files row names the kinds in the same colors
    [TestMethod]
    public void TestTheModeColorsTheFilesRow()
    {
        var rows = new DiffRowService().ToDiffRows(
            CommitDiffOf(
                FileOf("m.txt", lines: [(DiffMode.DiffSame, "m")]),
                FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "a")]),
                FileOf("r.txt", DiffMode.DiffRemoved, [(DiffMode.DiffRemoved, "r")]),
                FileOf("c.txt", DiffMode.DiffConflicts, [(DiffMode.DiffSame, "c")])
            )
        );

        Assert.AreEqual(
            """
            W WWWWW WWWWWWW  G GGGGGD Y YYYYYYYYD R RRRRRRRD r rrrrrrrrrr  GG RR
              Y WWWWW
              G WWWWW  GG
              R WWWWW  RR
              r WWWWW
            """,
            FileListOf(rows, colors: true)
        );
    }

    // The files row counts the files of each kind, as the list below it names them, and the lines
    // added and removed, which context lines do not count in
    [TestMethod]
    public void TestTheFilesRowCountsTheFilesAndTheLines()
    {
        var rows = new DiffRowService().ToDiffRows(
            CommitDiffOf(
                FileOf(
                    "m.txt",
                    lines:
                    [
                        (DiffMode.DiffSame, "m1"),
                        (DiffMode.DiffRemoved, "m2"),
                        (DiffMode.DiffAdded, "m2 changed"),
                        (DiffMode.DiffAdded, "m3"),
                        (DiffMode.DiffSame, "m4"),
                    ]
                ),
                FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "a1"), (DiffMode.DiffAdded, "a2")]),
                FileOf("b.txt", lines: [(DiffMode.DiffRemoved, "b"), (DiffMode.DiffAdded, "B")], pathBefore: "old.txt"),
                FileOf("c.bin", isBinary: true)
            )
        );

        Assert.AreEqual("4 files changed  1 added, 2 modified, 1 renamed  +5 -2", FileListOf(rows).Split('\n')[0]);
    }

    // A rename git found no content change in names both paths and is called a rename rather than
    // a modification, which is the one case the mode does not come from DiffMode
    [TestMethod]
    public void TestARenameWithNoContentChangeNamesBothPaths()
    {
        var rows = RowsOf(FileOf("b.txt", pathBefore: "a.txt"));

        Assert.AreEqual(
            """
            1 file changed  1 renamed
              R a.txt → b.txt
            """,
            FileListOf(rows)
        );
        Assert.AreEqual("▌ R a.txt → b.txt", HeaderRowsOf(rows).Single());
    }

    // A binary file is still called modified, with 'binary' in the place of its lines, in dark
    [TestMethod]
    public void TestABinaryFileIsMarkedButStillCalledModified()
    {
        var rows = RowsOf(FileOf("a.bin", isBinary: true));

        Assert.AreEqual(
            """
            1 file changed  1 modified
              M a.bin  binary
            """,
            FileListOf(rows)
        );
        Assert.AreEqual(
            """
            W WWWW WWWWWWW  Y YYYYYYYY
              Y WWWWW  DDDDDD
            """,
            FileListOf(rows, colors: true)
        );
    }

    // No section means no hunk header and no lines at all, i.e. the file is named and nothing else
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

        Assert.AreEqual("▌   1 one │ ▌   1 uno", DiffText.BodyOf(rows));
        Assert.AreEqual(DiffRowMode.SideBySide, BodyRowsOf(rows).Single().Mode);
    }

    // An unchanged line is on both sides with a space in the gutter, not the '▌' a change gets
    [TestMethod]
    public void TestAnUnchangedLineIsDrawnOnBothSidesWithNoMargin()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "one"), (DiffMode.DiffSame, "two")]));

        Assert.AreEqual(
            """
                1 one │     1 one
                2 two │     2 two
            """,
            DiffText.BodyOf(rows)
        );
    }

    // More removed than added, so the surplus removed lines have nothing to pair with and the
    // right side is blank beside them
    [TestMethod]
    public void TestASurplusRemovedLineLeavesTheRightSideBlank()
    {
        var rows = RowsOf(
            FileOf(
                "a.txt",
                lines: [(DiffMode.DiffRemoved, "one"), (DiffMode.DiffRemoved, "two"), (DiffMode.DiffAdded, "uno")]
            )
        );

        Assert.AreEqual(
            """
            ▌   1 one │ ▌   1 uno
            ▌   2 two │
            """,
            DiffText.BodyOf(rows)
        );

        var surplus = BodyRowsOf(rows)[1];
        Assert.AreSame(DiffRowService.NoLine, surplus.Right, "The right side has no line of its own");
        Assert.AreEqual(2, surplus.LeftLineNbr);
        Assert.AreEqual(0, surplus.RightLineNbr, "The blank side is numbered 0, i.e. it is not a source line");
    }

    [TestMethod]
    public void TestASurplusAddedLineLeavesTheLeftSideBlank()
    {
        var rows = RowsOf(
            FileOf(
                "a.txt",
                lines: [(DiffMode.DiffRemoved, "one"), (DiffMode.DiffAdded, "uno"), (DiffMode.DiffAdded, "dos")]
            )
        );

        Assert.AreEqual(
            """
            ▌   1 one │ ▌   1 uno
                      │ ▌   2 dos
            """,
            DiffText.BodyOf(rows)
        );

        var surplus = BodyRowsOf(rows)[1];
        Assert.AreSame(DiffRowService.NoLine, surplus.Left);
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
            ▌   1 one
            ▌   2 two
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

        Assert.AreEqual("▌   1 one", DiffText.BodyOf(rows));
        Assert.AreEqual(DiffRowMode.SpanBoth, BodyRowsOf(rows).Single().Mode);
        Assert.AreEqual(1, BodyRowsOf(rows).Single().LeftLineNbr);
    }

    // Marking what changed within a line, word by word (LineWords), which is where DiffPlex is used

    [TestMethod]
    public void TestOnlyTheChangedWordOfALineIsMarked()
    {
        var rows = RowsOf(
            FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "the quick fox"), (DiffMode.DiffAdded, "the slow fox")])
        );

        Assert.AreEqual("▌   1 the quick fox │ ▌   1 the slow fox", DiffText.BodyOf(rows));
        Assert.AreEqual(
            """
            R   R WWW ----- WWW │ G   G WWW ++++ WWW
            """,
            DiffText.BodyColorsOf(rows),
            "Only 'quick' and 'slow' are marked, the text either side of them is not"
        );
    }

    // Lines that share less than half their text are rewritten rather than edited, and are not
    // marked at all, since a dozen scattered marks read as noise rather than as a diff: the bars and
    // the numbers in red and green say they changed
    [TestMethod]
    public void TestALineThatSharesLittleIsNotMarked()
    {
        var rows = RowsOf(
            FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "a b c d e f"), (DiffMode.DiffAdded, "1 2 3 4 5 6")])
        );

        Assert.AreEqual(
            """
            R   R W W W W W W │ G   G W W W W W W
            """,
            DiffText.BodyColorsOf(rows)
        );
    }

    // A changed indent is the case the word diff cannot show, since the two lines are the same once
    // trimmed. Only the spaces one side has and the other does not are marked.
    [TestMethod]
    public void TestAChangedIndentIsMarkedOnTheIndentAlone()
    {
        var rows = RowsOf(FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "  x"), (DiffMode.DiffAdded, "    x")]));

        Assert.AreEqual("▌   1   x │ ▌   1     x", DiffText.BodyOf(rows));
        Assert.AreEqual(
            """
            R   R   W │ G   G   ++W
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

        Assert.AreEqual("▌   1 x │ ▌   1 x", DiffText.BodyOf(rows));
        Assert.AreEqual(
            """
            R   R W │ G   G W++
            """,
            DiffText.BodyColorsOf(rows)
        );
    }

    // Conflicts, which are drawn ours left and theirs right between magenta markers, in the resolver's
    // colors, yellow and cyan, and with no word marked: neither side is removed or added

    [TestMethod]
    public void TestAConflictIsMarkedOnBothSidesWithOursLeftAndTheirsRight()
    {
        var rows = RowsOf(FileOf("a.txt", DiffMode.DiffConflicts, ConflictLines));

        Assert.AreEqual(
            """
            === Start of conflict │ === Start of conflict
            ▌   2 ours            │
            === Common ancestor
            base line
                                  │ ▌   6 theirs
            === End of conflict   │ === End of conflict
            """,
            DiffText.BodyOf(rows)
        );
    }

    // The markers are lines of the working tree file, as git writes them there, so the right side is
    // numbered past them: the lines after a conflict were numbered 3 low, 4 with a common ancestor
    [TestMethod]
    public void TestTheMarkersAreLinesOfTheWorkingTreeFile()
    {
        var rows = RowsOf(
            FileOf(
                "a.txt",
                DiffMode.DiffConflicts,
                [
                    (DiffMode.DiffSame, "before"),
                    (DiffMode.DiffConflictStart, "<<<<<<< HEAD"),
                    (DiffMode.DiffSame, "ours"),
                    (DiffMode.DiffConflictSplit, "======="),
                    (DiffMode.DiffAdded, "theirs"),
                    (DiffMode.DiffConflictEnd, ">>>>>>> other"),
                    (DiffMode.DiffSame, "after"),
                ]
            )
        );

        Assert.AreEqual(
            """
                1 before          │     1 before
            === Start of conflict │ === Start of conflict
            ▌   3 ours            │ ▌   5 theirs
            === End of conflict   │ === End of conflict
                3 after           │     7 after
            """,
            DiffText.BodyOf(rows)
        );
    }

    // A change of line ends alone, '\n' to '\r\n', is a '␍' on the side that has the '\r', where the
    // two sides were the same text when every '\r' was dropped; a file with '\r\n' line ends that
    // changed otherwise looks as any other
    [TestMethod]
    public void TestAChangeOfLineEndsIsShown()
    {
        var rows = RowsOf(
            FileOf(
                "a.txt",
                lines:
                [
                    (DiffMode.DiffSame, "same\r"),
                    (DiffMode.DiffRemoved, "one"),
                    (DiffMode.DiffRemoved, "two\r"),
                    (DiffMode.DiffAdded, "one\r"),
                    (DiffMode.DiffAdded, "TWO\r"),
                ]
            )
        );

        Assert.AreEqual(
            """
                1 same │     1 same
            ▌   2 one  │ ▌   2 one␍
            ▌   3 two  │ ▌   3 TWO
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
            Y   D YYYY            │
            DDD DDDDDD DDDDDDDD
            DDDD DDDD
                                  │ C   D CCCCCC
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
        var rows = new DiffRowService().ToDiffRows(
            [CommitDiffOf(FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one")]))],
            new Dictionary<string, int>(),
            ["a.txt"]
        );

        Assert.AreEqual("▌ C a.txt  +1", HeaderRowsOf(rows).Single());
    }

    // A file in conflict now is its header and one row saying how many conflicts it has, which the
    // resolver resolves, Enter there; its sides are read there, not here. One git wrote no markers
    // for, e.g. deleted on one side, is a conflict of the whole file.
    [TestMethod]
    public void TestAFileInConflictIsOneRowSayingSo()
    {
        var added = FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one")]);
        var marked = FileOf("b.txt", DiffMode.DiffConflicts, [.. ConflictLines, .. ConflictLines]);

        Assert.AreEqual("▌   1 one", DiffText.BodyOf(RowsOf(added)));
        Assert.AreEqual(
            "      A conflict of the whole file: Enter resolves it",
            DiffText.BodyOf(
                new DiffRowService().ToDiffRows([CommitDiffOf(added)], new Dictionary<string, int>(), ["a.txt"])
            )
        );
        Assert.AreEqual(
            "      2 conflicts: Enter resolves them",
            DiffText.BodyOf(
                new DiffRowService().ToDiffRows([CommitDiffOf(marked)], new Dictionary<string, int>(), ["b.txt"])
            )
        );
    }

    [TestMethod]
    public void TestAFileNotReportedAsUnmergedIsLeftAlone()
    {
        var rows = new DiffRowService().ToDiffRows(
            [CommitDiffOf(FileOf("a.txt", DiffMode.DiffAdded, [(DiffMode.DiffAdded, "one")]))],
            new Dictionary<string, int>(),
            ["other.txt"]
        );

        Assert.AreEqual("▌ A a.txt  +1", HeaderRowsOf(rows).Single());
    }

    // Full file history is one file over many commits, so the diff is headed with the list of them,
    // which the view's top row counts
    [TestMethod]
    public void TestAMultiCommitDiffIsHeadedWithACommitList()
    {
        var rows = new DiffRowService().ToDiffRows([
            CommitDiffOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "one")])),
            CommitDiffOf(FileOf("a.txt", lines: [(DiffMode.DiffSame, "two")])),
        ]);

        StringAssert.StartsWith(DiffText.Of(rows).Split('\n')[0], "  abc123  A commit");
        Assert.AreEqual(
            2,
            DiffText.Of(rows).Split('\n').Count(l => l.StartsWith("abc123  A commit")),
            "Each commit still gets its own summery below the list"
        );
    }

    // In one column, the layout of a narrow view, a change's removed lines are above its added ones,
    // as git writes them, the unchanged line before them once, and the words that differ marked as
    // side by side

    static DiffRows OneColumnOf(FileDiff file) =>
        new DiffRowService().ToDiffRows([CommitDiffOf(file)], new Dictionary<string, int>(), [], isOneColumn: true);

    [TestMethod]
    public void TestOneColumnHasTheRemovedLinesAboveTheAdded()
    {
        var rows = OneColumnOf(
            FileOf(
                "a.txt",
                lines:
                [
                    (DiffMode.DiffSame, "one"),
                    (DiffMode.DiffRemoved, "the quick fox"),
                    (DiffMode.DiffRemoved, "gone"),
                    (DiffMode.DiffAdded, "the slow fox"),
                    (DiffMode.DiffSame, "four"),
                ]
            )
        );

        Assert.AreEqual(
            """
                1 one
            ▌   2 the quick fox
            ▌   3 gone
            ▌   2 the slow fox
                3 four
            """,
            DiffText.BodyOf(rows)
        );
        Assert.AreEqual(
            """
                D WWW
            R   R WWW ----- WWW
            R   R WWWW
            G   G WWW ++++ WWW
                D WWWW
            """,
            DiffText.BodyColorsOf(rows),
            "The words of the pair marked, the line with no pair not"
        );
        CollectionAssert.AreEqual(
            new[] { (1, 1), (2, 0), (3, 0), (0, 2), (4, 3) },
            BodyRowsOf(rows).Select(r => (r.LeftLineNbr, r.RightLineNbr)).ToArray(),
            "An unchanged line has both numbers, a changed one its own side's"
        );
    }

    // A conflict's sides one after the other, each named by git's marker, ours yellow, theirs cyan
    [TestMethod]
    public void TestOneColumnNamesTheSidesOfAConflict()
    {
        var rows = OneColumnOf(
            FileOf(
                "a.txt",
                DiffMode.DiffConflicts,
                [
                    (DiffMode.DiffConflictStart, "<<<<<< HEAD"),
                    (DiffMode.DiffSame, "ours"),
                    (DiffMode.DiffConflictSplit, "======"),
                    (DiffMode.DiffSame, "theirs"),
                    (DiffMode.DiffConflictEnd, ">>>>>> dev"),
                ]
            )
        );

        Assert.AreEqual(
            """
            === Start of conflict  HEAD
            ▌   2 ours
            === dev
            ▌   4 theirs
            === End of conflict
            """,
            DiffText.BodyOf(rows)
        );
        Assert.IsTrue(BodyRowsOf(rows)[0].IsConflictStart, "What ] goes to");
    }

    // A file whose lines are hidden, h, is its header alone, which says so; the others are as they were
    [TestMethod]
    public void TestAHiddenFileIsItsHeaderAlone()
    {
        var diff = CommitDiffOf(
            FileOf("a.txt", lines: [(DiffMode.DiffRemoved, "one"), (DiffMode.DiffAdded, "uno")]),
            FileOf("b.txt", lines: [(DiffMode.DiffSame, "two")])
        );

        var rows = new DiffRowService().ToDiffRows(
            [diff],
            new Dictionary<string, int>(),
            [],
            hiddenFiles: new HashSet<string> { "a.txt" }
        );

        CollectionAssert.AreEqual(
            new[] { "▌ M a.txt  +1 -1  lines hidden", "▌ M b.txt" },
            HeaderRowsOf(rows).ToArray()
        );
        Assert.IsFalse(rows.Rows.Any(r => r.IsChange), "No line of a.txt");
        Assert.AreEqual("    1 two │     1 two", DiffText.Of(rows).Split('\n')[^1]);
    }

    // A hunk after lines left out starts with '⋯' and a line, naming the function git found it in
    // where that is not the one before it: with no pattern for C#, git names the class for each, and
    // a name on every hunk said nothing. One from the file's first line has nothing left out above it.
    // The row keeps the function either way, for the top row. The file's line is blue, which sets it
    // apart from the hunks' dark ones.
    [TestMethod]
    public void TestAHunkAfterLinesLeftOutIsSeparated()
    {
        var rows = RowsOf(FileWithHunks());
        var text = DiffText.Of(rows).Split('\n');
        var header = text.FindIndexBy(l => l.StartsWith("▌ M Pricing.cs"));

        Assert.AreEqual(
            """
            ▌ M Pricing.cs  +4 -4 ─
                1 same     │     1 same
            ▌   2 using A; │ ▌   2 using B;
               ⋯ class Pricing ─
               20 same     │    20 same
            ▌  21 a = 1;   │ ▌  21 a = 2;
               ⋯ ─
               40 same     │    40 same
            ▌  41 b = 1;   │ ▌  41 b = 2;
               ⋯ decimal Total() ─
               60 same     │    60 same
            ▌  61 c = 1;   │ ▌  61 c = 2;
            """,
            string.Join("\n", text.Skip(header))
        );
        var colors = DiffText.ColorsOf(rows).Split('\n').Skip(header).ToArray();
        Assert.AreEqual("M Y WWWWWWWWWW  GG RR B", colors[0]);
        Assert.AreEqual("   D CCCCC CCCCCCC D", colors[3]);
        Assert.AreEqual("   D D", colors[6]);
        CollectionAssert.AreEqual(
            new[] { "class Pricing", "class Pricing", "decimal Total()" },
            rows.Rows.Where(r => r.IsHunkHeader).Select(r => r.Function).ToArray()
        );
    }

    // A file of four hunks: from its first line, and in the functions git found the others in,
    // two in one
    static FileDiff FileWithHunks()
    {
        SectionDiff Hunk(int line, string function, string removed, string added) =>
            new(
                $"{line},2 +{line},2",
                line,
                2,
                line,
                2,
                [new(DiffMode.DiffSame, "same"), new(DiffMode.DiffRemoved, removed), new(DiffMode.DiffAdded, added)]
            )
            {
                Function = function,
            };

        return new FileDiff(
            "Pricing.cs",
            "Pricing.cs",
            false,
            false,
            DiffMode.DiffModified,
            [
                Hunk(1, "", "using A;", "using B;"),
                Hunk(20, "class Pricing", "a = 1;", "a = 2;"),
                Hunk(40, "class Pricing", "b = 1;", "b = 2;"),
                Hunk(60, "decimal Total()", "c = 1;", "c = 2;"),
            ]
        );
    }
}
