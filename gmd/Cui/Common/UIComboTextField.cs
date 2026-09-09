using Terminal.Gui;

namespace gmd.Cui.Common;

// A text input field with a drop down list of suggestions, shown by the down arrow key or by
// clicking the '▼' marker that UIDialog.AddComboTextField adds next to the field. While the list
// is open it has the focus, so the arrow keys move in it, Enter (or a click) picks the item and
// Esc closes it; the field gets the focus back either way. A pick raises ItemSelected, since it
// changes the text without a key reaching the field.
class UIComboTextField : TextField
{
    private readonly int w;

    readonly Func<IReadOnlyList<string>> getItems;
    readonly Label borderTop;
    readonly List<Label> borderSides;
    readonly Label borderBottom;
    readonly ContentView listView;
    private List<string> items = [];
    IReadOnlyList<Text> itemTexts = new List<Text>();
    bool isShowList = false;

    // Raised when an item is picked from the list and has become the text
    public event Action? ItemSelected;

    internal UIComboTextField(int x, int y, int w, int h, Func<IReadOnlyList<string>> getItems, string text = "")
        : base(x, y, w, text)
    {
        ColorScheme = ColorSchemes.TextField;
        this.w = w;
        this.getItems = getItems;

        listView = new ContentView(OnGetContent)
        {
            X = x,
            Y = y + 2,
            Width = w + 2,
            Height = h - 1,
        };

        // For some reason the list view will not show border using the Border property, lets just draw it manually
        borderTop = new Label(x - 1, y + 1, "├" + new string('─', w + 2) + "┤")
        {
            ColorScheme = ColorSchemes.Scrollbar,
        };
        borderSides = Enumerable
            .Range(0, h)
            .Select(i => new Label(x - 1, y + 2 + i, "│" + new string('─', w + 2) + "│")
            {
                ColorScheme = ColorSchemes.Scrollbar,
            })
            .ToList();
        borderBottom = new Label(x - 1, y + h + 1, "└" + new string('─', w + 2) + "┘")
        {
            ColorScheme = ColorSchemes.Scrollbar,
        };
    }

    // Called when clicking on the down arrow
    public void OnFieldMouseClicked(MouseEventArgs e)
    {
        if (e.MouseEvent.Flags == MouseFlags.Button1Clicked && !isShowList)
            UI.Post(() => ShowListView());
        if (e.MouseEvent.Flags == MouseFlags.Button1Clicked && isShowList)
            UI.Post(() => CloseListView());
        e.Handled = false;
    }

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        if (keyEvent.Key == Key.CursorDown)
        { // User press down, show list view (if not already shown)
            if (isShowList)
                return base.ProcessKey(keyEvent);
            ShowListView();
            return true;
        }

        return base.ProcessKey(keyEvent);
    }

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }

    void ShowListView()
    {
        isShowList = true;
        items = getItems().ToList();
        itemTexts = items
            .Select(item =>
                item.Length > w + 1
                    ? Common.Text.Dark("…").White(item[^w..]).ToText()
                    : Common.Text.White(item.Max(w + 1, true)).ToText()
            )
            .ToList();

        listView.RegisterKeyHandler(Key.Esc, () => CloseListView());
        listView.RegisterKeyHandler(Key.Enter, () => SelectItem(listView.CurrentIndex));
        listView.RegisterMouseHandler(MouseFlags.Button1Clicked, (x, y) => SelectItem(y + listView.FirstIndex));

        // Tabbing away from the open list would otherwise leave it lying on the dialog
        listView.Leave += _ => CloseListView();

        listView.IsShowCursor = false;
        listView.IsScrollMode = false;
        listView.IsCursorMargin = false;
        listView.ColorScheme = ColorSchemes.TextField;
        listView.SetNeedsDisplay();

        Dialog dlg = (Dialog)this.SuperView.SuperView;
        dlg.Add(borderTop);
        borderSides.ForEach(bs => dlg.Add(bs));
        dlg.Add(borderBottom);
        dlg.Add(listView);

        // Start on the item the field already names, and take the keys until closed
        listView.SetCurrentIndex(Math.Max(0, items.IndexOf(Text)));
        listView.SetFocus();
        dlg.SetNeedsDisplay();
    }

    void SelectItem(int index)
    {
        if (index >= 0 && index < items.Count)
        {
            Text = items[index].Trim();
            CursorPosition = Text.Length;
        }
        CloseListView();
        if (index >= 0 && index < items.Count)
            ItemSelected?.Invoke();
    }

    void CloseListView()
    {
        if (!isShowList)
            return;
        isShowList = false;

        Dialog dlg = (Dialog)this.SuperView.SuperView;
        dlg.Remove(listView);

        dlg.Remove(borderTop);
        borderSides.ForEach(bs => dlg.Remove(bs));
        dlg.Remove(borderBottom);

        SetFocus();
        dlg.SetNeedsDisplay();
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        var rows = itemTexts
            .Skip(firstIndex)
            .Take(count)
            .Select(
                (item, i) =>
                {
                    // Show selected or unselected commit row
                    var isSelectedRow = i + firstIndex == currentIndex;
                    return isSelectedRow ? item.ToHighlight() : item;
                }
            );

        return (rows, itemTexts.Count);
    }
}
