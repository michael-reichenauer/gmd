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
        Assert.AreEqual("a-bb-ccc-bb", Names.Join('-'));
        Assert.AreEqual("A, BB, CCC, BB", Names.JoinBy(n => n.ToUpper(), ", "));
        Assert.AreEqual("A-BB-CCC-BB", Names.Join(n => n.ToUpper(), '-'));
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

    // The item is added when nothing matches the predicate, i.e. the predicate decides what
    // 'already exists' means, not the item itself
    [TestMethod]
    public void TestTryAddBy()
    {
        List<string> list = ["a"];

        list.TryAddBy(n => n == "a", "b");
        list.TryAddBy(n => n == "c", "c");

        CollectionAssert.AreEqual(new[] { "a", "c" }, list);
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

    [TestMethod]
    public void TestAdd()
    {
        CollectionAssert.AreEqual(new[] { "a", "bb", "ccc", "bb", "d", "e" }, Names.Add("d", "e").ToArray());
        CollectionAssert.AreEqual(Names, Names.Add().ToArray(), "The source is not changed");
    }

    // Distinct by a comparison of two items rather than by a key, so it works for types without a
    // usable Equals
    [TestMethod]
    public void TestDistinctBy()
    {
        CollectionAssert.AreEqual(new[] { "a", "bb", "ccc" }, Names.DistinctBy((x, y) => x == y).ToArray());

        string[] names = ["a", "bb", "cc", "d"];
        CollectionAssert.AreEqual(
            new[] { "a", "bb" },
            names.DistinctBy((x, y) => x.Length == y.Length).ToArray(),
            "Compared by length, so 'cc' and 'd' are duplicates"
        );
    }

    [TestMethod]
    public void TestDistinctByRequiresItsArguments()
    {
        IEnumerable<string>? nothing = null;
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            nothing!.DistinctBy((x, y) => x == y).ToList();
        });
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            Names.DistinctBy((Func<string, string, bool>)null!).ToList();
        });
    }
}
