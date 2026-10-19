using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Diff;

// The code of a diff's rows in its language's colors, line after line of each side of a file
[TestClass]
public class DiffSyntaxTest
{
    static FileDiff FileOf(string path, DiffMode fileMode, params SectionDiff[] sections) =>
        new(path, path, false, false, fileMode, sections);

    static SectionDiff SectionOf(int leftLine, int rightLine, params (DiffMode Mode, string Text)[] lines) =>
        new(
            $"{leftLine} +{rightLine}",
            leftLine,
            lines.Length,
            rightLine,
            lines.Length,
            lines.Select(l => new LineDiff(l.Mode, l.Text)).ToList()
        );

    static DiffRows RowsOf(FileDiff file, bool isOneColumn = false) =>
        new DiffRowService().ToDiffRows(
            [new CommitDiff("abc123", "Test", new DateTime(2025, 1, 1), "A commit", [file])],
            new Dictionary<string, int>(),
            [],
            isOneColumn: isOneColumn
        );

    // The colors of the file's lines with the code of each side colored, as the view shows them
    static string ColorsOf(DiffRows rows, DiffSyntax syntax) =>
        DiffText.BodyColorsOf(
            rows,
            (row, i) =>
                row with
                {
                    Left = syntax.Colored(row.Left, i, false),
                    Right = row.Mode == DiffRowMode.SideBySide ? syntax.Colored(row.Right, i, true) : row.Right,
                }
        );

    // Only the code of a line is colored, what the text's color is drawn in: not the gutter. A
    // changed word keeps its background, which the picture has no letter for alongside the keyword.
    [TestMethod]
    public void TestTheCodeOfEachSideIsColored()
    {
        var rows = RowsOf(
            FileOf(
                "a.cs",
                DiffMode.DiffModified,
                SectionOf(
                    1,
                    1,
                    (DiffMode.DiffSame, "int a;"),
                    (DiffMode.DiffRemoved, "long b = 1;"),
                    (DiffMode.DiffAdded, "int b = 1;"),
                    (DiffMode.DiffSame, "x = 2;")
                )
            )
        );
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(new FakeCodeGrammar()));

