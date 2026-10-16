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

    // A long rewrite is paired line by line, a removed line looked for only so far past where the
    // alignment puts it: among every added line left, a thousand lines were half a million
    // comparisons, made anew with every decision in the resolver, seconds a key. Lines added above
    // the changed ones, fewer than the look ahead, are still added rather than paired.
    [TestMethod]
    public void TestALongRewriteIsPairedLineByLine()
    {
        var ancestor = Lines([.. Enumerable.Range(0, 1000).Select(i => $"var value{i} = Compute(a, b);")]);
        var side = Lines([
            .. Enumerable.Range(0, 10).Select(i => $"// added {i}"),
            .. Enumerable.Range(0, 1000).Select(i => $"var value{i} = Compute(a, c);"),
        ]);

        var lines = SideChanges.Of(ancestor, side);

        Assert.AreEqual(10, lines.Count(l => l.Kind == SideLineKind.Added));
        Assert.AreEqual(1000, lines.Count(l => l.Kind == SideLineKind.Changed));
        Assert.AreEqual("var value999 = Compute(a, c);", lines[^1].Text);
    }

    // A side's changes are made once for the lines of a file as read, and again for another ancestor
    [TestMethod]
    public void TestASidesChangesAreMadeOnce()
    {
        var (ancestor, side) = (Lines("one", "two"), Lines("one", "2"));

        Assert.AreSame(SideChanges.Of(ancestor, side), SideChanges.Of(ancestor, side));
        Assert.AreNotSame(SideChanges.Of(ancestor, side), SideChanges.Of(Lines("one", "two"), side));
    }

    // A carriage return alone is text in a line, as the parser keeps it, not the end of one: a line
    // diff that split on it as well put its pieces out of step with the lines and threw
    [TestMethod]
    public void TestALoneCarriageReturnIsPartOfItsLine()
    {
        var ancestor = Lines("a\rb", "total = 1", "d");
        var ours = Lines("a\rb", "total = 2", "d", "e");

        Assert.AreEqual("  a\rb\n~ total = [2]\n  d\n+ e", Shown(SideChanges.Of(ancestor, ours)));
    }
}
