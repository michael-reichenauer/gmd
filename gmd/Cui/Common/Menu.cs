using Terminal.Gui;

namespace gmd.Cui.Common;


class Menu2
{
    const int maxHeight = 31;
    readonly string title;
    readonly Menu2? parent;
    readonly int x;
    readonly int y;

    UIDialog dlg = null!;
    ContentView itemsView = null!;
    IReadOnlyList<Text> itemRows = null!;
    IReadOnlyList<MenuItem2> items = null!;
    int viewWidth = 0;
    int titleWidth;
    int viewHeight;
    int shortcutWidth;
    int subMenuMarkerWidth;

    MenuItem2 CurrentItem => items[itemsView.CurrentIndex];

    public Menu2(int x, int y, string title = "", Menu2? parent = null)
    {
        this.x = x;
        this.y = y;
        this.title = title;
        this.parent = parent;
    }


    public static void Show(int x, int y, IEnumerable<MenuItem> items, string title = "")
    {
        if (!items.Any()) return;

        new Menu2(x, y, title).Show(items.Select(ToItem));
    }


    void Show(IEnumerable<MenuItem2> items)
    {
        // Get unicode right arror char as a string
        var rightArrow = new string(new[] { '\u25B6' });




        this.items = items.ToList();
        this.items.ForEach(i => i.IsDisabled = i.IsDisabled || !i.CanExecute());

        CalculateSizes();
        itemRows = ToItemsRows();

        dlg = new UIDialog(title, viewWidth, viewHeight, null, options =>
        {
            options.X = x;
            options.Y = y;
        });

        itemsView = CreateItemsView();


        itemsView.TriggerUpdateContent(this.items.Count);
        if (this.items[0].IsDisabled) UI.Post(() => OnCursorDown());
        dlg.Show();
    }

    void CalculateSizes()
    {
        viewHeight = Math.Min(items.Count + 2, maxHeight);
        shortcutWidth = items.Max(i => i.Shortcut.Length);
        shortcutWidth = shortcutWidth == 0 ? 0 : shortcutWidth + 1;  // Add space before shortcut if any
        subMenuMarkerWidth = items.Any(i => i is SubMenu2) ? 2 : 0;  // Include space befoe submenu marker if any
        titleWidth = items.Max(i => i.Title.Length);

        var scrollbarWidth = items.Count + 2 > viewHeight ? 1 : 0;
        viewWidth = titleWidth + shortcutWidth + subMenuMarkerWidth + scrollbarWidth + 1;
    }

    IReadOnlyList<Text> ToItemsRows()
    {
        return items.Select(ToItemText).ToList();
    }


    Text ToItemText(MenuItem2 item)
    {
        if (item is MenuSeparator2 ms) return Text.New.BrightMagenta(ToSepratorText(ms));

        var titleColor = item.IsDisabled ? TextColor.Dark : TextColor.White;

        var text = Text.New.Color(titleColor, item.Title.Max(titleWidth, true));
        if (item.Shortcut != "")
            text.Black(new string(' ', shortcutWidth - item.Shortcut.Length)).Cyan(item.Shortcut);
        else if (shortcutWidth > 0)
            text.Black(new string(' ', shortcutWidth));

        if (item is SubMenu2)
            text.Black("").BrightMagenta(">");
        if (subMenuMarkerWidth > 0)
            text.Black(" ");

        return text;
    }

    string ToSepratorText(MenuSeparator2 item)
    {
        string title = item.Title;
        var width = viewWidth - 2;
        if (title == "")
        {   // Just a line ----
            title = new string('─', viewWidth - 2);
        }
        else
        {   // A line with text, e.g. '-- text ------
            title = title.Max(width - 5);
            string suffix = new string('─', Math.Max(0, width - title.Length - 5));
            title = $"╴{title} {suffix}──";
        }

        return title;
    }

    ContentView CreateItemsView()
    {
        var view = dlg.AddContentView(0, 0, Dim.Fill(), Dim.Fill(), OnGetContent);
        view.RegisterKeyHandler(Key.Esc, () => dlg.Close());
        view.IsShowCursor = false;
        view.IsScrollMode = false;
        view.IsCursorMargin = false;

        view.RegisterKeyHandler(Key.Enter, () => OnEnter());
        view.RegisterKeyHandler(Key.CursorUp, () => OnCursorUp());
        view.RegisterKeyHandler(Key.CursorDown, () => OnCursorDown());
        view.RegisterKeyHandler(Key.CursorLeft, () => OnCursorLeft());
        view.RegisterKeyHandler(Key.CursorRight, () => OnCursorRight());
        return view;
    }

