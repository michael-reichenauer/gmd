using gmd.Cui.Blame;
using gmd.Cui.Common;

namespace gmdTest.Cui.Blame;

[TestClass]
public class BlameHintsTest
{
    // 'p' for a line with an older version, and Backspace after stepping back to one; '⇧l' the history
    // of the whole file beside 'l' for the lines'
    [TestMethod]
    public void TestBlameOffersPreviousAndBackWhereTheyAct()
    {
        Assert.AreEqual(
            "m menu  Esc close  Enter details  d diff  l line history  ⇧l file history  f search  i copy id  g gutter",
            Line(BlameHints.For(isDetailsShown: false, hasPrevious: false, canGoBack: false, hasSearch: false))
        );
        Assert.AreEqual(
            "m menu  Esc close  Enter hide details  d diff  l line history  ⇧l file history  p previous  Backspace back  f search  i copy id  g gutter",
            Line(BlameHints.For(isDetailsShown: true, hasPrevious: true, canGoBack: true, hasSearch: false))
        );
    }

    // n goes on to the next match once a search found any, as in the diff
    [TestMethod]
    public void TestASearchWithMatchesOffersTheNextOne()
    {
        Assert.AreEqual(
            "m menu  Esc close  Enter details  d diff  l line history  ⇧l file history  n next match  f search  i copy id  g gutter",
            Line(BlameHints.For(isDetailsShown: false, hasPrevious: false, canGoBack: false, hasSearch: true))
        );
    }

    static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));
}
