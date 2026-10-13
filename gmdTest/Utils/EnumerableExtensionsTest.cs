namespace gmdTest.Utils;

[TestClass]
public class EnumerableExtensionsTest
{
    static readonly string[] Names = ["a", "bb", "ccc", "bb"];

    [TestMethod]
    public void TestForEach()
    {
        var visited = new List<string>();
        Names.ForEach(visited.Add);
        CollectionAssert.AreEqual(Names, visited);

        var indexed = new List<string>();
        Names.ForEach((n, i) => indexed.Add($"{i}:{n}"));
        CollectionAssert.AreEqual(new[] { "0:a", "1:bb", "2:ccc", "3:bb" }, indexed);
    }

    [TestMethod]
    public void TestJoin()
    {
        Assert.AreEqual("a-bb-ccc-bb", Names.Join("-"));
        Assert.AreEqual("", Array.Empty<string>().Join("-"));
    }

    // Used where a list is built up from several passes and must not get duplicates, e.g. the
    // branches and ancestors collected for the graph
    [TestMethod]
    public void TestTryAdd()
    {
        List<string> list = ["a"];

        list.TryAdd("a");
        list.TryAdd("b");

        CollectionAssert.AreEqual(new[] { "a", "b" }, list);
    }

    [TestMethod]
    public void TestTryAddAll()
    {
        List<string> list = ["a"];

        list.TryAddAll(["a", "b", "c"]);
        list.TryAddAll([]);

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, list);
    }

    [TestMethod]
    public void TestContainsBy()
    {
        Assert.IsTrue(Names.ContainsBy(n => n.Length == 3));
        Assert.IsFalse(Names.ContainsBy(n => n.Length == 9));
        Assert.IsFalse(Array.Empty<string>().ContainsBy(n => true));
    }

    [TestMethod]
    public void TestFindIndexBy()
    {
        Assert.AreEqual(1, Names.FindIndexBy(n => n.Length == 2), "The first match");
        Assert.AreEqual(0, Names.FindIndexBy(n => n == "a"));
        Assert.AreEqual(-1, Names.FindIndexBy(n => n == "zz"), "No match");
    }

    [TestMethod]
    public void TestFindLastIndexBy()
    {
        Assert.AreEqual(3, Names.FindLastIndexBy(n => n.Length == 2), "The last match");
        Assert.AreEqual(0, Names.FindLastIndexBy(n => n == "a"));
        Assert.AreEqual(-1, Names.FindLastIndexBy(n => n == "zz"), "No match");
    }
}
