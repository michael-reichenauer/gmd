using Terminal.Gui;

namespace gmd.Cui.Common;


// Context menu
class Menu
{
    record Dimensions(int X, int Y, int Width, int Height, int TitleWidth, int ShortcutWidth, int SubMenuMarkerWidth);

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
    bool isAllDisabled = false;
    bool isFocus = false;
    bool hasMovedMouse = false;

    public event Action? Closed;
    public const int Center = -int.MaxValue;


    Menu RootMenu => parent == null ? this : parent.RootMenu;

    public static void Show(string title, int x, int y, IEnumerable<MenuItem> items, Action? onEscAction = null)
    {
        var menu = new Menu(x, y, title, null, -1, onEscAction);
        menu.Show(items);
    }


    public static ICollection<MenuItem> Items => new List<MenuItem>();
    public static MenuItem Item(string title, string shortcut, Action action, Func<bool>? canExecute = null) =>
        new MenuItem(title, shortcut, action, canExecute);
    public static MenuItem Separator(string text = "") => new MenuSeparator(text);
    public static MenuItem SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null) =>
        new SubMenu(title, shortcut, children, canExecute);


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
        this.items = items
        .Select(i => i with
        {
            IsDisabled = i.IsDisabled || !(i.CanExecute?.Invoke() ?? true) || i is SubMenu sm && !sm.Children.Any()
        })
        .ToList();


        this.isAllDisabled = this.items.All(i => i.IsDisabled);

        dim = GetDimensions();
        itemRows = ToMenuItemsRows();

        dlg = new UIDialog(title, dim.Width, dim.Height, null, options =>
        {
            options.X = dim.X;
            options.Y = dim.Y;
        });

        itemsView = CreateItemsView();

        itemsView.TriggerUpdateContent(this.items.Count);
        if (this.items.Any() && this.items[0].IsDisabled && !isAllDisabled) UI.Post(() => OnCursorDown());

        isFocus = true;
        Application.RootMouseEvent += OnRootMouseEvent;  // To handle mouse clicks both within and also outside this menu to close it
        dlg.Show();
        Application.RootMouseEvent -= OnRootMouseEvent;
        isFocus = false;
        Closed?.Invoke();
    }


    // Called for all mouse events, both within and also outside this menu, 
    // Skipping if not focused or not clicked events
    void OnRootMouseEvent(MouseEvent e)
    {
        e.Handled = false;

        if (!isFocus) return;

        if (e.Flags.HasFlag(MouseFlags.Button1Clicked)) OnMouseClicked(e.X, e.Y);
        if (e.Flags == MouseFlags.ReportMousePosition) OnMouseMove(e.X, e.Y);
    }


    void OnMouseMove(int screenX, int screenY)
    {
        // Map screen coordinates to this menu view coordinates
        var x = screenX - dlg.View.Frame.X;
        var y = screenY - dlg.View.Frame.Y;
        if (x < 0 || y < 0 || x >= dim.Width || y >= dim.Height)
        { // Outside this menu, forward click to parent menu and close this menu
            if (hasMovedMouse)
            {
                Closed += () => parent?.OnMouseClicked(screenX, screenY);
                UI.Post(() => Close());
            }
            return;
        }
        //hasMovedMouse = true;

        UI.Post(() =>
        {
            itemsView.SetIndex(y - 1);
            // if (CurrentItem is SubMenu && !CurrentItem.IsDisabled)
            // {
            //     OnCursorRight();
            // }
        });
    }


    void OnMouseClicked(int screenX, int screenY)
    {
        // Map screen coordinates to this menu view coordinates
        var x = screenX - dlg.View.Frame.X;
        var y = screenY - dlg.View.Frame.Y;

        if (x < 0 || y < 0 || x >= dim.Width || y >= dim.Height)
        {   // Outside this menu, forward click to parent menu and close this menu
            Closed += () => parent?.OnMouseClicked(screenX, screenY);
            UI.Post(() => Close());
            return;
        }

        // Is inside this menu, handle click
        UI.Post(() => OnClick(x, y - 1));
    }


    // Closes this menu and all parent menus as well. Used menu item action is called.
    void CloseAll()
    {
        Close();
        UI.Post(() => parent?.CloseAll());
    }


    // Calculates menu view dimensions based on screen size and number of items
    Dimensions GetDimensions()
    {
        var screenWidth = Application.Driver.Cols;
        var screenHeight = Application.Driver.Rows;

        // Calculate view height based on number of items, screen height and max height if very large screen 
        var viewHeight = Math.Min(items.Count + 2, Math.Min(maxHeight, screenHeight));

        // Calculate items width based on longest title and shortcut, and if sub menu marker is needed and scrollbar is needed
        var titleWidth = Math.Max(items.Any() ? items.Max(i => i.Title.Length) : 0, title.Length + 4);
        var shortcutWidth = items.Any() ? items.Max(i => i.Shortcut.Length + 1) : 0;  // Include space before
        var subMenuMarkerWidth = items.Any(i => i is SubMenu) ? 2 : 0;  // Include space before 
        var scrollbarWidth = items.Count + 2 > viewHeight ? 1 : 0;

        // Calculate view width based on title, shortcut, sub menu marker and scrollbar
        var viewWidth = titleWidth + shortcutWidth + subMenuMarkerWidth + scrollbarWidth + 2; // (2 for borders)
        if (viewWidth > screenWidth)
        {   // Too wide view, try to fit on screen (reduce title width)
            viewWidth = screenWidth;
            titleWidth = Math.Max(10, viewWidth - shortcutWidth - subMenuMarkerWidth - scrollbarWidth - 1);
        }

        // Calculate view x and y position to be centered if Menu.Center or based on original x and y 
        var viewX = xOrg == Center ? screenWidth / 2 - viewWidth / 2 : xOrg; // Centered if x == Center
        var viewY = yOrg == Center ? screenHeight / 2 - viewHeight / 2 : yOrg; // Centered if y == Center

        if (viewX + viewWidth > screenWidth)
        {   // Too far to the right, try to move menu left
            if (altX >= 0)
            {   // Use alternative x position (left of parent menu)
                viewX = altX - viewWidth;
            }
            else
            {   // Adjust original x position
                viewX = viewX - viewWidth;
            }
        }
        viewX = Math.Max(0, viewX);

        if (viewY + viewHeight > screenHeight)
        {   // Too far down, try to move up
            viewY = screenHeight - viewHeight;
        }
        viewY = Math.Max(0, viewY);

        return new Dimensions(viewX, viewY, viewWidth, viewHeight, titleWidth, shortcutWidth, subMenuMarkerWidth);
    }


    IReadOnlyList<Text> ToMenuItemsRows()
    {
        return items.Select(item =>
        {
            if (item is MenuSeparator ms) return Text.BrightMagenta(ToSeparatorText(ms));

            // Color if disabled or not
            var titleColor = item.IsDisabled ? Color.Dark : Color.White;

            // Title text might need to be truncated
            var text = new TextBuilder();
            if (item.Title.Length > dim.TitleWidth)
            {
                text.Color(titleColor, item.Title.Max(dim.TitleWidth - 1, true)).Dark("…");
            }
            else
            {
                text.Color(titleColor, item.Title.Max(dim.TitleWidth, true));
            }

            // Shortcut
            if (!item.IsDisabled && item.Shortcut != "")
                text.Black(new string(' ', dim.ShortcutWidth - item.Shortcut.Length)).Cyan(item.Shortcut);
            else if (item.Shortcut != "")
                text.Black(new string(' ', dim.ShortcutWidth - item.Shortcut.Length)).Dark(item.Shortcut);
            else if (dim.ShortcutWidth > 0)
                text.Black(new string(' ', dim.ShortcutWidth));

            // Submenu marker >
            if (!item.IsDisabled && item is SubMenu)
                text.BrightMagenta(" >");
            if (item.IsDisabled && item is SubMenu)
                text.Dark(" >");
            if (dim.SubMenuMarkerWidth > 0)
                text.Black("  ");

            return text.ToText();
        })
        .ToList();
    }


    string ToSeparatorText(MenuSeparator item)
    {
        string title = item.Title;
        var width = dim.Width - 2;
        var scrollbarWidth = items.Count + 2 > dim.Height ? 0 : 1;
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
    }


    void OnEsc()
    {
        RootMenu.Closed += () => UI.Post(() => onEscAction());
        UI.Post(() => Close());
    }


    void OnEnter()
    {
        if (items.Any() && CurrentItem is SubMenu)
        {   // For a sub menu, the action is to open menu as if right arrow was pressed
            UI.Post(() => OnCursorRight());
            return;
        }

        // Handle enter key on 'normal' menu item (after root menu has been closed)
        RootMenu.Closed += () =>
        {
            if (items.Any() && !CurrentItem.IsDisabled && CurrentItem.Action != null)
            {
                UI.Post(() => CurrentItem.Action());
            }
        };

        // Close menu and all parent menus, this will trigger the above action
        UI.Post(() => CloseAll());
    }


    void OnClick(int x, int y)
    {
        itemsView.SetIndex(y);
        if (CurrentItem.IsDisabled)
        {
            if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled) OnCursorUp();
            if (CurrentItem.IsDisabled) OnCursorDown();
            return;
        }

        UI.Post(() => OnEnter());
    }


    void OnCursorUp()
    {
        if (itemsView.CurrentIndex <= 0 || isAllDisabled) return;
        itemsView.Move(-1);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled) OnCursorDown();
        if (CurrentItem.IsDisabled) OnCursorUp();
    }

    void OnCursorDown()
    {
        if (itemsView.CurrentIndex >= items.Count - 1 || isAllDisabled) return;
        itemsView.Move(1);

        if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled) OnCursorUp();
        if (CurrentItem.IsDisabled) OnCursorDown();
    }

    void OnPageUp()
    {
        if (itemsView.CurrentIndex <= 0 || isAllDisabled) return;
        itemsView.Move(-itemsView.ViewHeight);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled) OnCursorDown();
        if (CurrentItem.IsDisabled) OnCursorUp();
    }

    void OnPageDown()
    {
        if (itemsView.CurrentIndex >= items.Count - 1 || isAllDisabled) return;
        itemsView.Move(itemsView.ViewHeight);

        if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled) OnCursorUp();
        if (CurrentItem.IsDisabled) OnCursorDown();
    }

    void OnHome()
    {
        if (itemsView.CurrentIndex <= 0 || isAllDisabled) return;
        itemsView.Move(-itemsView.TotalCount);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled) OnCursorDown();
        if (CurrentItem.IsDisabled) OnCursorUp();
    }

    void OnEnd()
    {
        if (itemsView.CurrentIndex >= items.Count - 1 || isAllDisabled) return;
        itemsView.Move(itemsView.TotalCount);

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
            isFocus = false;
            hasMovedMouse = false;
            subMenu.Show(sm.Children);
            isFocus = true;
        }
    }


    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        return itemRows.Skip(firstIndex).Take(count).Select((row, i) =>
        {
            var isSelectedRow = i + firstIndex == currentIndex && !isAllDisabled;
            return isSelectedRow ? row.ToHighlight() : row;
        });
    }
}


