namespace gmd.Utils;

// Some useful IEnumerable extensions that are missing in .NET
public static class EnumerableExtensions
{
    // Calls action for each item in the enumeration
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
    {
        foreach (T item in enumeration)
        {
            action(item);
        }
    }

    // Calls action for each item in the enumeration (includes index)
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T, int> action)
    {
        int i = 0;
        foreach (T item in enumeration)
        {
            action(item, i++);
        }
    }

    // Returns a concatenated string of all items in the string enumeration
    // Same result as calling string.Join(separator, source)
    public static string Join(this IEnumerable<string> source, string separator)
    {
        return string.Join(separator, source);
    }

    // Tries to add the item to the list if it does not already exist
    public static void TryAdd<TSource>(this List<TSource> source, TSource item)
    {
        if (source.Contains(item))
        {
            return;
        }
        source.Add(item);
    }

    // Tries to add all the items to the list if they do not already exist. What the list has is looked
    // up in a set, not in the list, which made adding many items to a long list quadratic: a commit's
    // candidate branches, hundreds long in an ambiguous stretch of rust's history.
    public static void TryAddAll<TSource>(this List<TSource> source, IEnumerable<TSource> items)
    {
        var existing = new HashSet<TSource>(source);
        foreach (var item in items)
        {
            if (existing.Add(item))
            {
                source.Add(item);
            }
        }
    }

    // Returns the index of the first item that matches the predicate
    public static int FindIndexBy<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
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

    // Returns the index of the last item that matches the predicate
    public static int FindLastIndexBy<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        var last = -1;
        var index = 0;
        foreach (var item in source)
        {
            if (predicate(item))
            {
                last = index;
            }
            index++;
        }
        return last;
    }
}
