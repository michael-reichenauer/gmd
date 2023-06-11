using Terminal.Gui;

namespace gmd.Cui.Common;

class Menu
{
    ContextMenu menu = new ContextMenu();

    public static void Show(int x, int y, IEnumerable<MenuItem> menuItems)
    {
        ContextMenu menu = new ContextMenu(x, y, new MenuBarItem(
            menuItems.Where(i => i != null).Select(i => i.Item()).ToArray()));
        menu.Show();
    }

    public static IList<MenuItem> NewItems => new List<MenuItem>();
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

class MenuSeparator : MenuItem
{
    const int maxDivider = 25;

    public MenuSeparator(string text = "")
    : base(ToTitle(text), "", () => { }, () => false)
    { }

    static string ToTitle(string text = "")
    {
        string title = "";
        if (text == "")
        {   // Just a line ----
            title = new string('─', maxDivider);
        }
        else
        {   // A line with text, e.g. '-- text ------'
            text = text.Max(maxDivider - 6);
            string suffix = new string('─', Math.Max(0, maxDivider - text.Length - 6));
            title = $"── {text} {suffix}──";
        }

        return title;
    }
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
            Children = children.Where(c => c != null).Select(c => c.Item()).ToArray()
        };
    }

    public override Terminal.Gui.MenuItem Item() => menuBar;
}


// Extension methods to make it easier to build menus
static class MenuExtensions
{
    public static ICollection<MenuItem> AddSubMenu(this ICollection<MenuItem> items, string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
    {
        items.Add(new SubMenu(title, shortcut, children, canExecute));
        return items;
    }

    public static ICollection<MenuItem> AddItem(this ICollection<MenuItem> items, string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        items.Add(new MenuItem(title, shortcut, action, canExecute));
        return items;
    }

    public static ICollection<MenuItem> AddSeparator(this ICollection<MenuItem> items, string text = "")
    {
        items.Add(new MenuSeparator(text));
        return items;
    }

    public static ICollection<MenuItem> Add(this ICollection<MenuItem> items, params MenuItem[] moreItems)
    {
        moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }

    public static ICollection<MenuItem> Add(this ICollection<MenuItem> items, IEnumerable<MenuItem> moreItems)
    {
        moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }
}

