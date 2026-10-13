using gmd.Cui.Common;
using gmd.Cui.Start;

namespace gmdTest.Cui.Start;

[TestClass]
public class StartHintsTest
{
    // The numbers only when it lists a repository, and Delete only on a recent one, which is all it
    // can take off the list
    [TestMethod]
    public void TestTheStartPageOffersTheNumbersAndDeleteWhereTheyAct()
    {
        Assert.AreEqual(
            "Enter open  1-4 open repository  o open folder  c clone  i init  Del remove from list  Esc quit",
            Line(StartHints.For(numbered: 4, canRemove: true))
        );
        Assert.AreEqual(
            "Enter open  1 open repository  o open folder  c clone  i init  Esc quit",
            Line(StartHints.For(numbered: 1, canRemove: false))
        );
        Assert.AreEqual(
            "Enter open  o open folder  c clone  i init  Esc quit",
            Line(StartHints.For(numbered: 0, canRemove: false))
        );
    }

    static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));
}
