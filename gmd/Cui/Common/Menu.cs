using Terminal.Gui;

namespace gmd.Cui.Common;

// Context menu
class Menu
{
    readonly string title;
    readonly int xOrg;
    readonly int yOrg;
    readonly int altX;
    readonly Action onEscAction;
    readonly Menu? parent;
    Menu? childSubMenu;
    int childSubMenuIndex;

    UIDialog dlg = null!;
    ContentView itemsView = null!;
    IReadOnlyList<Text> itemRows = null!;
    IReadOnlyList<MenuItem> items = null!;
    MenuDimensions dimensions = null!;
    MenuItem CurrentItem => items[itemsView.CurrentIndex];
    bool isAllDisabled = false;
    bool isFocus = false;
    readonly TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
    Menu RootMenu => parent == null ? this : parent.RootMenu;

    public const int Center = -int.MaxValue;

    public static void Show(string title, int x, int y, IEnumerable<MenuItem> items, Action? onEscAction = null)
    {
        var menu = new Menu(x, y, title, null, -1, onEscAction);
        menu.Show(items);
    }

    // Creating menu helpers
    public static ICollection<MenuItem> Items => [];

    public static MenuItem Item(string text, string shortcut, Action action, Func<bool>? canExecute = null) =>
        new MenuItem(text, shortcut, action, canExecute);

    public static MenuItem Separator(string text = "") => new MenuSeparator(text);

    public static MenuItem SubMenu(
        string text,
        string shortcut,
        IEnumerable<MenuItem> children,
        Func<bool>? canExecute = null
    ) => new SubMenu(text, shortcut, children, canExecute);

    public Menu(int x, int y, string title, Menu? parent, int altX, Action? onEscAction)
    {
        this.xOrg = x;
        this.yOrg = y;
        this.title = title;
        this.parent = parent;
        this.altX = altX;
        this.onEscAction = onEscAction ?? (() => { });
    }

    public void Show(IEnumerable<MenuItem> items)
    {
        this.items = items
            .Select(i =>
                i with
                {
                    IsDisabled =
                        i.IsDisabled || !(i.CanExecute?.Invoke() ?? true) || i is SubMenu sm && !sm.Children.Any(),
                }
            )
            .ToList();

        this.isAllDisabled = this.items.All(i => i.IsDisabled);

        dimensions = MenuDimensions.Calculate(
            this.items,
            title,
            xOrg,
            yOrg,
            altX,
            Application.Driver.Cols,
            Application.Driver.Rows
        );
        itemRows = MenuRows.ToRows(this.items, dimensions);

        dlg = new UIDialog(
            title,
            dimensions.Width,
            dimensions.Height,
            null,
            options =>
            {
                options.X = dimensions.X;
                options.Y = dimensions.Y;
            }
        );

        itemsView = CreateItemsView();

        itemsView.SetNeedsDisplay();
        if (this.items.Any() && this.items[0].IsDisabled && !isAllDisabled)
            UI.Post(() => OnCursorDown());

        isFocus = true;
        Application.RootMouseEvent += OnRootMouseEvent; // To handle mouse clicks both within and also outside this menu to close it
        dlg.Show();
        Application.RootMouseEvent -= OnRootMouseEvent;
        isFocus = false;
        done.TrySetResult(true);
    }

    public async Task CloseAsync()
    {
        if (childSubMenu != null)
        {
            await childSubMenu.CloseAsync();
        }

        await dlg.CloseAsync();
        await done.Task;
    }

    public void Close() => CloseAsync().RunInBackground();

    // Called for all mouse events, both within and also outside this menu,
    // Skipping if not focused or not clicked events
    void OnRootMouseEvent(MouseEvent e)
    {
        e.Handled = false;

        if (!isFocus)
            return;

        if (e.Flags.HasFlag(MouseFlags.Button1Clicked))
            OnMouseClicked(e.X, e.Y);
        if (e.Flags == MouseFlags.ReportMousePosition)
            OnMouseMove(e.X, e.Y);
    }

    void OnMouseMove(int screenX, int screenY)
    {
        (var x, var y) = ToViewCoordinates(screenX, screenY);
        if (!IsInside(x, y))
            return;
        var index = y - 1;

        if (index < 0 || index >= items.Count || !items.Any() || items[index].IsDisabled)
            return;
        itemsView.SetCurrentIndex(index);
    }

    async void OnMouseClicked(int screenX, int screenY)
    {
        (var x, var y) = ToViewCoordinates(screenX, screenY);
        if (!IsInside(x, y))
        { // Clicked outside this menu, close this menu and forward click to parent menu
            await CloseAsync();
            parent?.OnMouseClicked(screenX, screenY);
            if (parent == null)
                onEscAction();
            return;
        }

        // Is inside this menu, handle click
        UI.Post(() => OnClick(x, y - 1));
    }