        Assert.AreEqual(
            """
                1 int a;      │     1 int a;
            ▌   2 long b = 1; │ ▌   2 int b = 1;
                3 x = 2;      │     3 x = 2;
            """,
            DiffText.BodyOf(rows)
        );
        Assert.AreEqual(
            """
                D KKK WW      │     D KKK WW
            R   R ---- W W WW │ G   G KKK W W WW
                D W W WW      │     D W W WW
            """,
            ColorsOf(rows, syntax)
        );
        var index = rows.Rows.FindIndexBy(r => r.AddedText == "int b = 1;");
        var keyword = syntax.Colored(rows.Rows[index].Right, index, true).Fragments.Single(f => f.Text == "int");
        Assert.AreEqual(new Color(ColorName.CodeKeyword, ColorName.AddedBg), keyword.Color);
    }

    // Each side is colored line after line, the old file's lines on the left and the new file's on the
    // right, so a comment of many lines on one side is a comment to its end, and not on the other
    [TestMethod]
    public void TestEachSideIsColoredFromItsLinesBefore()
    {
        var rows = RowsOf(
            FileOf(
                "a.cs",
                DiffMode.DiffModified,
                SectionOf(
                    1,
                    1,
                    (DiffMode.DiffRemoved, "/* was"),
                    (DiffMode.DiffAdded, "int now;"),
                    (DiffMode.DiffSame, "int x; */ int y;")
                )
            )
        );
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(new FakeCodeGrammar()));

        Assert.AreEqual(
            """
            R   R // ///           │ G   G KKK WWWW
                D /// // // WWW WW │     D KKK WW WW KKK WW
            """,
            ColorsOf(rows, syntax)
        );
    }

    // Where lines are left out between two hunks, the next starts afresh, as the top of a file does
    [TestMethod]
    public void TestAHunkStartsAfresh()
    {
        var rows = RowsOf(
            FileOf(
                "a.cs",
                DiffMode.DiffModified,
                SectionOf(1, 1, (DiffMode.DiffSame, "/* open"), (DiffMode.DiffAdded, "still")),
                SectionOf(20, 21, (DiffMode.DiffSame, "int x;"), (DiffMode.DiffAdded, "int y;"))
            )
        );
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(new FakeCodeGrammar()));

        Assert.AreEqual(
            """
                D // //// │     D // ////
                          │ G   G /////
               DD KKK WW  │    DD KKK WW
                          │ G  GG KKK WW
            """,
            ColorsOf(rows, syntax)
        );
    }

    // A file added whole is drawn in one column, and colored as the new file
    [TestMethod]
    public void TestAFileAddedWholeIsColored()
    {
        var rows = RowsOf(
            FileOf(
                "a.cs",
                DiffMode.DiffAdded,
                SectionOf(0, 1, (DiffMode.DiffAdded, "/* a"), (DiffMode.DiffAdded, "b */ int c;"))
            )
        );
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(new FakeCodeGrammar()));

        Assert.AreEqual(
            """
            G   G // /
            G   G / // WWW WW
            """,
            ColorsOf(rows, syntax)
        );
    }

    // In one column an unchanged line is a line of both files, which each side's lines go on from
    [TestMethod]
    public void TestOneColumnColorsBothFiles()
    {
        var rows = RowsOf(
            FileOf(
                "a.cs",
                DiffMode.DiffModified,
                SectionOf(
                    1,
                    1,
                    (DiffMode.DiffSame, "/* both"),
                    (DiffMode.DiffRemoved, "old */ int a;"),
                    (DiffMode.DiffAdded, "new */ int b;")
                )
            ),
            isOneColumn: true
        );
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(new FakeCodeGrammar()));

        Assert.AreEqual(
            """
                D // ////
            R   R /// // WWW -W
            G   G /// // WWW +W
            """,
            ColorsOf(rows, syntax)
        );
    }

    // A line is colored once, however often it is drawn
    [TestMethod]
    public void TestEachLineIsColoredOnce()
    {
        var rows = RowsOf(
            FileOf(
                "a.cs",
                DiffMode.DiffModified,
                SectionOf(1, 1, (DiffMode.DiffSame, "int a;"), (DiffMode.DiffRemoved, "b"), (DiffMode.DiffAdded, "c"))
            )
        );
        var grammar = new FakeCodeGrammar();
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(grammar));

        ColorsOf(rows, syntax);
        ColorsOf(rows, syntax);

        CollectionAssert.AreEquivalent(new[] { "int a;", "b", "int a;", "c" }, grammar.Lines);
    }

    // Reaching the end of a large file added whole colors it from a few lines above, not from its top
    [TestMethod]
    public void TestALineFarDownIsColoredFromAFewLinesAbove()
    {
        var lines = Enumerable.Range(1, 5000).Select(i => (DiffMode.DiffAdded, $"line {i}")).ToArray();
        var rows = RowsOf(FileOf("a.cs", DiffMode.DiffAdded, SectionOf(0, 1, lines)));
        var grammar = new FakeCodeGrammar();
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(grammar));

        syntax.Colored(rows.Rows[^1].Left, rows.Count - 1, false);

        Assert.AreEqual(101, grammar.Lines.Count);
        Assert.AreEqual("line 5000", grammar.Lines[^1]);
    }

    // A file no grammar is for is drawn as it is
    [TestMethod]
    public void TestAFileWithNoGrammarIsAsItIs()
    {
        var rows = RowsOf(FileOf("a.txt", DiffMode.DiffModified, SectionOf(1, 1, (DiffMode.DiffSame, "int a;"))));
        var syntax = new DiffSyntax(rows, new FakeCodeGrammars(new FakeCodeGrammar()));

        var row = rows.Rows[^1];
        Assert.AreSame(row.Left, syntax.Colored(row.Left, rows.Count - 1, false));
    }
}
