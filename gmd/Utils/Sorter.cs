namespace gmd.Utils;

// For some reason, the standard Sort does not work as expected, So this is a custom implementation.
static class Sorter
{
    public static void Sort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        CustomSort(list, comparer);
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

    // private static void CustomSortOld<T>(IList<T> list, Func<T, T, int> comparer)
    // {
    //     for (int i = 0; i < list.Count; i++)
    //     {
    //         bool swapped = false;
    //         T item = list[i];

    //         for (int j = i + 1; j < list.Count; j++)
    //         {
    //             if (comparer(item, list[j]) > 0)
    //             {
    //                 T tmp = list[j];
    //                 list.RemoveAt(j);
    //                 list.Insert(i, tmp);
    //                 swapped = true;
    //             }
    //         }

    //         if (swapped)
    //         {
    //             i = i - 1;
    //         }
    //     }
    // }
}