using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// A side of a conflict as its change against the common ancestor: which of its lines it added,
// which it changed and in what words, which it kept, and which of the ancestor's it removed
[TestClass]
public class SideChangesTest
{
    static FileLine[] Lines(params string[] texts) => texts.Select(t => new FileLine(t)).ToArray();

    // Each line as its mark and text, a changed line with its words in brackets
    static string Shown(IReadOnlyList<SideLine> lines) =>
        string.Join(
            "\n",
            lines.Select(l =>
                $"{Mark(l.Kind)} {(l.Words.Count > 0 ? string.Concat(l.Words.Select(w => w.IsChanged ? $"[{w.Text}]" : w.Text)) : l.Text)}"
            )
        );

    static char Mark(SideLineKind kind) =>
        kind switch
        {
            SideLineKind.Added => '+',
            SideLineKind.Changed => '~',
            SideLineKind.Removed => '-',
            _ => ' ',
        };

    // The test conflict's second one: main rewrote the lines, the branch only added two above them
    [TestMethod]
    public void TestASideThatOnlyAddedLinesKeepsTheRest()
    {
        var ancestor = Lines("if (customer.IsMember)", "    return 0.05m;", "return 0m;");
        var theirs = Lines(
            "if (customer.IsGold)",
            "    return 0.10m;",
            "if (customer.IsMember)",
            "    return 0.05m;",
            "return 0m;"
        );

        Assert.AreEqual(
            """
            + if (customer.IsGold)
            +     return 0.10m;
              if (customer.IsMember)
                  return 0.05m;
              return 0m;
            """,
            Shown(SideChanges.Of(ancestor, theirs))
        );
    }

    // A line most of which is kept is changed, its words marked; the lines it no longer has are
    // removed, in the ancestor's words
    [TestMethod]
    public void TestASideThatRewroteLinesChangesAndRemovesThem()
    {
        var ancestor = Lines("if (customer.IsMember)", "    return 0.05m;", "return 0m;");
        var ours = Lines("return customer.IsMember ? 0.05m : 0m;");

        Assert.AreEqual(
            """
            ~ [return] customer.IsMember [? 0.05m : 0m;]
            -     return 0.05m;
            - return 0m;
            """,
            Shown(SideChanges.Of(ancestor, ours))
        );
    }

    // Each line of the side knows where it is in the side, for its number; a removed one is no line
    // of the side
    [TestMethod]
    public void TestEachLineKnowsItsPlaceInTheSide()
    {
        var lines = SideChanges.Of(Lines("x", "a = 1", "c"), Lines("x", "a = 2", "new", "c"));

        Assert.AreEqual("  x\n~ a = [2]\n+ new\n  c", Shown(lines));
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, lines.Select(l => l.SideIndex).ToArray());
        Assert.AreEqual(
            -1,
            SideChanges.Of(Lines("a", "b"), Lines("a")).Single(l => l.Kind == SideLineKind.Removed).SideIndex
        );
    }

    // Where the ancestor had nothing, both sides having added lines there, every line is added
    [TestMethod]
    public void TestAnEmptyAncestorMakesEveryLineAdded()
    {
        Assert.AreEqual("+ one\n+ two", Shown(SideChanges.Of([], Lines("one", "two"))));
    }

    // A line added above a changed one does not take its place: the removed line is paired with the
    // added line most like it, the test conflict's first one, where the branch added a line above the
    // return it changed
    [TestMethod]
    public void TestAChangedLineIsPairedWithTheMostAlike()
    {
        var ancestor = Lines("return sum + sum * VatRate;");
        var theirs = Lines(
            "var discounted = sum - sum * Discount(order.Customer);",
            "return discounted + discounted * VatRate;"
        );

        Assert.AreEqual(
            """
            + var discounted = sum - sum * Discount(order.Customer);
            ~ return [discounted] + [discounted] * VatRate;
            """,
            Shown(SideChanges.Of(ancestor, theirs))
        );
    }
}
