using gmd.Cui.Common;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

// Lines colored from the top, each from the state the line before ended in. Coloring from a few lines
// above a line far down is BlameSyntaxTest's, the blame being colored so.
[TestClass]
public class CodeLinesTest
{
    // A row that is no line of the file has no code, and the state goes on past it, so a comment
    // opened above it is still one below it
    [TestMethod]
    public void TestTheStateGoesOnPastARowOfNoCode()
    {
        var lines = new CodeLines(["/* start", null, "still */ int b;"], new FakeCodeGrammar());

        Assert.AreEqual(0, lines.SpansOf(1).Count);
        Assert.AreEqual(new CodeSpan(0, 8, ColorName.CodeComment), lines.SpansOf(2)[0]);
    }

    // The lines of another version keep their colors as far as the two versions are the same, and the
    // lines from the first that differs on are colored again, as the resolver's result is with each
    // decision, from the state the line before ended in
    [TestMethod]
    public void TestTheLinesTheSameAsBeforeKeepTheirColors()
    {
        var grammar = new FakeCodeGrammar();
        var before = new CodeLines(["/* start", "still */", "int a;", "int b;"], grammar);
        before.SpansOf(3);
        grammar.Lines.Clear();

        var lines = new CodeLines(["/* start", "still */", "int c;", "int b;"], grammar, before);

        Assert.AreEqual(new CodeSpan(0, 8, ColorName.CodeComment), lines.SpansOf(1).Single());
        Assert.AreEqual(0, grammar.Lines.Count, "Kept");
        Assert.AreEqual(new CodeSpan(0, 3, ColorName.CodeKeyword), lines.SpansOf(3).Single());
        CollectionAssert.AreEqual(new[] { "int c;", "int b;" }, grammar.Lines);
    }

    // Only lines colored before are kept, not the ones never drawn, which are colored when they are
    [TestMethod]
    public void TestOnlyTheLinesColoredBeforeAreKept()
    {
        var grammar = new FakeCodeGrammar();
        var before = new CodeLines(["int a;", "int b;", "int c;"], grammar);
        before.SpansOf(0);
        grammar.Lines.Clear();

        var lines = new CodeLines(["int a;", "int b;", "int c;"], grammar, before);
        lines.SpansOf(2);

        CollectionAssert.AreEqual(new[] { "int b;", "int c;" }, grammar.Lines);
    }
}
