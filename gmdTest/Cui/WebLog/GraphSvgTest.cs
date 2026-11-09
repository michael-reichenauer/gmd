using gmd.Cui.Common;
using gmd.Cui.Graphs;
using gmd.Cui.WebLog;

namespace gmdTest.Cui.WebLog;

[TestClass]
public class GraphSvgTest
{
    [TestMethod]
    public void DrawsABranchLineWithTheCommitAsADot()
    {
        var svg = GraphSvg.Row(Text.Color(Color.Magenta, "┣"), 0, 1);

        Assert.AreEqual(
            "<svg class=\"g\" width=\"9\" height=\"40\" viewBox=\"0 0 9 40\" aria-hidden=\"true\">"
                + "<path class=\"h c-Magenta\" d=\"M4.5 0V40\"/>"
                + "<circle class=\"d c-Magenta\" cx=\"4.5\" cy=\"20\" r=\"4.5\"/></svg>",
            svg
        );
    }

    [TestMethod]
    public void DrawsAMergeAsAnArc()
    {
        var svg = GraphSvg.Row(Text.Color(Color.Magenta, "┣").Blue("╮"), 0, 2);

        StringAssert.Contains(svg, "<path class=\"l c-Blue\" d=\"M9 20Q13.5 20 13.5 40\"/>");
    }

    [TestMethod]
    public void DrawsNoDotOnARowOfAnotherBranch()
    {
        var svg = GraphSvg.Row(Text.Color(Color.Magenta, "┃"), -1, 1);

        Assert.IsFalse(svg.Contains("<circle"), svg);
    }

    [TestMethod]
    public void DrawsTheLineThroughACommitSetByTheUser()
    {
        var svg = GraphSvg.Row(Text.Color(Color.White, "Φ"), 0, 1);

        StringAssert.Contains(svg, "<path class=\"h c-White\" d=\"M4.5 0V40\"/>");
        StringAssert.Contains(svg, "<circle class=\"d c-White\"");
    }

    [TestMethod]
    public void DrawsABranchMergedInAndOneBranchedOutAtOneCommitApart()
    {
        // ┣┤ in the terminal: blue merged in from below and green branched out above, at main's
        // commit, both in one white rune
        var row = new GraphRow(2);
        row.SetConnect(1, Sign.MergeFromRight, Color.Blue);
        row.SetConnect(1, Sign.BranchToRight, Color.Green);

        var svg = GraphSvg.Row(Text.Color(Color.Magenta, "┣").White("┤").Color(Color.Black, " "), 0, 3, row);

        // Each in its own color, the one going up above the one coming from below
        StringAssert.Contains(svg, "<path class=\"l c-Green\" d=\"M9 18Q13.5 18 13.5 0\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Blue\" d=\"M9 22Q13.5 22 13.5 40\"/>");
        Assert.IsFalse(svg.Contains("c-White"), svg);
    }

    [TestMethod]
    public void DrawsTheHorizontalLinesOfTwoBranchesSideBySide()
    {
        // ┣──┤: the two lines run to the right through a column before they turn
        var row = new GraphRow(3);
        foreach (var color in new[] { Color.Blue, Color.Green })
        {
            row.SetGraphBranchPass(1, Sign.Pass, color);
            row.SetGraphPass(1, Sign.Pass, color);
        }
        row.SetConnect(2, Sign.MergeFromRight, Color.Blue);
        row.SetConnect(2, Sign.BranchToRight, Color.Green);

        var svg = GraphSvg.Row(Text.Color(Color.Magenta, "┣").White("──┤").Color(Color.Black, " "), 0, 5, row);

        StringAssert.Contains(svg, "<path class=\"l c-Green\" d=\"M9 18H18\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Blue\" d=\"M9 22H18\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Green\" d=\"M18 18H27\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Blue\" d=\"M18 22H27\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Green\" d=\"M27 18Q31.5 18 31.5 0\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Blue\" d=\"M27 22Q31.5 22 31.5 40\"/>");
    }

    [TestMethod]
    public void DrawsATurnIntoALineOfAnotherColorStraight()
    {
        // ┣┤: a hidden branch merged in where a yellow line passes, white in the terminal
        var row = new GraphRow(2);
        row.SetConnect(1, Sign.ConnectLine, Color.Yellow);
        row.SetConnect(1, Sign.MergeFromRight, Color.Dark);

        var svg = GraphSvg.Row(Text.Color(Color.Magenta, "┣").White("┤").Color(Color.Black, " "), 0, 3, row);

        StringAssert.Contains(svg, "<path class=\"l c-Yellow\" d=\"M13.5 0V40\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Dark\" d=\"M9 20H13.5\"/>");
    }

    [TestMethod]
    public void DrawsARowOfOneColorFromItsRunes()
    {
        var row = new GraphRow(2);
        row.SetConnect(1, Sign.MergeFromRight, Color.Blue);
        var text = Text.Color(Color.Magenta, "┣").Blue("╮").Color(Color.Black, " ");

        Assert.AreEqual(GraphSvg.Row(text, 0, 3), GraphSvg.Row(text, 0, 3, row));
    }

    [TestMethod]
    public void DrawsABranchMergedInBesideOneBranchedOutFurtherRightApart()
    {
        // ┣┬┃╯: blue merged in from the column beside, green branched out from one further right,
        // its line passing over blue's turn
        var row = new GraphRow(3);
        row.SetConnect(1, Sign.MergeFromRight, Color.Blue);
        row.SetGraphBranchPass(1, Sign.Pass, Color.Green);
        row.SetGraphPass(1, Sign.Pass, Color.Green);
        row.SetConnect(2, Sign.BranchToRight, Color.Green);

        var svg = GraphSvg.Row(
            Text.Color(Color.Magenta, "┣").White("┬").Green("─╯").Color(Color.Black, " "),
            0,
            5,
            row
        );

        StringAssert.Contains(svg, "<path class=\"l c-Blue\" d=\"M9 22Q13.5 22 13.5 40\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Green\" d=\"M9 18H18\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Green\" d=\"M18 18H27\"/>");
        StringAssert.Contains(svg, "<path class=\"l c-Green\" d=\"M27 18Q31.5 18 31.5 0\"/>");
    }

    [TestMethod]
    public void DrawsNoLineLeftOfABranchOutToTheLeft()
    {
        // ┃╰┣: a blue branch out to the left of a green commit, its line passing the turn's cell
        // as it is set, where a red line runs too
        var row = new GraphRow(2);
        row.SetConnect(1, Sign.BranchToLeft, Color.Blue);
        row.SetGraphPass(1, Sign.Pass, Color.Blue);
        row.SetConnect(1, Sign.ConnectLine, Color.Red);

        var svg = GraphSvg.Row(Text.Color(Color.Magenta, "┃").White("┼").Green("┣"), 2, 3, row);

        StringAssert.Contains(svg, "<path class=\"l c-Blue\" d=\"M18 20H13.5\"/>");
        Assert.IsFalse(svg.Contains("c-Blue\" d=\"M9 20H18"), svg);
    }
}
