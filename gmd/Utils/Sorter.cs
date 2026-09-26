namespace gmd.Utils;

// For some reason, the standard Sort does not work as expected, So this is a custom implementation.
static class Sorter
{
    public static void Sort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        CustomSort(list, comparer);
    }

    // Sorts to the very same order as Sort, without comparing every item with every item behind it,
    // for a comparer that puts an item after only a few others: 'mayGoAfter' gives, for an item, every
    // item it can compare greater than, i.e. comparer(item, x) > 0 only for an x among them (more are
    // harmless, fewer change the order). The items must be distinct. Sort compares each position
    // with all the rest, which is quadratic, and seconds for the thousands of branches of a view of
    // all branches, while the order of items the comparer does not order is Sort's, see SorterTest.
    public static void Sort<T>(IList<T> list, Func<T, T, int> comparer, Func<T, IEnumerable<T>> mayGoAfter)
        where T : class
    {
        Dictionary<T, int> indexOf = new(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < list.Count; i++)
        {
            indexOf[list[i]] = i;
        }

        for (var i = 0; i < list.Count; i++)
        {
            while (true)
            {
                // What a pass of CustomSort at i does: the item is compared with each item behind it,
                // and each it is greater than is swapped into place i in turn, in position order. A
                // swap moves only place i and a place already passed, so which of them the item is
                // greater than can be found before the swaps. The pass is repeated until none is.
                var item = list[i];
                var greaterThan = mayGoAfter(item)
                    .Select(x => indexOf.TryGetValue(x, out var j) ? j : -1)
                    .Where(j => j > i)
                    .Distinct()
                    .Where(j => comparer(item, list[j]) > 0)
                    .Order()
                    .ToList();
                if (greaterThan.Count == 0)
                {
                    break;
                }

                foreach (var j in greaterThan)
                {
                    (list[i], list[j]) = (list[j], list[i]);
                    indexOf[list[i]] = i;
                    indexOf[list[j]] = j;
                }
            }
        }
    }

    static void CustomSort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        for (int i = 0; i < list.Count; i++)
        {
            bool swapped = false;
            T item = list[i];

            for (int j = i + 1; j < list.Count; j++)
            {
                if (comparer(item, list[j]) > 0)
                {
                    T tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                    swapped = true;
                }
            }

            if (swapped)
            {
                i = i - 1;
            }
        }
    }
}