    ContentView CreateItemsView()
    {
        var view = dlg.AddContentView(0, 0, Dim.Fill(), Dim.Fill(), OnGetContent);
        view.IsShowCursor = false;
        view.IsScrollMode = false;
        view.IsCursorMargin = false;

        view.RegisterKeyHandler(Key.Esc, () => OnKeyEsc());
        view.RegisterKeyHandler(Key.Enter, () => OnEnter());
        view.RegisterKeyHandler(Key.CursorUp, () => OnCursorUp());
        view.RegisterKeyHandler(Key.CursorDown, () => OnCursorDown());
        view.RegisterKeyHandler(Key.PageUp, () => OnPageUp());
        view.RegisterKeyHandler(Key.PageDown, () => OnPageDown());
        view.RegisterKeyHandler(Key.Home, () => OnHome());
        view.RegisterKeyHandler(Key.End, () => OnEnd());
        view.RegisterKeyHandler(Key.CursorLeft, () => OnCursorLeft());
        view.RegisterKeyHandler(Key.CursorRight, () => OpenSubMenu());

        return view;
    }

    async void OnKeyEsc()
    {
        await CloseAsync();
        if (parent == null)
        {
            onEscAction();
        }
    }

    async void OnEnter()
    {
        if (items.Any() && CurrentItem is SubMenu)
        { // For a sub menu, the action is to open menu as if right arrow was pressed
            if (childSubMenuIndex == itemsView.CurrentIndex)
            { // Clicked on same item as before, ignore
                childSubMenuIndex = -1;
                return;
            }
            UI.Post(() => OpenSubMenu());
            return;
        }

        // Store items action before closing menu
        Action? action = items.Any() && !CurrentItem.IsDisabled ? CurrentItem.Action : null;
        await RootMenu.CloseAsync();

        action?.Invoke();
    }

    void OnClick(int _, int y)
    {
        itemsView.SetIndexAtViewY(y);
        if (CurrentItem.IsDisabled)
        { // Clicked on disabled item, lets try select next enabled item
            if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled)
                OnCursorUp();
            if (CurrentItem.IsDisabled)
                OnCursorDown();
            return;
        }

        UI.Post(() => OnEnter());
    }

    void OnCursorUp()
    {
        if (itemsView.CurrentIndex <= 0 || isAllDisabled)
            return;
        itemsView.Move(-1);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled)
            OnCursorDown();
        if (CurrentItem.IsDisabled)
            OnCursorUp();
    }

    void OnCursorDown()
    {
        if (itemsView.CurrentIndex >= items.Count - 1 || isAllDisabled)
            return;
        itemsView.Move(1);

        if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled)
            OnCursorUp();
        if (CurrentItem.IsDisabled)
            OnCursorDown();
    }

    void OnPageUp()
    {
        if (itemsView.CurrentIndex <= 0 || isAllDisabled)
            return;
        itemsView.Move(-itemsView.ViewHeight);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled)
            OnCursorDown();
        if (CurrentItem.IsDisabled)
            OnCursorUp();
    }

    void OnPageDown()
    {
        if (itemsView.CurrentIndex >= items.Count - 1 || isAllDisabled)
            return;
        itemsView.Move(itemsView.ViewHeight);

        if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled)
            OnCursorUp();
        if (CurrentItem.IsDisabled)
            OnCursorDown();
    }

    void OnHome()
    {
        if (itemsView.CurrentIndex <= 0 || isAllDisabled)
            return;
        itemsView.Move(-itemsView.TotalCount);

        if (itemsView.CurrentIndex <= 0 && CurrentItem.IsDisabled)
            OnCursorDown();
        if (CurrentItem.IsDisabled)
            OnCursorUp();
    }

    void OnEnd()
    {
        if (itemsView.CurrentIndex >= items.Count - 1 || isAllDisabled)
            return;
        itemsView.Move(itemsView.TotalCount);

        if (itemsView.CurrentIndex >= items.Count - 1 && CurrentItem.IsDisabled)
            OnCursorUp();
        if (CurrentItem.IsDisabled)
            OnCursorDown();
    }

    void OnCursorLeft()
    {
        if (parent == null)
            return; // Do not close top level menu on left arrow (only sub menus)
        CloseAsync().RunInBackground();
    }

    void OpenSubMenu()
    {
        if (items.Any() && CurrentItem is SubMenu sm && !sm.IsDisabled)
        {
            var x = dimensions.X + dimensions.Width;
            var y = dimensions.Y + (itemsView.CurrentIndex - itemsView.FirstIndex);

            var title = sm.Text.Trim();
            childSubMenu = new Menu(x, y, title, this, dimensions.X, null);
            childSubMenuIndex = itemsView.CurrentIndex;
            isFocus = false;
            childSubMenu.Show(sm.Children);
            childSubMenu = null;
            isFocus = true;
        }
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        var rows = itemRows
            .Skip(firstIndex)
            .Take(count)
            .Select(
                (row, i) =>
                {
                    var isSelectedRow = i + firstIndex == currentIndex && !isAllDisabled;
                    return isSelectedRow ? row.ToHighlight() : row;
                }
            );

        return (rows, itemRows.Count);
    }

    (int x, int y) ToViewCoordinates(int screenX, int screenY)
    {
        var x = screenX - dlg.View.Frame.X;
        var y = screenY - dlg.View.Frame.Y;
        return (x, y);
    }

    bool IsInside(int x, int y) => x >= 0 && x < dimensions.Width && y >= 0 && y < dimensions.Height;
}
