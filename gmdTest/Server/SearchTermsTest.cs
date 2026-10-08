using gmd.Server;

namespace gmdTest.Server;

// What was typed into the search, split into terms: words, "quoted phrases", and the 'file:' paths
// that git is asked about
[TestClass]
public class SearchTermsTest
{
    [TestMethod]
    public void TestWordsAndQuotedPhrases()
    {
        var terms = SearchTerms.Parse("fix \"login page\" 2024");

        CollectionAssert.AreEqual(new[] { "fix", "login page", "2024" }, terms.Words.ToArray());
        Assert.AreEqual(0, terms.Files.Count);
    }

    [TestMethod]
    public void TestAFileTermIsAPath()
    {
        var terms = SearchTerms.Parse("fix file:Program.cs File:\"my dir/a b.txt\"");

        CollectionAssert.AreEqual(new[] { "fix" }, terms.Words.ToArray());
        CollectionAssert.AreEqual(new[] { "Program.cs", "my dir/a b.txt" }, terms.Files.ToArray());
    }

    // A 'change:' term is a text the changes of a commit added or removed, which git is asked about
    [TestMethod]
    public void TestAChangeTermIsAText()
    {
        var terms = SearchTerms.Parse("fix change:RetryCount Change:\"retry count\" file:a.cs");

        CollectionAssert.AreEqual(new[] { "fix" }, terms.Words.ToArray());
        CollectionAssert.AreEqual(new[] { "RetryCount", "retry count" }, terms.Changes.ToArray());
        CollectionAssert.AreEqual(new[] { "a.cs" }, terms.Files.ToArray());
        Assert.IsTrue(terms.IsAskingGit);
    }

    // As it is while the path is still being typed, which must not search every file
    [TestMethod]
    public void TestFileAloneIsNoTermYet()
    {
        var terms = SearchTerms.Parse("file: change:");

        Assert.AreEqual(0, terms.Words.Count);
        Assert.AreEqual(0, terms.Files.Count);
        Assert.AreEqual(0, terms.Changes.Count);
        Assert.IsFalse(terms.IsAskingGit);
    }
}
