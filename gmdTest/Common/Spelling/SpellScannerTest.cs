using gmd.Common.Spelling;

namespace gmdTest.Common.Spelling;

[TestClass]
public class SpellScannerTest
{
    static string[] Words(string line) => SpellScanner.Words(line).Select(w => w.Word).ToArray();

    [TestMethod]
    public void TestPlainWords()
    {
        CollectionAssert.AreEqual(new[] { "Fix", "the", "fetch", "issue" }, Words("Fix the fetch issue"));
        CollectionAssert.AreEqual(new string[0], Words(""));
        CollectionAssert.AreEqual(new string[0], Words("   "));
    }

    [TestMethod]
    public void TestSurroundingPunctuationIsNotPartOfTheWord()
    {
        CollectionAssert.AreEqual(
            new[] { "Move", "some", "items", "quoted", "and", "more", "done" },
            Words("Move (some) items, 'quoted' and \"more\": done.")
        );
        CollectionAssert.AreEqual(new[] { "force", "flag" }, Words("--force <flag>"));
    }

    [TestMethod]
    public void TestIdentifiersPathsAndReferencesAreSkipped()
    {
        CollectionAssert.AreEqual(
            new[] { "Fix", "in", "for", "and" },
            Words("Fix CommitDlg in gmd/Cui/CommitDlg.cs for #123 and sha1 abc1234 v1.2 net10.0 a_b x=y user@host")
        );
    }

    [TestMethod]
    public void TestAcronymsAndCamelCaseAreSkipped()
    {
        CollectionAssert.AreEqual(new[] { "Use", "and", "with" }, Words("Use TUI and GitHub API with iPhone"));
        CollectionAssert.AreEqual(new[] { "Fixes" }, Words("Co-Authored-By: Fixes"));
    }

    [TestMethod]
    public void TestInnerApostropheAndHyphenAreKept()
    {
        CollectionAssert.AreEqual(
            new[] { "Don't", "cherry-pick", "it’s", "well-known" },
            Words("Don't cherry-pick, it’s well-known")
        );
    }

    [TestMethod]
    public void TestBacktickQuotedCodeIsSkipped()
    {
        CollectionAssert.AreEqual(new[] { "Call", "then", "again" }, Words("Call `git fetch --all` then `Foo` again"));
        // An unmatched backtick is just punctuation
        CollectionAssert.AreEqual(new[] { "Use", "foo", "bar" }, Words("Use `foo bar"));
    }

    [TestMethod]
    public void TestSingleLettersAreSkipped()
    {
        CollectionAssert.AreEqual(new string[0], Words("a b I x"));
        CollectionAssert.AreEqual(new[] { "am" }, Words("I am"));
    }

    [TestMethod]
    public void TestSpansAreWhereTheWordsAre()
    {
        var spans = SpellScanner.Words("  Fix (resonable) issu.");

        CollectionAssert.AreEqual(
            new[] { new WordSpan(2, 3, "Fix"), new WordSpan(7, 9, "resonable"), new WordSpan(18, 4, "issu") },
            spans.ToArray()
        );
        Assert.AreEqual(16, spans[1].End);
        Assert.IsTrue(spans[1].Contains(7));
        Assert.IsTrue(spans[1].Contains(15));
        Assert.IsFalse(spans[1].Contains(16));
        Assert.IsFalse(spans[1].Contains(6));
    }

    [TestMethod]
    public void TestMisspelledKeepsOnlyWhatTheCheckRejects()
    {
        var spans = SpellScanner.Misspelled("Fix resonable issu", w => w.Length > 4);

        CollectionAssert.AreEqual(new[] { new WordSpan(4, 9, "resonable") }, spans.ToArray());
    }
}
