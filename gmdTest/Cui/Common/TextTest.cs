using gmd.Cui.Common;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

[TestClass]
public class TextTest
{
    // Each match is marked, across the fragments it spans, and the colors around it are kept
    [TestMethod]
    public void TestTheMatchesOfASearchAreMarked()
    {
        var text = Text.Dark("   1┃").Green("var x").White(" = Xy;").ToText();

        var marked = text.WithMatches("x", 5);

        Assert.AreEqual(text.ToString(), marked.ToString(), "The same text");
        Assert.AreEqual("   DDGGG * W *WW", TextColors.Of(marked), "Not the gutter's, from 5 on");
        Assert.AreEqual("   DDGGG******WW", TextColors.Of(text.WithMatches(" x = x")), "Across fragments");
    }

    // The highlight of the cursor's row and of a selection leave the match marked: the cursor is put on
    // the row a match is on, so it is where the mark is looked for
    [TestMethod]
    public void TestAHighlightLeavesAMatchMarked()
    {
        var marked = Text.White("a match here").ToText().WithMatches("match");

        Assert.AreEqual(Color.Match, marked.ToHighlight().Fragments[1].Color);
        Assert.AreEqual(Color.Match, marked.ToSelect().Fragments[1].Color);
        Assert.AreEqual(Color.Match, marked.ToTextBuilder().Highlight().ToText().Fragments[1].Color);
        Assert.AreNotEqual(Color.White, marked.ToHighlight().Fragments[0].Color, "The rest is highlighted");
    }

    // No search, or none of it in the text, leaves the text as it is
    [TestMethod]
    public void TestATextWithNoMatchIsLeftAsItIs()
    {
        var text = Text.White("nothing").ToText();

        Assert.AreSame(text, text.WithMatches(""));
        Assert.AreSame(text, text.WithMatches("absent"));
    }

    // A builder made from a text is as long as the text, which it was not: it counted only what was
    // added to it after, so a separator meant for after a text was left out when it was not empty
    [TestMethod]
    public void TestABuilderOfATextIsAsLongAsTheText()
    {
        var text = Text.Cyan("abc").White(" de").ToText();

        Assert.AreEqual(6, text.ToTextBuilder().Length);
        Assert.AreEqual(8, text.ToTextBuilder().Dark("..").Length);
    }
}
