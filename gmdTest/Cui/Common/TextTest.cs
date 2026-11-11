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

    // The mark of the commit marked for a diff is left as it is too, so that it is seen at once on the
    // cursor's row, which is where a commit is marked
    [TestMethod]
    public void TestAHighlightLeavesADiffMarkAsItIs()
    {
        var marked = Text.Color(Color.DiffMark, "±").White(" Subject").ToText();

        Assert.AreEqual(Color.DiffMark, marked.ToHighlight().Fragments[0].Color);
        Assert.AreEqual(Color.DiffMark, marked.ToSelect().Fragments[0].Color);
        Assert.AreEqual(Color.DiffMark, marked.ToTextBuilder().Highlight().ToText().Fragments[0].Color);
        Assert.AreEqual(
            ColorName.CursorBg,
            marked.ToHighlight().Fragments[1].Color.Background,
            "The rest is highlighted"
        );
    }

    // The cursor's row keeps the colors of its text, e.g. of the ahead and behind marks and the tags,
    // on the cursor's background; only the dark gray, too dim there, is the lighter gray made for it
    [TestMethod]
    public void TestTheCursorsRowKeepsItsColors()
    {
        var text = Text.BrightBlue("▼").White("Subject").Green(" [v1.0]").Dark(" Author").ToText();

        ColorName[] expected = [ColorName.BrightBlue, ColorName.White, ColorName.Green, ColorName.OnCursorDim];
        CollectionAssert.AreEqual(expected, text.ToHighlight().Fragments.Select(f => f.Color.Foreground).ToArray());
        CollectionAssert.AreEqual(
            expected,
            text.ToTextBuilder().Highlight().ToText().Fragments.Select(f => f.Color.Foreground).ToArray()
        );
        Assert.IsTrue(text.ToHighlight().Fragments.All(f => f.Color.Background == ColorName.CursorBg));
    }

    // With the 16 colors the cursor's row is as it always was, the dark gray with its dim text white
    [TestMethod]
    public void TestTheCursorsRowOfThe16ColorsIsDarkGray()
    {
        Assert.AreEqual(Palette.Sixteen[ColorName.Dark], Palette.Sixteen[ColorName.CursorBg]);
        Assert.AreEqual(Palette.Sixteen[ColorName.White], Palette.Sixteen[ColorName.OnCursorDim]);
        Assert.AreEqual(Palette.Sixteen[ColorName.Magenta], Palette.Sixteen[ColorName.OnCursorMagenta]);
        Assert.AreEqual(Palette.Sixteen[ColorName.Blue], Palette.Sixteen[ColorName.OnCursorBlue]);
    }

    // The main branch's magenta and the blue are too dark to read on the cursor's tint, so they have
    // colors of their own there, lighter in the dark palette
    [TestMethod]
    public void TestTheDarkestColorsAreLighterOnTheCursorsRow()
    {
        var text = Text.Magenta("┣").Blue(" old").ToText();

        ColorName[] expected = [ColorName.OnCursorMagenta, ColorName.OnCursorBlue];
        CollectionAssert.AreEqual(expected, text.ToHighlight().Fragments.Select(f => f.Color.Foreground).ToArray());
        CollectionAssert.AreEqual(
            expected,
            text.ToTextBuilder().Highlight().ToText().Fragments.Select(f => f.Color.Foreground).ToArray()
        );
    }

    // Dark is white on the background of the row marked for a diff, which is to stand out, as it is on
    // the cursor's, and the other colors are kept
    [TestMethod]
    public void TestDarkIsWhiteOnTheMarkedRow()
    {
        var text = Text.Dark("Author").Cyan(" c2000000").ToText();

        Assert.AreEqual(
            new Color(ColorName.White, ColorName.MarkBg),
            text.ToHighlight(Color.MarkBg).Fragments[0].Color
        );
        Assert.AreEqual(new Color(ColorName.Cyan, ColorName.MarkBg), text.ToHighlight(Color.MarkBg).Fragments[1].Color);
        Assert.AreEqual(
            new Color(ColorName.White, ColorName.MarkBg),
            text.ToTextBuilder().Highlight(Color.MarkBg).ToText().Fragments[0].Color
        );
    }

    // Code keeps its colors on the cursor's row, but selected it is the selection's text, as its colors
    // are made for the background, not for the selection's
    [TestMethod]
    public void TestCodeSelectedIsTheSelectionsText()
    {
        var keyword = new Color(ColorName.CodeKeyword, ColorName.Background);
        var code = Text.Color(keyword, "int").White(" x").ToText();

        Assert.AreEqual(new Color(ColorName.CodeKeyword, ColorName.CursorBg), code.ToHighlight().Fragments[0].Color);
        Assert.AreEqual(new Color(ColorName.Black, ColorName.White), code.ToSelect().Fragments[0].Color);
        Assert.AreEqual(new Color(ColorName.Black, ColorName.White), code.ToTextBuilder().Select().Fragments[0].Color);
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