// A normal menu item and base class for SubMenu and MenuSeparator
record MenuItem(string Title, string Shortcut, Action Action, Func<bool>? CanExecute = null)
{
    public bool IsDisabled { get; init; }
}


// To create a sub menu
record SubMenu : MenuItem
{
    public SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
        : base(title, shortcut, () => { }, canExecute)
    {
        Children = children;
    }

    public IEnumerable<MenuItem> Children { get; init; }
}


// To create a menu separator line or header line
record MenuSeparator : MenuItem
{
    public MenuSeparator(string title = "")
        : base(title, "", () => { }, () => false)
    { }
}



// Extension methods to make it easier to build menus
static class MenuExtensions
{
    public static ICollection<MenuItem> SubMenu(this ICollection<MenuItem> items, string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
    {
        items.Add(new SubMenu(title, shortcut, children, canExecute));
        return items;
    }

    public static ICollection<MenuItem> SubMenu(this ICollection<MenuItem> items, bool condition, string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
    {
        if (condition) items.Add(new SubMenu(title, shortcut, children, canExecute));
        return items;
    }

    public static ICollection<MenuItem> Item(this ICollection<MenuItem> items, string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        items.Add(new MenuItem(title, shortcut, action, canExecute));
        return items;
    }

    public static ICollection<MenuItem> Item(this ICollection<MenuItem> items, bool condition, string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        if (condition) items.Add(new MenuItem(title, shortcut, action, canExecute));
        return items;
    }


    public static ICollection<MenuItem> Separator(this ICollection<MenuItem> items, string text = "")
    {
        items.Add(new MenuSeparator(text));
        return items;
    }
    public static ICollection<MenuItem> Separator(this ICollection<MenuItem> items, bool condition, string text = "")
    {
        if (condition) items.Add(new MenuSeparator(text));
        return items;
    }

    public static ICollection<MenuItem> Items(this ICollection<MenuItem> items, params MenuItem[] moreItems)
    {
        moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }
    public static ICollection<MenuItem> Items(this ICollection<MenuItem> items, bool condition, params MenuItem[] moreItems)
    {
        if (condition) moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }

    public static ICollection<MenuItem> Items(this ICollection<MenuItem> items, IEnumerable<MenuItem> moreItems)
    {
        moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }

    public static ICollection<MenuItem> Items(this ICollection<MenuItem> items, bool condition, IEnumerable<MenuItem> moreItems)
    {
        if (condition) moreItems.Where(i => i != null).ForEach(i => items.Add(i));
        return items;
    }
}

