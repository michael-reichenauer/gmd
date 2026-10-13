using gmd.Cui.Blame;
using gmd.Cui.Common;

namespace gmdTest.Cui.Blame;

[TestClass]
public class BlameHintsTest
{
    // 'p' for a line with an older version, and Backspace after stepping back to one
    [TestMethod]
    public void TestBlameOffersPreviousAndBackWhereTheyAct()
    {
        Assert.AreEqual(
            "m menu  Esc close  Enter details  d diff  l line history  i copy id  g gutter",
            Line(BlameHints.For(isDetailsShown: false, hasPrevious: false, canGoBack: false))
        );
        Assert.AreEqual(
            "m menu  Esc close  Enter hide details  d diff  l line history  p previous  Backspace back  i copy id  g gutter",
            Line(BlameHints.For(isDetailsShown: true, hasPrevious: true, canGoBack: true))
        );
    }

    static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));
}
