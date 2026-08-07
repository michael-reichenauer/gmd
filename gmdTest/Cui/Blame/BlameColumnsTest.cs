using gmd.Cui.Blame;

namespace gmdTest.Cui.Blame;

[TestClass]
public class BlameColumnsTest
{
    [TestMethod]
    public void TestFullDetailOnAWideView()
    {
        var cw = BlameColumns.Calculate(BlameDetails.Full, 120, 100);

        Assert.AreEqual(6, cw.Sid);
        Assert.AreEqual(11, cw.Author);
        Assert.AreEqual(8, cw.Date);
        Assert.AreEqual(4, cw.LineNbr);
        Assert.AreEqual(36, cw.GutterWidth);
        Assert.AreEqual(120 - 36, cw.Code);
    }

    // The gutter is stepped down one column at a time rather than squeezing the code to nothing
    [TestMethod]
    [DataRow(120, 6, 11, 8)] // everything fits
    [DataRow(70, 6, 11, 8)] // still 34 left for code
    [DataRow(64, 6, 0, 8)] // author dropped
    [DataRow(50, 6, 0, 0)] // date dropped too
    [DataRow(35, 0, 0, 0)] // only the bracket and the line number are left
    public void TestNarrowViewDropsColumnsInOrder(int width, int sid, int author, int date)
    {
        var cw = BlameColumns.Calculate(BlameDetails.Full, width, 100);

        Assert.AreEqual(sid, cw.Sid);
        Assert.AreEqual(author, cw.Author);
        Assert.AreEqual(date, cw.Date);
    }

    // The bracket and the line number are what the view is for, they are never dropped
    [TestMethod]
    public void TestVeryNarrowViewKeepsTheBracketAndLineNumber()
    {
        var cw = BlameColumns.Calculate(BlameDetails.Full, 6, 100);

        Assert.AreEqual(0, cw.Sid);
        Assert.AreEqual(4, cw.LineNbr);
        Assert.AreEqual(BlameColumns.RunWidth + 2 + 4, cw.GutterWidth);
        Assert.AreEqual(0, cw.Code);
    }

    [TestMethod]
    public void TestLineNumberColumnGrowsWithTheFile()
    {
        Assert.AreEqual(4, BlameColumns.Calculate(BlameDetails.Full, 120, 9).LineNbr);
        Assert.AreEqual(4, BlameColumns.Calculate(BlameDetails.Full, 120, 9999).LineNbr);
        Assert.AreEqual(5, BlameColumns.Calculate(BlameDetails.Full, 120, 10000).LineNbr);
        Assert.AreEqual(6, BlameColumns.Calculate(BlameDetails.Full, 120, 123456).LineNbr);
    }

    // A detail the user chose is the starting point, it is only ever stepped further down
    [TestMethod]
    public void TestChosenDetailIsNotWidenedOnAWideView()
    {
        var cw = BlameColumns.Calculate(BlameDetails.Minimal, 200, 100);

        Assert.AreEqual(6, cw.Sid);
        Assert.AreEqual(0, cw.Author);
        Assert.AreEqual(0, cw.Date);
    }
}
