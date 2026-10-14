using gmd.Cui.App;

namespace gmdTest.Cui.App;

// The dev only list of character sets
[TestClass]
public class UnicodeSetsDlgTest
{
    // A character above U+FFFF was cut to its low 16 bits, so the Alchemical Symbols showed others
    [TestMethod]
    public void TestCharactersAboveTheBasicPlaneAreShown()
    {
        var text = string.Join('\n', UnicodeSetsDlg.GetContent().Select(t => t.ToString()));

        StringAssert.Contains(text, char.ConvertFromUtf32(0x1F700));
        Assert.IsFalse(text.Contains(''), "Not U+1F700 cut to 16 bits");
    }

    [TestMethod]
    public void TestEverySetIsListedOnce()
    {
        var titles = UnicodeSetsDlg
            .GetContent()
            .Select(t => t.ToString())
            .Where(t => t.StartsWith("  ") && !t.StartsWith("   "))
            .ToList();

        Assert.AreEqual(titles.Count, titles.Distinct().Count(), string.Join('\n', titles));
    }
}
