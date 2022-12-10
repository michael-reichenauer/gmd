namespace gmd.Utils;

internal static class Sorter
{
    public static void Sort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        CustomSort(list, comparer);
    }

    private static void CustomSort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        for (int i = 0; i < list.Count; i++)
        {
            bool swapped = false;
            T item = list[i];

            for (int j = i + 1; j < list.Count; j++)
            {
                if (comparer(item, list[j]) > 0)
                {
                    T tmp = list[j];
                    list.RemoveAt(j);
                    list.Insert(i, tmp);
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