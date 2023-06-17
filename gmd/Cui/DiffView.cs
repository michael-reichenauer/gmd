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
        view.RegisterKeyHandler(Key.CursorLeft | Key.ShiftMask, OnLeftCopy);
        view.RegisterKeyHandler(Key.CursorRight | Key.ShiftMask, OnRightCopy);


        view.RegisterKeyHandler(Key.i, OnInteractive);
    }

    void OnLeftCopy()
    {
        IsSelectedLeft = true;
        contentView.SetNeedsDisplay();
    }

    void OnRightCopy()
    {
        IsSelectedLeft = false;
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

        if (selectedCount > 0)
        {   // Clear down selection
            for (int i = selectedIndex; i < selectedIndex + selectedCount; i++)
            {
                diffRows.SetHighlighted(i, false);
            }

        }
        else
        {   // Clear upp selection
            for (int i = selectedIndex; i > selectedIndex + selectedCount; i--)
            {
                diffRows.SetHighlighted(i, false);
            }
        }

        selectedIndex = -1;
        selectedCount = 0;
    }

    void OnCopyUp()
    {
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
            diffRows.SetHighlighted(currentIndex, !currentRow.IsHighlighted);
            selectedCount--;
            return;
        }

        // Expand selection upp
        diffRows.SetHighlighted(currentIndex, !currentRow.IsHighlighted);
        selectedCount--;

        if (currentIndex <= 0)
        {   // Reache top of page
            return;
        }

        contentView.Move(-1);
    }

    void OnCopyDown()
    {
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
            return;
        }

        // Expand selection down
        diffRows.SetHighlighted(currentIndex, !currentRow.IsHighlighted);
        selectedCount++;

        if (currentIndex >= contentView.Count - 1)
        {   // Reache end of page
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
        if (rowStartX > 0)
        {
            rowStartX--;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }


    private void OnRightArrow()
    {
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
        DiffRowMode.Line => row.Left.AddLine(oneColumnWidth),
        DiffRowMode.SpanBoth => row.Left.Subtext(0, oneColumnWidth),
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
