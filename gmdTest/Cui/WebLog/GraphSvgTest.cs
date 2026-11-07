using gmd.Cui.Common;
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
}
