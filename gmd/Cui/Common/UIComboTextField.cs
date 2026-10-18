using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace gmd.Cui.Common;

// A text input field with a drop down list of suggestions, shown by the down arrow key or by
// clicking the '▼' marker that UIDialog.AddComboTextField adds next to the field. While the list
// is open it has the focus, so the arrow keys move in it, Enter (or a click) picks the item and
// Esc closes it; the field gets the focus back either way. A pick raises ItemSelected, since it
// changes the text without a key reaching the field. A UITextField otherwise, so a right click
// opens gmd's context menu, not Terminal.Gui's.
class UIComboTextField : UITextField
{
    private readonly int w;

    readonly Func<IReadOnlyList<string>> getItems;
    readonly BorderView border;
    readonly ContentView listView;
    private List<string> items = [];
    IReadOnlyList<Text> itemTexts = new List<Text>();
    bool isShowList = false;

    // Raised when an item is picked from the list and has become the text
    public event Action? ItemSelected;

    internal UIComboTextField(int x, int y, int w, int h, Func<IReadOnlyList<string>> getItems, string text = "")
        : base(x, y, w, text)
    {
        this.w = w;
        this.getItems = getItems;

        listView = new ContentView(OnGetContent)
        {
            X = x,
            Y = y + 2,
            Width = w + 2,
            Height = h - 1,
            IsShowCursor = false,
            IsScrollMode = false,
        }.WithScheme(() => ColorSchemes.TextField);
        // Once, here: they were added at every open, and the focus change is an event, so each open
        // added one
        listView.RegisterKeyHandler(Key.Esc, () => CloseListView());
        listView.RegisterKeyHandler(Key.Enter, () => SelectItem(listView.CurrentIndex));
        listView.RegisterMouseHandler(MouseFlags.LeftButtonClicked, (x, y) => SelectItem(y + listView.FirstIndex));
        // Tabbing away from the open list, or an arrow key it has no use for, which Terminal.Gui 2.x
        // moves the focus with, would otherwise leave it lying on the dialog. Closed once that change
        // of the focus is done, and the focus left where it went: changed within it, as it was, 2.x
        // threw, and gmd crashed.
        listView.HasFocusChanged += (_, e) =>
        {
            if (!e.NewValue)
                UI.Post(() => CloseListView(isFocusBack: false));
        };

        // The frame around the list, its top joining the field's underline
        border = new BorderView(Color.BrightMagenta)
        {
            X = x - 1,
            Y = y + 1,
            Width = w + 4,
            Height = h + 1,
        };
    }

    // Called when clicking on the down arrow
    public void OnMarkerClicked()
    {
        if (!isShowList)
            UI.Post(() => ShowListView());
        else
            UI.Post(() => CloseListView());
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.CursorDown && !isShowList)
        { // User press down, show list view (if not already shown)
            ShowListView();
            return true;
        }

        return base.OnKeyDown(key);
    }

    void ShowListView()
    {
        if (SuperView is not View dlg)
            return;
        isShowList = true;
        items = getItems().ToList();
        itemTexts = items
            .Select(item =>
                item.Length > w + 1
                    ? Common.Text.Dark("…").White(item[^w..]).ToText()
                    : Common.Text.White(item.Max(w + 1, true)).ToText()
            )
            .ToList();

        listView.SetTotalCount(itemTexts.Count);
        listView.SetNeedsDraw();

        dlg.Add(border);
        dlg.Add(listView);

        // Start on the item the field already names, and take the keys until closed
        listView.SetCurrentIndex(Math.Max(0, items.IndexOf(Text)));
        listView.SetFocus();
        dlg.SetNeedsDraw();
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

    // Closes the list, and gives the field the focus back, unless the focus went elsewhere
    void CloseListView(bool isFocusBack = true)
    {
        if (!isShowList || SuperView is not View dlg)
            return;
        isShowList = false;

        dlg.Remove(listView);
        dlg.Remove(border);

        if (isFocusBack)
            SetFocus();
        dlg.SetNeedsDraw();
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