    void OnEnter()
    {
        if (CurrentItem.CanExecute() && CurrentItem.Action != null)
        {
            CurrentItem.Action();
        }

        dlg.Close();
    }


    void OnCursorUp()
    {
        if (itemsView.CurrentIndex == 0) return;
        itemsView.Move(-1);
        if (CurrentItem.IsDisabled) OnCursorUp();
    }

    void OnCursorDown()
    {
        if (itemsView.CurrentIndex == items.Count - 1) return;
        itemsView.Move(1);
        if (CurrentItem.IsDisabled) OnCursorDown();
    }

    void OnCursorLeft()
    {
        if (parent == null) return; // Do not close top level menu on left arrow (only sub menus)
        dlg.Close();
    }

    void OnCursorRight()
    {

        if (CurrentItem is SubMenu2 sm && !sm.IsDisabled)
        {
            var x = this.x + viewWidth;
            var y = this.y + (itemsView.CurrentIndex - itemsView.FirstIndex);

            var subMenu = new Menu2(x, y, sm.Title, this);
            subMenu.Show(sm.Children);
        }
    }


    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        return itemRows.Skip(firstIndex).Take(count).Select((row, i) =>
        {
            var isSelectedRow = i + firstIndex == currentIndex;
            return isSelectedRow ? Text.New.WhiteSelected(row.ToString()) : row;
        });
    }

    static MenuItem2 ToItem(MenuItem item)
    {
        if (item is SubMenu sm)
        {
            return new SubMenu2(sm.Title, sm.Item().Help.ToString() ?? "", sm.Children.Select(ToItem), sm.Item().CanExecute);
        }
        else if (item is MenuSeparator ms)
        {
            return new MenuSeparator2(ms.Title);
        }

        return new MenuItem2(item.Title, item.Item().Help.ToString() ?? "", item.Item().Action, item.Item().CanExecute);
    }
}

class MenuItem2
{
    public MenuItem2(string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        Title = title;
        Shortcut = shortcut;
        Action = action;
        CanExecute = canExecute ?? (() => true);
    }

    public string Title { get; }
    public string Shortcut { get; }
    public Action Action { get; }
    public Func<bool> CanExecute { get; }
    public bool IsDisabled { get; set; }
}


class SubMenu2 : MenuItem2
{
    public SubMenu2(string title, string shortcut, IEnumerable<MenuItem2> children, Func<bool>? canExecute = null)
        : base(title, shortcut, () => { }, () => canExecute?.Invoke() ?? true && children.Any())
    {
        Children = children;
    }

    public IEnumerable<MenuItem2> Children { get; }
}


class MenuSeparator2 : MenuItem2
{
    public MenuSeparator2(string text = "")
        : base(text, "", () => { }, () => false)
    { }
}


// #################### OLD ####################

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

    public virtual string Title => item.Title.ToString() ?? "";

    public virtual Terminal.Gui.MenuItem Item() => item;
}

class MenuSeparator : MenuItem
{
    const int maxDivider = 25;

    public MenuSeparator(string text = "")
    : base(text, "", () => { }, () => false)
    { }

    // static string ToTitle(string text = "")
    // {
    //     string title = "";
    //     if (text == "")
    //     {   // Just a line ----
    //         title = new string('─', maxDivider);
    //     }
    //     else
    //     {   // A line with text, e.g. '-- text ------'
    //         text = text.Max(maxDivider - 6);
    //         string suffix = new string('─', Math.Max(0, maxDivider - text.Length - 6));
    //         title = $"── {text} {suffix}──";
    //     }

    //     return title;
    // }
}


// A menu item that opens a submenu
class SubMenu : MenuItem
{
    MenuBarItem menuBar;
    public IEnumerable<MenuItem> Children;
    string title;

    public SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
    {
        this.title = title;
        this.Children = children;
        shortcut = shortcut == "" ? "" : shortcut + " ";
        menuBar = new MenuBarItem(title, shortcut, null, canExecute)
        {
            Children = Menu.ToMaxItems(children.Where(c => c != null)).Select(c => c.Item()).ToArray()
        };
    }

    public override string Title => title;
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

