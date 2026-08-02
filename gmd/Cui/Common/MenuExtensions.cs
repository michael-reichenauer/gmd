namespace gmd.Cui.Common;

// Extension methods to make it easier to build menus
static class MenuExtensions
{
    public static ICollection<MenuItem> SubMenu(
        this ICollection<MenuItem> items,
        string text,
        string shortcut,
        IEnumerable<MenuItem> children,
        Func<bool>? canExecute = null
    )
    {
        items.Add(new SubMenu(text, shortcut, children, canExecute));
        return items;
    }

    public static ICollection<MenuItem> SubMenu(
        this ICollection<MenuItem> items,
        bool condition,
        string title,
        string shortcut,
        IEnumerable<MenuItem> children,
        Func<bool>? canExecute = null
    )
    {
        if (condition)
            items.Add(new SubMenu(title, shortcut, children, canExecute));
        return items;
    }

    public static ICollection<MenuItem> Item(
        this ICollection<MenuItem> items,
        string text,
        string shortcut,
        Action action,
        Func<bool>? canExecute = null
    )
    {
        items.Add(new MenuItem(text, shortcut, action, canExecute));
        return items;
    }

    public static ICollection<MenuItem> Item(
        this ICollection<MenuItem> items,
        bool condition,
        string title,
        string shortcut,
        Action action,
        Func<bool>? canExecute = null
    )
    {
        if (condition)
            items.Add(new MenuItem(title, shortcut, action, canExecute));
        return items;
    }

    public static ICollection<MenuItem> Separator(this ICollection<MenuItem> items, string text = "")
    {
        items.Add(new MenuSeparator(text));
        return items;
    }

    public static ICollection<MenuItem> Separator(this ICollection<MenuItem> items, bool condition, string text = "")
    {
        if (condition)
            items.Add(new MenuSeparator(text));
        return items;
    }

    public static ICollection<MenuItem> Item(this ICollection<MenuItem> items, MenuItem item)
    {
        items.Add(item);
        return items;
    }

    public static ICollection<MenuItem> Items(this ICollection<MenuItem> items, params MenuItem[] moreItems)
    {
        moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }

    public static ICollection<MenuItem> Items(
        this ICollection<MenuItem> items,
        bool condition,
        params MenuItem[] moreItems
    )
    {
        if (condition)
            moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }

    public static ICollection<MenuItem> Items(this ICollection<MenuItem> items, IEnumerable<MenuItem> moreItems)
    {
        moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }

    public static ICollection<MenuItem> Items(
        this ICollection<MenuItem> items,
        bool condition,
        IEnumerable<MenuItem> moreItems
    )
    {
        if (condition)
            moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }
}
