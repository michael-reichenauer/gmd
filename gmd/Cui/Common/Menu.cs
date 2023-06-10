using Terminal.Gui;

namespace gmd.Cui.Common;

class Menu
{
    ContextMenu menu = new ContextMenu();

    public static void Show(int x, int y, IEnumerable<MenuItem> menuItems)
    {
        ContextMenu menu = new ContextMenu(x, y, new MenuBarItem(
            menuItems.Select(i => i.Item()).ToArray()));
        menu.Show();
    }

    public static SubMenu SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null) =>
        new SubMenu(title, shortcut, children, canExecute);

    public static MenuItem Item(string title, string shortcut, Action action, Func<bool>? canExecute = null) =>
        new MenuItem(title, shortcut, action, canExecute);

    public static MenuItem Separator(string text = "")
    {
        const int maxDivider = 25;
        if (text == "")
        {   // Just a line ----
            return new MenuItem(new string('─', maxDivider), "", () => { }, () => false);
        }

        // A line with text, e.g. '-- text ------'
        text = text.Max(maxDivider - 6);
        string suffix = new string('─', Math.Max(0, maxDivider - text.Length - 6));
        return new MenuItem($"── {text} {suffix}──", "", () => { }, () => false);
    }
}


// One item in a menu
class MenuItem
{
    Terminal.Gui.MenuItem item;

    public MenuItem()
    {
        item = new Terminal.Gui.MenuItem();
    }

    public MenuItem(string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        shortcut = shortcut == "" ? "" : shortcut + " ";
        item = new Terminal.Gui.MenuItem(title, shortcut, action, canExecute);
    }

    public string Title => item.Title.ToString() ?? "";

    public virtual Terminal.Gui.MenuItem Item() => item;
}


// A menu item that opens a submenu
class SubMenu : MenuItem
{
    MenuBarItem menuBar;

    public SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
    {
        shortcut = shortcut == "" ? "" : shortcut + " ";
        menuBar = new MenuBarItem(title, shortcut, null, canExecute)
        {
            Children = children.Select(c => c.Item()).ToArray()
        };
    }

    public override Terminal.Gui.MenuItem Item() => menuBar;
}


// Extension methods to make it easier to build menus
static class MenuExtensions
{
    public static ICollection<MenuItem> AddSubMenu(this ICollection<MenuItem> items, string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
    {
        items.Add(Menu.SubMenu(title, shortcut, children, canExecute));
        return items;
    }

    public static ICollection<MenuItem> AddItem(this ICollection<MenuItem> items, string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        items.Add(Menu.Item(title, shortcut, action, canExecute));
        return items;
    }

    public static ICollection<MenuItem> AddSeparator(this ICollection<MenuItem> items, string text = "")
    {
        items.Add(Menu.Separator(text));
        return items;
    }

    public static ICollection<MenuItem> Add(this ICollection<MenuItem> items, params MenuItem[] moreItems)
    {
        moreItems.ForEach(i => items.Add(i));
        return items;
    }

    public static ICollection<MenuItem> Add(this ICollection<MenuItem> items, IEnumerable<MenuItem> moreItems)
    {
        moreItems.ForEach(i => items.Add(i));
        return items;
    }

    public static void Show(this IEnumerable<MenuItem> items, int x, int y)
    {
        Menu.Show(x, y, items);
    }
}

