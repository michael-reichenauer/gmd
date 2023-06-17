using gmd.Cui.Common;
using Terminal.Gui;


namespace gmd.Cui;

interface IDiffView
{
    void Show(Server.CommitDiff diff, string commitId);
    void Show(Server.CommitDiff[] diffs, string commitId = "");
}


class DiffView : IDiffView
{
    static readonly Text splitLine = Text.New.Dark("│");

    readonly IDiffConverter diffService;

    ContentView contentView = null!;
    Toplevel? diffView;
    DiffRows diffRows = new DiffRows();
    int rowStartX = 0;
    string commitId = "";
    bool isInteractive = false;

    int selectedIndex = -1;
    int selectedCount = 0;
    bool IsSelectedLeft = true;

    public DiffView(IDiffConverter diffService)
    {
        this.diffService = diffService;
    }


    public void Show(Server.CommitDiff diff, string commitId) => Show(new[] { diff }, commitId);

    public void Show(Server.CommitDiff[] diffs, string commitId)
    {
        rowStartX = 0;
        this.commitId = commitId;
        isInteractive = false;
        selectedIndex = -1;
        selectedCount = 0;
        IsSelectedLeft = true;

        diffView = new Toplevel() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), };
        contentView = new ContentView(OnGetContent)
        { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), IsNoCursor = true, IsCursorMargin = true };

        diffView.Add(contentView);

        RegisterShortcuts(contentView);

        diffRows = diffService.ToDiffRows(diffs);
        contentView.TriggerUpdateContent(diffRows.Rows.Count);

        UI.RunDialog(diffView);
    }


    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.CursorRight, OnRightArrow);
        view.RegisterKeyHandler(Key.CursorLeft, OnLeftArrow);
        view.RegisterKeyHandler(Key.CursorUp, OnMoveUp);
        view.RegisterKeyHandler(Key.CursorDown, OnMoveDown);

        view.RegisterKeyHandler(Key.CursorUp | Key.ShiftMask, OnCopyUp);
        view.RegisterKeyHandler(Key.CursorDown | Key.ShiftMask, OnCopyDown);
        view.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);
        view.RegisterKeyHandler(Key.i, OnInteractive);
    }

    void OnCopy()
    {
        if (selectedIndex == -1)
        {
            UI.ErrorMessage("No selection to copy");
            return;
        }

        var firstIndex = selectedCount > 0 ? selectedIndex : selectedIndex + selectedCount;
        var count = selectedCount > 0 ? selectedCount : -selectedCount;

        var rows = diffRows.Rows.Skip(firstIndex).Take(count);

        // Convert left or right rows to text, remove empty lines and line numbers
        var text = string.Join("\n", rows
            .Select(r => IsSelectedLeft || r.Mode != DiffRowMode.LeftRight ? r.Left : r.Right)
            .Select(t => t.ToString())
            .Select(t => t.Length > 4 && Char.IsNumber(t[3]) ? t.Substring(5) : t)
            .Where(t => !t.StartsWith('░')));

        if (!Try(out var e, Utils.Clipboard.Set(text)))
        {
            UI.ErrorMessage($"Failed to copy to clipboard\nError: {e}");
        }

        ClearSelection();
        contentView.SetNeedsDisplay();
    }




    void OnMoveUp()
    {
        ClearSelection();
        contentView.Move(-1);
    }

    void OnMoveDown()
    {
        ClearSelection();
        contentView.Move(1);
    }

    void ClearSelection()
    {
        if (selectedIndex == -1) return;
        var firstIndex = selectedCount > 0 ? selectedIndex : selectedIndex + selectedCount;
        var count = selectedCount > 0 ? selectedCount : -selectedCount;

        for (int i = firstIndex; i < firstIndex + count; i++)
        {
            diffRows.SetHighlighted(i, false);
        }

        selectedIndex = -1;
        selectedCount = 0;
    }

    void OnCopyUp()
    {
        if (contentView.CurrentIndex <= 0) return;
        int currentIndex = contentView.CurrentIndex;
        var currentRow = diffRows.Rows[currentIndex];

        if (selectedIndex == -1)
        {   // Start selection
            selectedIndex = currentIndex;
        }

        if (currentIndex > selectedIndex)
        {   // Shrink selection upp
            contentView.Move(-1);
            currentIndex = contentView.CurrentIndex;
            currentRow = diffRows.Rows[currentIndex];
            diffRows.SetHighlighted(currentIndex, false);
            selectedCount--;
            if (selectedCount == 0) selectedIndex = -1;
            return;
        }

        // Expand selection upp
        diffRows.SetHighlighted(currentIndex, true);
        if (selectedIndex + selectedCount <= 0) return;
        selectedCount--;

        if (currentIndex <= 0)
        {   // Reache top of page
            contentView.SetNeedsDisplay();
            return;
        }

        contentView.Move(-1);
    }

    void OnCopyDown()
    {
        if (contentView.CurrentIndex >= contentView.Count - 1) return;

        int currentIndex = contentView.CurrentIndex;
        var currentRow = diffRows.Rows[currentIndex];

        if (selectedIndex == -1)
        {   // Start selection
            selectedIndex = currentIndex;
        }

        if (currentIndex < selectedIndex)
        {   // Shrink selection down
            contentView.Move(1);
            currentIndex = contentView.CurrentIndex;
            currentRow = diffRows.Rows[currentIndex];
            diffRows.SetHighlighted(currentIndex, !currentRow.IsHighlighted);
            selectedCount++;
            if (selectedCount == 0) selectedIndex = -1;
            return;
        }

        // Expand selection down
        diffRows.SetHighlighted(currentIndex, !currentRow.IsHighlighted);
        if (selectedIndex + selectedCount >= contentView.Count) return;
        selectedCount++;

        if (currentIndex >= contentView.Count - 1)
        {   // Reache end of page
            contentView.SetNeedsDisplay();
            return;
        }

        contentView.Move(1);
    }

    private void OnInteractive()
    {
        ClearSelection();
        isInteractive = !isInteractive;
        contentView!.IsNoCursor = !isInteractive;
        contentView.SetNeedsDisplay();
    }

    private void OnLeftArrow()
    {
        if (selectedIndex != -1)
        {
            IsSelectedLeft = true;
            contentView.SetNeedsDisplay();
            return;
        }

        if (rowStartX > 0)
        {
            rowStartX--;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }


    private void OnRightArrow()
    {
        if (selectedIndex != -1)
        {
            IsSelectedLeft = false;
            contentView.SetNeedsDisplay();
            return;
        }

        int maxColumnWidth = contentView!.ContentWidth / 2;
        if (diffRows!.MaxLength - rowStartX > maxColumnWidth)
        {
            rowStartX++;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }


    IEnumerable<Text> OnGetContent(int firstRow, int rowCount, int rowStartX, int contentWidth)
    {
        int columnWidth = (contentWidth - 2) / 2;
        int oneColumnWidth = columnWidth * 2 + 1;

        return diffRows.Rows.Skip(firstRow).Take(rowCount)
            .Select(r => ToText(r, columnWidth, oneColumnWidth));
    }


    private Text ToText(DiffRow row, int columnWidth, int oneColumnWidth) => row.Mode switch
    {
        DiffRowMode.Line => row.IsHighlighted ? Text.New.WhiteSelected(row.Left.AddLine(oneColumnWidth).ToString()) : row.Left.AddLine(oneColumnWidth),
        DiffRowMode.SpanBoth => row.IsHighlighted ? Text.New.WhiteSelected(row.Left.Subtext(0, oneColumnWidth).ToString()) : row.Left.Subtext(0, oneColumnWidth),
        DiffRowMode.LeftRight => Text.New
            .Add(row.IsHighlighted && IsSelectedLeft ?
                Text.New.WhiteSelected(row.Left.Subtext(rowStartX, columnWidth, true).ToString()) :
                row.Left.Subtext(rowStartX, columnWidth, true))
            .Add(splitLine)
            .Add(row.IsHighlighted && !IsSelectedLeft ?
                 Text.New.WhiteSelected(row.Right.Subtext(rowStartX, columnWidth, true).ToString()) :
                 row.Right.Subtext(rowStartX, columnWidth, true)),
        _ => throw Asserter.FailFast($"Unknown row mode {row.Mode}")
    };
}
