using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// Both sides' changes to the common ancestor in one, where they touch no word in common: what the
// resolver offers as Combine. Written before the code, from the conflicts it is for.
[TestClass]
public class ConflictCombineTest
{
    static FileLine[] Lines(params string[] texts) => texts.Select(t => new FileLine(t)).ToArray();

    // The lines combined, each with where it came from: 'a' the ancestor, 'o' ours, 't' theirs, 'b' both
    static string Shown(IReadOnlyList<CombinedLine>? lines) =>
        lines == null
            ? "<none>"
            : string.Join(
                "\n",
                lines.Select(l =>
                    $"{l.Origin switch
                    {
                        LineOrigin.Ours => 'o',
                        LineOrigin.Theirs => 't',
                        LineOrigin.Both => 'b',
                        _ => 'a',
                    }} {l.Text}"
                )
            );

    // The test conflict's first one: main rounded the total and renamed VatRate, the branch added a
    // line above and changed the sums. Each touches other words of the line both changed.
    [TestMethod]
    public void TestChangesToOtherWordsOfALineAreCombined()
    {
        var combined = ConflictCombine.Of(
            Lines("        return sum + sum * VatRate;"),
            Lines("        return Math.Round(sum + sum * vatRate, 2);"),
            Lines(
                "        var discounted = sum - sum * Discount(order.Customer);",
                "        return discounted + discounted * VatRate;"
            )
        );

        Assert.AreEqual(
            """
            t         var discounted = sum - sum * Discount(order.Customer);
            b         return Math.Round(discounted + discounted * vatRate, 2);
            """,
            Shown(combined)
        );
    }

    // The test conflict's second one: main rewrote the three lines as one, the branch added two
    // above them, which go above main's line
    [TestMethod]
    public void TestLinesAddedAboveLinesRewrittenAreCombined()
    {
        var combined = ConflictCombine.Of(
            Lines("        if (customer.IsMember)", "            return 0.05m;", "        return 0m;"),
            Lines("        return customer.IsMember ? 0.05m : 0m;"),
            Lines(
                "        if (customer.IsGold)",
                "            return 0.10m;",
                "        if (customer.IsMember)",
                "            return 0.05m;",
                "        return 0m;"
            )
        );

        Assert.AreEqual(
            """
            t         if (customer.IsGold)
            t             return 0.10m;
            o         return customer.IsMember ? 0.05m : 0m;
            """,
            Shown(combined)
        );
    }

    // Each side changed another line, which git sets in one conflict where the lines touch
    [TestMethod]
    public void TestChangesToOtherLinesAreCombined()
    {
        var combined = ConflictCombine.Of(
            Lines("a = 1;", "b = 2;"),
            Lines("a = 10;", "b = 2;"),
            Lines("a = 1;", "b = 20;")
        );

        Assert.AreEqual("o a = 10;\nt b = 20;", Shown(combined));
    }

    // The same change on both sides is taken once
    [TestMethod]
    public void TestTheSameChangeIsTakenOnce()
    {
        var combined = ConflictCombine.Of(
            Lines("x = 1;", "y = 1;"),
            Lines("x = 2;", "y = 1;"),
            Lines("x = 2;", "y = 3;")
        );

        Assert.AreEqual("b x = 2;\nt y = 3;", Shown(combined));
    }

    // Both changed the same word, each in its own way, so there is no way to have both
    [TestMethod]
    public void TestTheSameWordChangedTwoWaysIsNotCombined()
    {
        Assert.IsNull(ConflictCombine.Of(Lines("rate = 0.25m;"), Lines("rate = 0.20m;"), Lines("rate = 0.30m;")));
    }

    // Both wrote lines in the same place, with nothing to say which go first, as where the ancestor
    // had nothing, both sides having added lines there
    [TestMethod]
    public void TestLinesBothAddedInOnePlaceAreNotCombined()
    {
        Assert.IsNull(ConflictCombine.Of([], Lines("ours"), Lines("theirs")));
        Assert.IsNull(ConflictCombine.Of(Lines("a"), Lines("o", "a"), Lines("t", "a")));
    }

    // One removed a line the other changed: having both would keep and remove it
    [TestMethod]
    public void TestALineRemovedAndChangedIsNotCombined()
    {
        Assert.IsNull(ConflictCombine.Of(Lines("keep", "x = 1;"), Lines("keep"), Lines("keep", "x = 2;")));
    }

    // Words written inside the words the other changed, rather than beside them, have no place left
    [TestMethod]
    public void TestWordsWrittenInsideTheOthersChangeAreNotCombined()
    {
        Assert.IsNull(ConflictCombine.Of(Lines("f(a, b);"), Lines("f(a, x, b);"), Lines("g(c);")));
    }

    // Lines both removed are gone, and lines one removed and the other kept too
    [TestMethod]
    public void TestRemovedLinesAreGone()
    {
        var combined = ConflictCombine.Of(
            Lines("one", "two", "three = 3;"),
            Lines("three = 3;"),
            Lines("one", "three = 30;")
        );

        Assert.AreEqual("t three = 30;", Shown(combined));
    }
}
