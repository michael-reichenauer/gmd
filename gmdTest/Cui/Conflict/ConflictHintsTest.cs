using gmd.Cui.Common;
using gmd.Cui.Conflict;

namespace gmdTest.Cui.Conflict;

[TestClass]
public class ConflictHintsTest
{
    // 1 to 4 and 0 are said by the result pane, for the conflict under the cursor
    [TestMethod]
    public void TestTheResolverOffersItsKeys()
    {
        Assert.AreEqual(
            "m menu  Esc close  ] [ next, previous  s save  e edit  b ancestor  a whole file  u clear",
            Line(ConflictHints.For())
        );
    }

    static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));
}
