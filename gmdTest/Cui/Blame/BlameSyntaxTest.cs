using gmd.Cui.Blame;
using gmd.Cui.Common;
using gmdTest.Fixtures;
using ServerBlame = gmd.Server.Blame;
using ServerBlameCommit = gmd.Server.BlameCommit;
using ServerBlameLine = gmd.Server.BlameLine;

namespace gmdTest.Cui.Blame;

// The code of a blame's lines in its language's colors, line after line from the top of the file
[TestClass]
public class BlameSyntaxTest
{
    const string Id = "1111111111111111111111111111111111111111";

    static BlameRows RowsOf(params string[] lines)
    {
        var commit = new ServerBlameCommit(Id, "11111111", "A", "", DateTime.Now, "S", false, false, "", "", "a.cs");
        var blameLines = lines.Select((l, i) => new ServerBlameLine(Id, i + 1, i + 1, l)).ToList();
        var blame = new ServerBlame(
            "a.cs",
            "",
            blameLines,
            new Dictionary<string, ServerBlameCommit> { [Id] = commit }
        );
        return new BlameRowService().ToBlameRows(blame);
    }

    // A line is colored after the lines above it, so the end of a comment opened two lines up is
    // colored as one, and each line only once
    [TestMethod]
    public void TestALineIsColoredAfterTheLinesAboveIt()
    {
        var grammar = new FakeCodeGrammar();
        var syntax = new BlameSyntax(RowsOf("int a;", "/* start", "still */ int b;", "int c;"), grammar);

        Assert.AreEqual(new CodeSpan(0, 8, ColorName.CodeComment), syntax.SpansOf(2).Single());
        CollectionAssert.AreEqual(new[] { "int a;", "/* start", "still */ int b;" }, grammar.Lines);

        Assert.AreEqual(new CodeSpan(0, 3, ColorName.CodeKeyword), syntax.SpansOf(3).Single());
        Assert.AreEqual(new CodeSpan(0, 3, ColorName.CodeKeyword), syntax.SpansOf(0).Single());
        Assert.AreEqual(4, grammar.Lines.Count, "The lines above were colored already");
    }

    // A line far below the last one colored, as after End in a large file, is colored from a few lines
    // above it rather than from the top
    [TestMethod]
    public void TestALineFarDownIsColoredFromAFewLinesAbove()
    {
        var grammar = new FakeCodeGrammar();
        var syntax = new BlameSyntax(RowsOf(Enumerable.Range(1, 3000).Select(i => $"int x{i};").ToArray()), grammar);

        Assert.AreEqual(1, syntax.SpansOf(2999).Count);
        Assert.AreEqual(101, grammar.Lines.Count);
        Assert.AreEqual("int x2900;", grammar.Lines[0]);
    }

    // A row not in the blame has no code
    [TestMethod]
    public void TestARowNotInTheBlameHasNoSpans()
    {
        var syntax = new BlameSyntax(RowsOf("int a;"), new FakeCodeGrammar());

        Assert.AreEqual(0, syntax.SpansOf(1).Count);
        Assert.AreEqual(0, syntax.SpansOf(-1).Count);
    }
}
