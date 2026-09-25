using gmd.Cui.RepoView;

namespace gmdTest.Cui.RepoView;

// What n and Shift-N step through: the matches of the last search, from the one picked
[TestClass]
public class SearchMatchesTest
{
    [TestMethod]
    public void TestSteppingStartsFromThePickedMatch()
    {
        var matches = new SearchMatches();
        matches.Set("fix", ["a", "b", "c"], "b");

        Assert.AreEqual(2, matches.Number);
        Assert.AreEqual("c", matches.Step(1));
        Assert.AreEqual(3, matches.Number);
        Assert.AreEqual("b", matches.Step(-1));
        Assert.AreEqual("a", matches.Step(-1));
    }

    // Past the last one, in either direction, there is nothing, and the one shown stays shown
    [TestMethod]
    public void TestThereIsNothingPastTheEnds()
    {
        var matches = new SearchMatches();
        matches.Set("fix", ["a", "b"], "b");

        Assert.IsNull(matches.Step(1));
        Assert.AreEqual(2, matches.Number);
        Assert.AreEqual("a", matches.Step(-1));
        Assert.IsNull(matches.Step(-1));
        Assert.AreEqual(1, matches.Number);
    }

    [TestMethod]
    public void TestClearEndsTheSearch()
    {
        var matches = new SearchMatches();
        matches.Set("fix", ["a"], "a");

        matches.Clear();

        Assert.IsFalse(matches.IsActive);
        Assert.IsNull(matches.Step(1));
    }
}
