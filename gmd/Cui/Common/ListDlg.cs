using Terminal.Gui;

namespace gmd.Cui.Common;

// A dialog with a list to pick a row from: the header above the list, below it what the selected row
// is about, and a button per action. Each button is also a key on the list, the letter after the '_'
// in its label, in either case, and Enter for the first one, so an action is one key away while the
// list has the focus rather than a Tab away. A dumb dialog: it returns what the action makes of the
// selected row and closes, and the command acts on it, and shows it again if it is a list to come
// back to. The rows are drawn by a *Rows class, which can be asserted without a driver.
class ListDlg<TItem, TChoice>
    where TChoice : class
{
    const int maxListHeight = 15;

    record ListAction(string Label, Func<TItem, TChoice> Choose, Func<TItem, bool> CanAct);

    readonly string title;
    readonly int minWidth;
    readonly int maxWidth;
    readonly Func<int, Text> header;
    readonly Func<TItem, int, Text> row;
    readonly Func<TItem, string> about;
    readonly List<ListAction> actions = [];

    // The list is as wide as the terminal allows up to maxWidth, but never narrower than minWidth, the
    // rows' own minimum. A header and a row are drawn for the width of the list.
    public ListDlg(
        string title,
        int minWidth,
        int maxWidth,
        Func<int, Text> header,
        Func<TItem, int, Text> row,
        Func<TItem, string> about
    )
    {
        this.title = title;
        this.minWidth = minWidth;
        this.maxWidth = maxWidth;
        this.header = header;
        this.row = row;
        this.about = about;
    }

    // An action, labeled with its key after a '_', and whether it can act on the selected row (always,
    // when null), which greys out its button when it cannot
    public ListDlg<TItem, TChoice> Action(string label, Func<TItem, TChoice> choose, Func<TItem, bool>? canAct = null)
    {
        actions.Add(new ListAction(label, choose, canAct ?? (_ => true)));
        return this;
    }

    public Result<TChoice> Show(IReadOnlyList<TItem> items, int selectedIndex = 0)
    {
        Asserter.Requires(items.Count > 0 && actions.Count > 0);

        var width = Math.Max(minWidth + 6, Math.Min(maxWidth, Application.Driver.Cols - 2));
        var listWidth = width - 6;
        var listHeight = Math.Clamp(items.Count, 1, maxListHeight);
        var height = listHeight + 7;

        var dlg = new UIDialog(title, width, height);
        dlg.AddLabel(2, 0, header(listWidth));

        var list = dlg.AddContentView(2, 2, listWidth, listHeight, items.Select(i => row(i, listWidth)).ToList());
        list.IsShowCursor = false;
        list.IsScrollMode = false;
        list.IsCursorMargin = false;
        list.IsHighlightCurrentIndex = true;
        dlg.AddBorderView(list, Color.Dark);

        var aboutLabel = dlg.AddLabel(2, listHeight + 3, "");

        TChoice? choice = null;
        TItem Selected() => items[Math.Clamp(list.CurrentIndex, 0, items.Count - 1)];
        void Choose(ListAction action)
        {
            if (!action.CanAct(Selected()))
                return;
            choice = action.Choose(Selected());
            dlg.Close();
        }

        // Left to right, two apart, and Close at the right edge. A button is its label, with no '_',
        // in '[ ' and ' ]'.
        var y = listHeight + 4;
        var x = 1;
        List<Button> buttons = [];
        foreach (var action in actions)
        {
            buttons.Add(dlg.AddButton(x, y, action.Label, () => Choose(action)));
            x += action.Label.Replace("_", "").Length + 4 + 2;
        }
        dlg.AddButton(width - 13, y, "Close", () => dlg.Close());

        void UpdateForSelected()
        {
            var item = Selected();
            aboutLabel.Text = Text.Dark(about(item)).ToText();
            for (var i = 0; i < actions.Count; i++)
            {
                buttons[i].Enabled = actions[i].CanAct(item);
            }
        }
        list.CurrentIndexChange += UpdateForSelected;

        list.RegisterKeyHandler(Key.Enter, () => Choose(actions[0]));
        foreach (var action in actions)
        {
            list.RegisterLetterHandler(KeyOf(action.Label), () => Choose(action));
        }
        list.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        list.SetCurrentIndex(Math.Clamp(selectedIndex, 0, items.Count - 1));
        UpdateForSelected();

        dlg.Show(list);
        return choice != null ? choice : new Error();
    }

    // The letter after the '_', in lower case
    static Key KeyOf(string label)
    {
        var index = label.IndexOf('_');
        Asserter.Requires(index >= 0 && index < label.Length - 1);
        return (Key)char.ToLowerInvariant(label[index + 1]);
    }
}
