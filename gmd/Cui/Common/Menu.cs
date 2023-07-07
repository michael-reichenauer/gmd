using Terminal.Gui;

namespace gmd.Cui.Common;


class Menu2
{
    ContentView itemsView = null!;
    IReadOnlyList<MenuItem> items = null!;
    private readonly string title;
    private readonly int x;
    private readonly int y;

    public Menu2(int x, int y, string title = "")
    {
        this.x = x;
        this.y = y;
        this.title = title;
    }

    public static void Show(int x, int y, IEnumerable<MenuItem> items, string title = "")
    {
        new Menu2(x, y, title).Show(items);
    }

    void Show(IEnumerable<MenuItem> items)
    {
        this.items = items.ToList();
        var dlg = new UIDialog(title, 29, 7, null, options => { options.Y = 0; });
        itemsView = dlg.AddContentView(0, 0, Dim.Fill(), Dim.Fill(), OnGetContent);
        itemsView.RegisterKeyHandler(Key.Esc, () => dlg.Close());
        itemsView.IsShowCursor = false;
        itemsView.IsScrollMode = false;
        itemsView.IsCursorMargin = false;
        itemsView.TriggerUpdateContent(this.items.Count);
        dlg.Show();
    }

    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        return items.Skip(firstIndex).Take(count).Select((m, i) =>
        {

            var isSelectedRow = i + firstIndex == currentIndex;
            return (isSelectedRow
                ? Text.New.WhiteSelected($"{m.Title}")
                : Text.New.White($"{m.Title} "));
        });
    }
}

class Menu
{
    public static int MaxItemCount = 15;
    ContextMenu menu = new ContextMenu();

    public static void Show(int x, int y, IEnumerable<MenuItem> menuItems)
    {
        ContextMenu menu = new ContextMenu(x, y, new MenuBarItem(
            ToMaxItems(menuItems.Where(i => i != null)).Select(i => i.Item()).ToArray()));
        menu.Show();
    }

    public static IList<MenuItem> NewItems => new List<MenuItem>();

    internal static IEnumerable<MenuItem> ToMaxItems(IEnumerable<MenuItem> items)
    {
        if (items.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return items;
        }

        return items.Take(MaxItemCount)
            .Concat(new List<MenuItem>().AddSubMenu("  ...", "", ToMaxItems(items.Skip(MaxItemCount))));
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
            Children = Menu.ToMaxItems(children.Where(c => c != null)).Select(c => c.Item()).ToArray()
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

