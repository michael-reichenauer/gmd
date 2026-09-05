using gmd.Common;
using gmd.Common.Spelling;

namespace gmdTest.Common.Spelling;

// The real checker over the embedded dictionary. Loading it takes a moment, so the read-only
// tests share one instance; the tests that add words or change the config take their own.
[TestClass]
public class SpellCheckerTest
{
    static readonly Lazy<SpellChecker> shared = new(() => new SpellChecker(new Config()));

    static SpellChecker Shared => shared.Value;

    [TestMethod]
    public void TestEmbeddedDictionaryLoads()
    {
        Assert.IsTrue(Shared.IsEnabled);
    }

    [TestMethod]
    public void TestKnownAndMisspelledWords()
    {
        Assert.IsFalse(Shared.IsMisspelled("reasonable"));
        Assert.IsFalse(Shared.IsMisspelled("Reasonable")); // Capitalized at the start of a sentence
        Assert.IsFalse(Shared.IsMisspelled("branches"));
        Assert.IsFalse(Shared.IsMisspelled("don't"));
        Assert.IsFalse(Shared.IsMisspelled("cherry-pick"));

        Assert.IsTrue(Shared.IsMisspelled("resonable"));
        Assert.IsTrue(Shared.IsMisspelled("issu"));
        Assert.IsTrue(Shared.IsMisspelled("Sumerize"));
        Assert.IsTrue(Shared.IsMisspelled("brnach"));
    }

    [TestMethod]
    public void TestSuggestions()
    {
        var suggestions = Shared.Suggest("resonable");
        Assert.IsTrue(suggestions.Count > 0 && suggestions.Count <= 6, $"{suggestions.Count}");
        CollectionAssert.Contains(suggestions.ToList(), "reasonable");

        Assert.AreEqual("issue", Shared.Suggest("issu")[0]);
        Assert.AreEqual("branch", Shared.Suggest("brnach")[0]);
        CollectionAssert.Contains(Shared.Suggest("Sumerize").ToList(), "Summarize"); // Keeps the capital
    }

    [TestMethod]
    public void TestAddToDictionary()
    {
        var checker = new SpellChecker(new Config());
        Assert.IsTrue(checker.IsMisspelled("gmd"));

        checker.AddToDictionary("gmd");

        Assert.IsFalse(checker.IsMisspelled("gmd"));
        Assert.IsFalse(checker.IsMisspelled("Gmd"));
    }

    [TestMethod]
    public void TestUserWordsFromConfigAreKnown()
    {
        var checker = new SpellChecker(new Config { SpellWords = ["csproj", "hoovered"] });

        Assert.IsFalse(checker.IsMisspelled("csproj"));
        Assert.IsFalse(checker.IsMisspelled("hoovered"));
        Assert.IsTrue(checker.IsMisspelled("resonable"));
    }

    [TestMethod]
    public void TestDisabledByConfig()
    {
        var checker = new SpellChecker(new Config { SpellCheck = false });

        Assert.IsFalse(checker.IsEnabled);
        Assert.IsFalse(checker.IsMisspelled("resonable"));
        Assert.AreEqual(0, checker.Suggest("resonable").Count);
    }

    [TestMethod]
    public void TestMissingDictionaryFileDisablesRatherThanFails()
    {
        var checker = new SpellChecker(new Config { SpellDictionary = "/no/such/folder/en_US.dic" });

        Assert.IsFalse(checker.IsEnabled);
        Assert.IsFalse(checker.IsMisspelled("resonable"));
        Assert.AreEqual(0, checker.Suggest("resonable").Count);
    }
}
