using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// Text is laid out in terminal columns, not in UTF-16 chars, which a row of the log was: a subject in
// Japanese moved the columns after it, and a cut could split an emoji in two
[TestClass]
public class TextWidthTest
{
    [TestMethod]
    public void TestAWideCharacterIsTwoColumns()
    {
        Assert.AreEqual(5, TextWidth.Of("plain"));
        Assert.AreEqual(6, TextWidth.Of("日本語"));
        Assert.AreEqual(3, TextWidth.Of("é─●"), "Accented, box drawing and a dot are one each");
        Assert.AreEqual(1, TextWidth.Of("é"), "A combining mark is none");
    }

    // Drawn as '�', since Terminal.Gui 1.x's curses driver cannot draw it, so one column
    [TestMethod]
    public void TestACharacterOutsideTheBasicPlaneIsDrawnAsOneColumn()
    {
        Assert.AreEqual(2, "🎉".Length);
        Assert.AreEqual(1, TextWidth.Of("🎉"));
        Assert.AreEqual("Party � time", TextWidth.Drawn("Party 🎉 time"));
    }

    // A wide character cut by either end of a slice is spaces for its columns that are in it
    [TestMethod]
    public void TestASliceNeverSplitsACharacter()
    {
        Assert.AreEqual("本", TextWidth.Slice("日本語", 0, 2, 4));
        Assert.AreEqual(" 本 ", TextWidth.Slice("日本語", 0, 1, 5));
        Assert.AreEqual("a🎉", TextWidth.Slice("a🎉b", 0, 0, 2), "Not half of its surrogate pair");
        Assert.AreEqual("本語", TextWidth.Slice("本語", 10, 10, 20), "From where the text starts");
    }

    [TestMethod]
    public void TestFitCutsOrPadsToColumns()
    {
        Assert.AreEqual("山田太郎  ", TextWidth.Fit("山田太郎", 10));
        Assert.AreEqual("山田 ", TextWidth.Fit("山田太郎", 5));
        Assert.AreEqual("Test ", TextWidth.Fit("Test User", 5));
    }

    // What the log row is made of: the subject cut to its column, and the columns after it in place
    [TestMethod]
    public void TestTextIsLaidOutInColumns()
    {
        Text text = Text.White("日本語の").Dark("abc");

        Assert.AreEqual(11, text.Length);
        Assert.AreEqual("本語のab", text.Subtext(2, 8).ToString());
        Assert.AreEqual(8, text.Subtext(2, 8).Length);
        Assert.AreEqual("日本語の   ", Text.White("日本語の").ToText().Subtext(0, 11, true).ToString());
    }
}
