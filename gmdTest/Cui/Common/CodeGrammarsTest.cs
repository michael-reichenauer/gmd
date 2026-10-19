using gmd.Cui.Common;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

// The grammars of TextMateSharp, with Oniguruma, the native library their regular expressions are
// run by, which a build that left it out would show here first: no line would be colored.
[TestClass]
public class CodeGrammarsTest
{
    // One letter per char of the line, as TextColors spells the code colors, ' ' for the text's own
    static string ColorsOf(string line, CodeLine colored)
    {
        var colors = new string(' ', line.Length).ToCharArray();
        foreach (var span in colored.Spans)
        {
            var letter = TextColors.Of(Text.Color(new Color(span.Color, ColorName.Background), "x"));
            for (var i = span.Start; i < span.Start + span.Length; i++)
                colors[i] = letter[0];
        }
        return new string(colors).TrimEnd();
    }

    [TestMethod]
    public void TestCSharpIsColored()
    {
        var grammar = new CodeGrammars().For("src/Total.cs")!;
        var line = "var total = 42; // \"so far\"";

        Assert.AreEqual("KKK VVVVV   ##  ///////////", ColorsOf(line, grammar.Tokenize(line, null)));
    }

    // A line is colored as the line before left it, here inside a comment of two lines
    [TestMethod]
    public void TestALineIsColoredFromTheStateTheLineBeforeEndedIn()
    {
        var grammar = new CodeGrammars().For("Total.cs")!;
        var first = grammar.Tokenize("/* a comment", null);
        var line = "   ending */ int x;";

        Assert.AreEqual("//////////// KKK V", ColorsOf(line, grammar.Tokenize(line, first.State)));
        Assert.IsFalse(ColorsOf(line, grammar.Tokenize(line, null)).StartsWith("   ///"), "No comment without it");
    }

    // By the extension, in any case; a file with none, or one no grammar is for, has none
    [TestMethod]
    public void TestTheGrammarIsFoundByTheExtension()
    {
        var grammars = new CodeGrammars();
        Assert.IsNotNull(grammars.For("README.MD"));
        Assert.IsNotNull(grammars.For("a/b/tool.py"));
        Assert.IsNull(grammars.For("Makefile"));
        Assert.IsNull(grammars.For("data.unknown-extension"));
        Assert.AreSame(grammars.For("one.cs"), grammars.For("two.cs"), "One grammar per language");
    }

    // A line too long to color in time is left as it is, and the state goes on from the line before
    [TestMethod]
    public void TestAVeryLongLineIsNotColored()
    {
        var grammar = new CodeGrammars().For("Data.cs")!;
        var state = grammar.Tokenize("/* a comment", null).State;
        var line = new string('x', CodeGrammars.MaxLineLength + 1);

        var colored = grammar.Tokenize(line, state);

        Assert.AreEqual(0, colored.Spans.Count);
        Assert.AreSame(state, colored.State);
    }
}
