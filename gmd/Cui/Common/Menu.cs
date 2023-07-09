using Terminal.Gui;

namespace gmd.Cui.Common;


// Context menu
class Menu
{
    record Dimensions(int X, int Y, int Width, int Heigth, int TitleWidth, int ShortcutWidth, int SubMenuMarkerWidth);

    const int maxHeight = 30;
    readonly string title;
    readonly Menu? parent;
    readonly int xOrg;
    readonly int yOrg;
    readonly int altX;
    readonly Action onEscAction;

    UIDialog dlg = null!;
    ContentView itemsView = null!;
    IReadOnlyList<Text> itemRows = null!;
    IReadOnlyList<MenuItem> items = null!;
    Dimensions dim = null!;
    MenuItem CurrentItem => items[itemsView.CurrentIndex];


    public event Action? Closed;


    Menu RootMenu => parent == null ? this : parent.RootMenu;

    public static void Show(int x, int y, IEnumerable<MenuItem> items, string title = "", Action? onEscAction = null)
    {
        var menu = new Menu(x, y, title, null, -1, onEscAction);
        menu.Show(items);
    }

    public static IList<MenuItem> NewItems => new List<MenuItem>();

    public Menu(int x, int y, string title, Menu? parent, int altX, Action? onEscAction)
    {
        this.xOrg = x;
        this.yOrg = y;
        this.title = title;
        this.parent = parent;
        this.altX = altX;
        this.onEscAction = onEscAction ?? (() => { });
    }

    void Show(IEnumerable<MenuItem> items)
    {
        this.items = items.ToList();
        this.items.ForEach(i => i.IsDisabled = i.IsDisabled || !i.CanExecute());

        dim = GetDimensions();
        itemRows = ToItemsRows();

        dlg = new UIDialog(title, dim.Width, dim.Heigth, null, options =>
        {
            options.X = dim.X;
            options.Y = dim.Y;
        });

        itemsView = CreateItemsView();


        itemsView.TriggerUpdateContent(this.items.Count);
        if (this.items.Any() && this.items[0].IsDisabled) UI.Post(() => OnCursorDown());
        dlg.Show();
    }

    void CloseAll()
    {
        Close();
        UI.Post(() => parent?.CloseAll());
    }

    Dimensions GetDimensions()
    {
        var screeenWidth = Application.Driver.Cols;
        var screenHeight = Application.Driver.Rows;

        var viewHeight = Math.Min(items.Count + 2, Math.Min(maxHeight, screenHeight));

        var shortcutWidth = items.Any() ? items.Max(i => i.Shortcut.Length) : 0;
        shortcutWidth = shortcutWidth == 0 ? 0 : shortcutWidth + 1;  // Add space before shortcut if any
        var subMenuMarkerWidth = items.Any(i => i is SubMenu) ? 2 : 0;  // Include space befoe submenu marker if any
        var titleWidth = Math.Max(items.Any() ? items.Max(i => i.Title.Length + 1) : 0, title.Length + 4);

        var scrollbarWidth = items.Count + 2 > viewHeight ? 1 : 0;
        var viewWidth = Math.Max(20, titleWidth + shortcutWidth + subMenuMarkerWidth + scrollbarWidth + 1);
        if (viewWidth > screeenWidth)
        {   // Too wide, try to fit on screen
            viewWidth = screeenWidth;
            titleWidth = Math.Max(10, viewWidth - shortcutWidth - subMenuMarkerWidth - scrollbarWidth - 1);
        }

        var viewX = xOrg == -1 ? screeenWidth / 2 - viewWidth / 2 : xOrg; // Centered if x == -1
        var viewY = yOrg == -1 ? screenHeight / 2 - viewHeight / 2 : yOrg; // Centered if y == -1

        if (viewX + viewWidth > screeenWidth)
        {   // Too far to the right, try to move left
            if (altX >= 0)
            {   // Use alternative x position (left of parent menu)
                viewX = Math.Max(0, altX - viewWidth);
            }
            else
            {   // Adjust original x position
                viewX = Math.Max(0, viewX - viewWidth);
            }
        }
        if (viewY + viewHeight > screenHeight)
        {   // Too far down, try to move up
            viewY = Math.Max(0, screenHeight - viewHeight);
        }

        return new Dimensions(viewX, viewY, viewWidth, viewHeight, titleWidth, shortcutWidth, subMenuMarkerWidth);
    }

    IReadOnlyList<Text> ToItemsRows()
    {
        return items.Select(item =>
        {
            if (item is MenuSeparator ms) return Text.New.BrightMagenta(ToSepratorText(ms));

            var titleColor = item.IsDisabled ? TextColor.Dark : TextColor.White;

            var text = Text.New;
            if (item.Title.Length > dim.TitleWidth)
            {
                text.Color(titleColor, item.Title.Max(dim.TitleWidth - 1, true)).Dark("…");
            }
            else
            {
                text.Color(titleColor, item.Title.Max(dim.TitleWidth, true));
            }

            if (item.Shortcut != "")
                text.Black(new string(' ', dim.ShortcutWidth - item.Shortcut.Length)).Cyan(item.Shortcut);
            else if (dim.ShortcutWidth > 0)
                text.Black(new string(' ', dim.ShortcutWidth));

            if (!item.IsDisabled && item is SubMenu)
                text.BrightMagenta(">");
            if (item.IsDisabled && item is SubMenu)
                text.Dark(">");
            if (dim.SubMenuMarkerWidth > 0)
                text.Black(" ");

            return text;
        })
        .ToList();
    }


