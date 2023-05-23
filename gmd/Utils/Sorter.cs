namespace gmd.Utils;

internal static class Sorter
{
    public static void Sort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        CustomSort2(list, comparer);
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

    private static void CustomSort2<T>(IList<T> list, Func<T, T, int> comparer)
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

    // A simple sorting algorithm that repeatedly steps through the list, compares adjacent elements and swaps them if they are in the wrong order.
    private static void BubbleSort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = 0; j < list.Count - i - 1; j++)
            {
                if (comparer(list[j], list[j + 1]) > 0)
                {
                    T tmp = list[j];
                    list[j] = list[j + 1];
                    list[j + 1] = tmp;
                }
            }
        }
    }
}