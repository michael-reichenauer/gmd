using System.Collections.Generic;


namespace System.Linq;

public static class EnumerableExtensions
{
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
    {
        foreach (T item in enumeration)
        {
            action(item);
        }
    }

    public static string AsString<T1, T2>(this IDictionary<T1, T2> dictionary) =>
        dictionary == null ? "{}" :
            "{" + string.Join(",", dictionary.Select(p => $"{p.Key}={p.Value}")) + "}";

    public static string AsString<T1>(this IEnumerable<T1> source) =>
        source == null ? "{}" :
         "{" + string.Join(",", source.Select(p => $"{p}")) + "}";


    public static IReadOnlyList<TSource> AsReadOnlyList<TSource>(this IReadOnlyList<TSource> source)
    {
        return source;
    }

    public static string Join(this IEnumerable<string> source, string separator)
    {
        return string.Join(separator, source);
    }


    public static void TryAdd<TSource>(this List<TSource> source, TSource item)
    {
        if (source.Contains(item))
        {
            return;
        }
        source.Add(item);
    }

    public static void TryAddAll<TSource>(this List<TSource> source, IEnumerable<TSource> items)
    {
        foreach (var item in items)
        {
            if (source.Contains(item))
            {
                continue;
            }
            source.Add(item);
        }
    }

    public static void TryAddBy<TSource>(this List<TSource> source, Func<TSource, bool> predicate, TSource item)
    {
        if (null != source.FirstOrDefault(predicate))
        {
            return;
        }
        source.Add(item);
    }

    public static bool ContainsBy<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        return null != source.FirstOrDefault(predicate);
    }

    public static int FindIndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        var index = 0;
        foreach (var item in source)
        {
            if (predicate(item))
            {
                return index;
            }
            index++;
        }
        return -1;
    }


    public static IReadOnlyList<TSource> ToReadOnlyList<TSource>(this IEnumerable<TSource> enumeration)
    {
        return enumeration.ToList();
    }

    /// <summary>
    ///  Returns elements from a sequence by concating the params parameters
    /// </summary>
    public static IEnumerable<TSource> Add<TSource>(this IEnumerable<TSource> source,
       params TSource[] items) => source.Concat(items);


    /// <summary>
    ///  Returns distinct elements from a sequence by using a specified 
    ///  predicate to compare values of two elements.
    /// </summary>
    public static IEnumerable<TSource> Distinct<TSource>(this IEnumerable<TSource> source,
        Func<TSource, TSource, bool> comparer)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (comparer == null)
        {
            throw new ArgumentNullException(nameof(comparer));
        }

        // Use the MSDN provided Distinct function with a private custom IEqualityComparer comparer.
        return source.Distinct(new DistinctComparer<TSource>(comparer));
    }

    // public static void Sort<T>(this IList<T> list, IComparer<T> comparer)
    // {
    //     CustomSort(list, comparer);
    // }

    // private static void CustomSort<T>(IList<T> list, IComparer<T> comparer)
    // {
    //     for (int i = 0; i < list.Count; i++)
    //     {
    //         bool swapped = false;
    //         T item = list[i];

    //         for (int j = i + 1; j < list.Count; j++)
    //         {
    //             if (comparer.Compare(item, list[j]) > 0)
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


    private class DistinctComparer<TSource> : IEqualityComparer<TSource>
    {
        private readonly Func<TSource, TSource, bool> comparer;

        public DistinctComparer(Func<TSource, TSource, bool> comparer)
        {
            this.comparer = comparer;
        }

#pragma warning disable CS8767
        public bool Equals(TSource x, TSource y) => comparer(x, y);
#pragma warning restore CS8767

        // Always returns 0 to force the Distinct comparer function to call the Equals() function
        // to do the comparison
        public int GetHashCode(TSource obj) => 0;
    }
}


public class EnumerableEx
{
    /// <summary>
    ///  Returns elements from a sequence of the params parameters
    /// </summary>
    public static IEnumerable<T> From<T>(params T[] items) => items;
}