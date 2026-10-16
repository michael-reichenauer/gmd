using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// Where the text of a conflict being typed goes in the result pane: the file's lines above it, the
// rows of the text, and the lines after it
[TestClass]
public class ConflictTypingTest
{
    static FileLine[] Lines(params string[] texts) => texts.Select(t => new FileLine(t)).ToArray();

    // A row to spare below the text, for a line added at its end, and three of the file's lines
    // above it and two after, as the pane shows a conflict when not typing
    [TestMethod]
    public void TestTheTextHasTheFileAroundIt()
    {
        Assert.AreEqual(new ConflictTyping(7, 3, 3, 2), ConflictTyping.Of(10, 12, 30, 2, 14));
    }

    // Fewer where the file has fewer
    [TestMethod]
    public void TestAConflictNearTheStartHasLessAbove()
    {
        Assert.AreEqual(new ConflictTyping(0, 1, 2, 2), ConflictTyping.Of(1, 2, 5, 1, 14));
    }

    // A text longer than the pane is as tall as the pane has room for, and scrolls
    [TestMethod]
    public void TestALongTextFillsThePane()
    {
        Assert.AreEqual(new ConflictTyping(7, 3, 9, 2), ConflictTyping.Of(10, 30, 40, 20, 14));
    }

    // In a low pane the lines after the text give way first, then those above it
    [TestMethod]
    public void TestALowPaneGivesWayToTheText()
    {
        Assert.AreEqual(new ConflictTyping(8, 2, 3, 0), ConflictTyping.Of(10, 14, 30, 4, 5));
    }

    // The text's rows are its gutter, numbered as the file will be where a row is a line of the
    // result, counted from the row the text is scrolled to; the text view is laid over the rest
    [TestMethod]
    public void TestTheTextsRowsAreNumbered()
    {
        var file = new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [
                new ConflictSegment(Lines("a", "b"), null),
                new ConflictSegment([], new ConflictHunk(0, "HEAD", "", "topic", Lines("o"), [], Lines("t"))),
                new ConflictSegment(Lines("c"), null),
            ],
            true,
            true
        );
        var resolution = new ConflictResolution(file).WithTyped(0, "x\ny");
        var names = ConflictNames.Of(file, new ConflictSides("main", "", ""));
        var result = new ConflictRowService().ToResult(file, resolution, names);
        var typing = ConflictTyping.Of(result.Starts[0], result.Ends[0], result.Rows.Count, 2, 10);

        Assert.AreEqual(
            """
                1 a
                2 b
            ▌   3
            ▌   4
            ▌
                5 c
            """,
            Shown(typing.Rows(result, 0, 0))
        );
        Assert.AreEqual(
            """
                1 a
                2 b
            ▌   4
            ▌
            ▌
                5 c
            """,
            Shown(typing.Rows(result, 0, 1))
        );
    }

    static string Shown(IReadOnlyList<gmd.Cui.Common.Text> rows) =>
        string.Join("\n", rows.Select(r => r.ToString().TrimEnd()));
}