    string ToSepratorText(MenuSeparator item)
    {
        string title = item.Title;
        var width = dim.Width - 2;
        var scrollbarWidth = items.Count + 2 > dim.Heigth ? 0 : 1;
        if (title == "")
        {   // Just a line ----
            title = new string('─', dim.Width - 2 + scrollbarWidth);
        }
        else
        {   // A line with text, e.g. '-- text ------
            title = title.Max(width - 5);
            string suffix = new string('─', Math.Max(0, width - title.Length - 5 + scrollbarWidth));
            title = $"╴{title} {suffix}──";
        }

        return title;
    }

    ContentView CreateItemsView()
    {
        var view = dlg.AddContentView(0, 0, Dim.Fill(), Dim.Fill(), OnGetContent);
        view.RegisterKeyHandler(Key.Esc, () => Close());
        view.IsShowCursor = false;
        view.IsScrollMode = false;
        view.IsCursorMargin = false;

        view.RegisterKeyHandler(Key.Esc, () => OnEsc());
        view.RegisterKeyHandler(Key.Enter, () => OnEnter());
        view.RegisterKeyHandler(Key.CursorUp, () => OnCursorUp());
        view.RegisterKeyHandler(Key.CursorDown, () => OnCursorDown());
        view.RegisterKeyHandler(Key.PageUp, () => OnPageUp());
        view.RegisterKeyHandler(Key.PageDown, () => OnPageDown());
        view.RegisterKeyHandler(Key.Home, () => OnHome());
        view.RegisterKeyHandler(Key.End, () => OnEnd());
        view.RegisterKeyHandler(Key.CursorLeft, () => OnCursorLeft());
        view.RegisterKeyHandler(Key.CursorRight, () => OnCursorRight());
        return view;
    }

    void Close()
    {
        dlg.Close();
        Closed?.Invoke();
    }

    void OnEsc()
    {
        RootMenu.Closed += () => UI.Post(() => onEscAction());
        UI.Post(() => Close());
    }

    void OnEnter()
    {
        RootMenu.Closed += () =>
        {
            if (items.Any() && CurrentItem.CanExecute() && CurrentItem.Action != null)
            {
                UI.Post(() => CurrentItem.Action());
            }
        };

        UI.Post(() => CloseAll());
    }


    void OnCursorUp()
    {
        if (itemsView.CurrentIndex <= 0) return;
        itemsView.Move(-1);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled) OnCursorDown();
        if (CurrentItem.IsDisabled) OnCursorUp();
    }

    void OnCursorDown()
    {
        if (itemsView.CurrentIndex >= items.Count - 1) return;
        itemsView.Move(1);

        if (itemsView.CurrentIndex == items.Count - 1 && CurrentItem.IsDisabled) OnCursorUp();
        if (CurrentItem.IsDisabled) OnCursorDown();
    }

    void OnPageUp()
    {
        if (itemsView.CurrentIndex <= 0) return;
        itemsView.Move(-itemsView.ViewHeight);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled) OnCursorDown();
        if (CurrentItem.IsDisabled) OnCursorUp();
    }

    void OnPageDown()
    {
        if (itemsView.CurrentIndex >= items.Count - 1) return;
        itemsView.Move(itemsView.ViewHeight);

        if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled) OnCursorUp();
        if (CurrentItem.IsDisabled) OnCursorDown();
    }

    void OnHome()
    {
        if (itemsView.CurrentIndex <= 0) return;
        itemsView.Move(-itemsView.Count);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled) OnCursorDown();
        if (CurrentItem.IsDisabled) OnCursorUp();
    }

    void OnEnd()
    {
        if (itemsView.CurrentIndex >= items.Count - 1) return;
        itemsView.Move(itemsView.Count);

        if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled) OnCursorUp();
        if (CurrentItem.IsDisabled) OnCursorDown();
    }

    void OnCursorLeft()
    {
        if (parent == null) return; // Do not close top level menu on left arrow (only sub menus)
        Close();
    }

    void OnCursorRight()
    {
        if (items.Any() && CurrentItem is SubMenu sm && !sm.IsDisabled)
        {
            var x = dim.X + dim.Width;
            var y = dim.Y + (itemsView.CurrentIndex - itemsView.FirstIndex);

            var subMenu = new Menu(x, y, sm.Title, this, dim.X, null);
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
}


// A normal menu item and base class for SubMenu and MenuSeparator
class MenuItem
{
    public MenuItem(string title, string shortcut, Action action, Func<bool>? canExecute = null)
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


// To create a sub menu
class SubMenu : MenuItem
{
    public SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
        : base(title, shortcut, () => { }, () => canExecute?.Invoke() ?? true && children.Any())
    {
        Children = children;
    }

    public IEnumerable<MenuItem> Children { get; }
}


// To create a menu separator line or header line
class MenuSeparator : MenuItem
{
    public MenuSeparator(string text = "")
        : base(text, "", () => { }, () => false)
    { }
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

