using gmd.Cui.Common;
using Terminal.Gui;


namespace gmd.Cui.Diff;

interface IDiffView
{
    void Show(Server.CommitDiff diff, string commitId);
    void Show(Server.CommitDiff[] diffs, string commitId = "");
}


class DiffView : IDiffView
{
    static readonly Text splitLineChar = Text.New.Dark("│");

    readonly IDiffService diffService;

    ContentView contentView = null!;
    Toplevel? diffView;
    DiffRows diffRows = new DiffRows();
    int rowStartX = 0;
    string commitId = "";

    bool IsSelectedLeft = true;

    public DiffView(IDiffService diffService)
    {
        this.diffService = diffService;
    }


    public void Show(Server.CommitDiff diff, string commitId) => Show(new[] { diff }, commitId);

    public void Show(Server.CommitDiff[] diffs, string commitId)
    {
        rowStartX = 0;
        this.commitId = commitId;
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
        view.RegisterKeyHandler(Key.CursorUp, OnMoveUp);
        view.RegisterKeyHandler(Key.CursorDown, OnMoveDown);
        view.RegisterKeyHandler(Key.CursorLeft, OnMoveLeft);
        view.RegisterKeyHandler(Key.CursorRight, OnMoveRight);

        // For copy support. Cursor is needed to select text
        view.RegisterKeyHandler(Key.i, ToggleShowCursor);
        view.RegisterKeyHandler(Key.CursorUp | Key.ShiftMask, OnSelectUp);
        view.RegisterKeyHandler(Key.CursorDown | Key.ShiftMask, OnSelectDown);

        view.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);
    }

    // Normal move upp, but clears selection if any
    void OnMoveUp()
    {
        ClearSelection();
        contentView.Move(-1);
    }

    // Normal move down, but clears selection if any
    void OnMoveDown()
    {
        ClearSelection();
        contentView.Move(1);
    }

    // Move boths sides in view left, or select left text if text is selected
    void OnMoveLeft()
    {
        if (!IsSelectedLeft && selectedStartIndex != -1)
        {   // Text is selected, lets move selection from right to left side
            IsSelectedLeft = true;
            contentView.SetNeedsDisplay();
            return;
        }

        // Move boths sides in view left
        if (rowStartX > 0)
        {
            rowStartX--;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }


    // Move boths sides in view right, or select right text if text is selected
    void OnMoveRight()
    {
        if (IsSelectedLeft && selectedStartIndex != -1)
        {   // Text is selected, lets move selection from left to right side
            IsSelectedLeft = false;
            contentView.SetNeedsDisplay();
            return;
        }

        // Move boths sides in view right
        int maxColumnWidth = contentView!.ContentWidth / 2;
        if (diffRows!.MaxLength - rowStartX > maxColumnWidth)
        {
            rowStartX++;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }

    // Returns the content for the view
    IEnumerable<Text> OnGetContent(int firstRow, int rowCount, int rowStartX, int contentWidth)
    {
        int columnWidth = (contentWidth - 2) / 2;
        int viewWidth = columnWidth * 2 + 1;

        return diffRows.Rows.Skip(firstRow).Take(rowCount)
            .Select((r, i) => ToDiffRowText(r, i + firstRow, columnWidth, viewWidth));
    }

    // Returns a row with either a line, a span or two columns of side by side text
    Text ToDiffRowText(DiffRow row, int index, int columnWidth, int viewWidth)
    {
        var isHighlighted = selectedStartIndex != -1 && index >= selectedStartIndex && index <= selectedEndIndex;
        if (row.Mode == DiffRowMode.DividerLine)
        {   // A line in the view, e.g. ━━━━, ══════, that need to be expanded to the full view width
            var line = row.Left.AddLine(viewWidth);
            return isHighlighted ? Text.New.WhiteSelected(line.ToString()) : line;
        }

        if (row.Mode == DiffRowMode.SpanBoth)
        {   // The left text spans over full width 
            var text = row.Left.Subtext(0, viewWidth);
            return isHighlighted ? Text.New.WhiteSelected(text.ToString()) : text;
        }

        // The left and right text is shown side by side with a gray vertical line char in between
        var left = row.Left.Subtext(rowStartX, columnWidth, true);
        var right = row.Right.Subtext(rowStartX, columnWidth, true);
        return Text.New
            .Add(isHighlighted && IsSelectedLeft ? Text.New.WhiteSelected(left.ToString()) : left)
            .Add(splitLineChar)
            .Add(isHighlighted && !IsSelectedLeft ? Text.New.WhiteSelected(right.ToString()) : right);
    }

    // Toggle showing/hiding cursor. Cursor is needed to select text for copy
    void ToggleShowCursor()
    {
        ClearSelection();
        contentView!.IsNoCursor = !contentView!.IsNoCursor;
        contentView.SetNeedsDisplay();
    }

    // Copy selected text to clipboard and clear selection
    void OnCopy()
    {
        if (selectedStartIndex == -1)
        {
            UI.ErrorMessage("No selection to copy");
            return;
        }

        var rows = diffRows.Rows.Skip(selectedStartIndex).Take(selectedEndIndex - selectedStartIndex + 1);

        // Convert left or right rows to text, remove empty lines and line numbers
        var text = string.Join("\n", rows
            .Select(r => IsSelectedLeft || r.Mode != DiffRowMode.SideBySide ? r.Left : r.Right)
            .Select(t => t.ToString())
            .Select(t => t.Length > 4 && char.IsNumber(t[3]) ? t.Substring(5) : t)
            .Where(t => !t.StartsWith('░')));

        if (!Try(out var e, Utils.Clipboard.Set(text)))
        {
            UI.ErrorMessage($"Failed to copy to clipboard\nError: {e}");
        }

        ClearSelection();
        contentView.SetNeedsDisplay();
    }


    void ClearSelection()
    {
        selectedStartIndex = -1;
        selectedEndIndex = -1;
    }


    int selectedStartIndex = -1;
    int selectedEndIndex = -1;

    void OnSelectUp()
    {
        int currentIndex = contentView.CurrentIndex;

        if (selectedStartIndex == -1)
        {   // Start selection of current row
            selectedStartIndex = currentIndex;
            selectedEndIndex = currentIndex;
            contentView.SetNeedsDisplay();
            return;
        }

        if (currentIndex == 0)
        {   // Already at top of page, no need to move         
            return;
        }

        if (currentIndex <= selectedStartIndex)
        {   // Expand selection upp
            selectedStartIndex = currentIndex - 1;
        }
        else
        {   // Shrink selection upp
            selectedEndIndex = selectedEndIndex - 1;
        }

        contentView.Move(-1);
    }


    void OnSelectDown()
    {
        int currentIndex = contentView.CurrentIndex;

        if (selectedStartIndex == -1)
        {   // Start selection of current row
            selectedStartIndex = currentIndex;
            selectedEndIndex = currentIndex;
            contentView.SetNeedsDisplay();
            return;
        }

        if (currentIndex >= contentView.Count - 1)
        {   // Already at bottom of page, no need to move         
            return;
        }

        if (currentIndex >= selectedEndIndex)
        {   // Expand selection down
            selectedEndIndex = currentIndex + 1;
        }
        else if (currentIndex <= selectedStartIndex)
        {   // Shrink selection down
            selectedStartIndex = selectedStartIndex + 1;
        }

        contentView.Move(1);
    }
}
