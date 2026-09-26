namespace gmdTest.Utils;

// Sorter exists because 'List.Sort does not work, why ????' (ViewRepoCreater.SortBranches). The
// answer is that the branch comparers are partial orders: CompareBranches returns 0 for two
// branches that are neither related nor ordered by the user, and GraphCreater compares only the
// column. List.Sort assumes a total order, so it can leave a pair that the comparer does order in
// the wrong order. These pin that Sorter handles what List.Sort does not.
[TestClass]
public class SorterTest
{
    [TestMethod]
    public void TestSortsAscending()
    {
        List<int> list = [5, 3, 9, 1, 3, 7];

        Sorter.Sort(list, (a, b) => a - b);

        CollectionAssert.AreEqual(new[] { 1, 3, 3, 5, 7, 9 }, list);
    }

    [TestMethod]
    public void TestSortsInPlace()
    {
        List<string> list = ["b", "a"];
        var same = list;

        Sorter.Sort(list, (a, b) => string.CompareOrdinal(a, b));

        Assert.AreSame(same, list);
        CollectionAssert.AreEqual(new[] { "a", "b" }, list);
    }

    [TestMethod]
    public void TestEmptyAndSingleItem()
    {
        List<int> empty = [];
        Sorter.Sort(empty, (a, b) => a - b);
        Assert.AreEqual(0, empty.Count);

        List<int> one = [7];
        Sorter.Sort(one, (a, b) => a - b);
        CollectionAssert.AreEqual(new[] { 7 }, one);
    }

    // The reason Sorter exists. The comparer only orders a branch against its ancestors and says
    // nothing (0) about anything else, which is what CompareBranches does. List.Sort leaves this
    // input untouched, i.e. 'feat' still before the 'main' and 'dev' it descends from.
    [TestMethod]
    public void TestSortsByAPartialOrderThatListSortGetsWrong()
    {
        List<string> byListSort = ["feat", "docs", "main", "bugfix", "dev"];
        byListSort.Sort((a, b) => ByAncestry(a, b));
        CollectionAssert.AreEqual(
            new[] { "feat", "docs", "main", "bugfix", "dev" },
            byListSort,
            "List.Sort was expected to leave the ancestors after their descendant"
        );

        List<string> bySorter = ["feat", "docs", "main", "bugfix", "dev"];
        Sorter.Sort(bySorter, ByAncestry);

        Assert.IsTrue(bySorter.IndexOf("main") < bySorter.IndexOf("dev"), $"{bySorter.Join(",")}");
        Assert.IsTrue(bySorter.IndexOf("dev") < bySorter.IndexOf("feat"), $"{bySorter.Join(",")}");
    }

    // Items the comparer calls equal are not kept in their original order, so the order of, say,
    // two unrelated branches is an artifact of the algorithm rather than of the input
    [TestMethod]
    public void TestIsNotStable()
    {
        List<(string Name, int Key)> list = [("a", 2), ("b", 1), ("c", 2), ("d", 1), ("e", 0)];

        Sorter.Sort(list, (x, y) => x.Key - y.Key);

        CollectionAssert.AreEqual(new[] { "e", "d", "b", "a", "c" }, list.Select(i => i.Name).ToArray());
    }

    // The sort that is told which items an item can go after must come to the very same order as the
    // one comparing every pair, also for the items the comparer leaves unordered, and whatever else
    // it is told. Over random orders of random items, each going after a random few of those before
    // it in a random order, so that the comparer never has a cycle.
    [TestMethod]
    public void TestSortingByWhatCanGoAfterIsTheSameAsComparingEveryPair()
    {
        var random = new Random(42);
        for (var run = 0; run < 2000; run++)
        {
            var items = Enumerable.Range(0, random.Next(0, 40)).Select(i => new Item($"i{i}")).ToList();
            var byRank = items.OrderBy(_ => random.Next()).ToList();
            var after = items.ToDictionary(
                i => i,
                i => byRank.Take(byRank.IndexOf(i)).Where(_ => random.Next(4) == 0).ToHashSet()
            );
            int Compare(Item a, Item b) =>
                after[a].Contains(b) ? 1
                : after[b].Contains(a) ? -1
                : 0;
            IEnumerable<Item> MayGoAfter(Item a) => after[a].Concat(items.Where(_ => random.Next(10) == 0));

            var input = items.OrderBy(_ => random.Next()).ToList();
            var expected = input.ToList();
            Sorter.Sort(expected, Compare);
            var actual = input.ToList();
            Sorter.Sort(actual, Compare, MayGoAfter);

            CollectionAssert.AreEqual(
                expected,
                actual,
                $"Run {run}: {string.Join(",", expected)} != {string.Join(",", actual)}"
            );
        }
    }

    class Item
    {
        public Item(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString() => Name;
    }

    static readonly Dictionary<string, string[]> Ancestors = new()
    {
        ["main"] = [],
        ["dev"] = ["main"],
        ["feat"] = ["dev", "main"],
        ["docs"] = [],
        ["bugfix"] = [],
    };

    // Ancestors come first, everything else is unordered
    static int ByAncestry(string a, string b)
    {
        if (a == b)
            return 0;
        if (Ancestors[b].Contains(a))
            return -1;
        if (Ancestors[a].Contains(b))
            return 1;
        return 0;
    }
}
