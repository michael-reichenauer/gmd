using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

[TestClass]
public class LineWordsTest
{
    // The runs of a side as text with the changed ones in brackets
    static string Marked(IReadOnlyList<WordRun> runs) =>
        string.Concat(runs.Select(r => r.IsChanged ? $"[{r.Text}]" : r.Text));

    // A word changed is marked whole, where the character diff marked the letters the two words do
    // not share, 'co' and 'st' of 'const' against 'readonly'
    [TestMethod]
    public void TestAChangedWordIsMarkedWhole()
    {
        var words = LineWords.Of("    const decimal rate;", "    readonly decimal rate;");

        Assert.AreEqual("    [const] decimal rate;", Marked(words.Left));
        Assert.AreEqual("    [readonly] decimal rate;", Marked(words.Right));
    }

    // Punctuation is a piece of its own, so a call wrapped around an expression marks the call alone
    [TestMethod]
    public void TestPunctuationIsAPieceOfItsOwn()
    {
        var words = LineWords.Of("return sum + sum * VatRate;", "return Math.Round(sum + sum * vatRate, 2);");

        Assert.AreEqual("return sum + sum * [VatRate];", Marked(words.Left));
        Assert.AreEqual("return [Math.Round(]sum + sum * [vatRate, 2)];", Marked(words.Right));
        Assert.IsTrue(words.IsAlike, $"Most of it is the same: {words.Likeness}");
    }

    // The differ may match a space in one place as well as another, which split a change in two
    // around a space and marked a space before it: a space between two changes is part of them, and
    // a change starts and ends with a word, unless it is a change of space alone
    [TestMethod]
    public void TestAChangeIsMarkedAsOneRunOfWords()
    {
        var words = LineWords.Of("return sum * VatRate;", "return sum * vatRate, 2);");

        Assert.AreEqual("return sum * [VatRate];", Marked(words.Left));
        Assert.AreEqual("return sum * [vatRate, 2)];", Marked(words.Right));
        Assert.AreEqual(
            "[if (]customer.IsMember[)]",
            Marked(LineWords.Of("if (customer.IsMember)", "return customer.IsMember ? 0.05m : 0m;").Left)
        );
    }

    // Lines that share little are not alike, and their words are not worth marking
    [TestMethod]
    public void TestLinesThatShareLittleAreNotAlike()
    {
        var words = LineWords.Of("return customer.IsMember ? 0.05m : 0m;", "if (customer.IsGold)");

        Assert.IsFalse(words.IsAlike, $"Too little is the same: {words.Likeness}");
    }

    [TestMethod]
    public void TestTheSameLinesAreWhollyAlike()
    {
        var words = LineWords.Of("same", "same");

        Assert.AreEqual(1, words.Likeness);
        Assert.AreEqual("same", Marked(words.Left));
    }

    // Space between words that differs is a change, unless whitespace is ignored, which also leaves it
    // out of the likeness
    [TestMethod]
    public void TestSpaceThatDiffersIsAChangeUnlessIgnored()
    {
        Assert.AreEqual("a[  ]b", Marked(LineWords.Of("a  b", "a b").Left));
        Assert.AreEqual("a  b", Marked(LineWords.Of("a  b", "a b", isIgnoreWhitespace: true).Left));
        Assert.AreEqual("a+b", Marked(LineWords.Of("a+b", "a + b", isIgnoreWhitespace: true).Left));
        Assert.AreEqual("a + b", Marked(LineWords.Of("a+b", "a + b", isIgnoreWhitespace: true).Right));
    }

    [TestMethod]
    public void TestEmptyLinesAreAlike()
    {
        Assert.IsTrue(LineWords.Of("", "").IsAlike);
    }
}
