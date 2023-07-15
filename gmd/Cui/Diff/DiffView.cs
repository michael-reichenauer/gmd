using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;


namespace gmd.Cui.Diff;

interface IDiffView
{
    bool Show(Server.CommitDiff diff, string commitId, string repoPath);
    void Show(Server.CommitDiff[] diffs);
}

class DiffView : IDiffView
{
    static readonly Text splitLineChar = Text.Dark("│");

    readonly IDiffService diffService;
    private readonly IProgress progress;
    private readonly IServer server;
    ContentView contentView = null!;
    Server.CommitDiff[] diffs = null!;
    DiffRows diffRows = new DiffRows();
    int rowStartX = 0;
    string commitId = "";
    string repoPath = "";
    bool IsSelectedLeft = true;
    bool isCommitTriggered = false;
    bool IsSelected => contentView.SelectCount > 0;

    public DiffView(IDiffService diffService, IProgress progress, IServer server)
    {
        this.diffService = diffService;
        this.progress = progress;
        this.server = server;
    }


    public bool Show(Server.CommitDiff diff, string commitId, string repoPath) => Show(new[] { diff }, commitId, repoPath);

    public void Show(Server.CommitDiff[] diff) => Show(diff, "", "");

    bool Show(Server.CommitDiff[] diffs, string commitId, string repoPath)
    {
        this.diffs = diffs;
        this.rowStartX = 0;
        this.commitId = commitId;
        this.repoPath = repoPath;
        this.IsSelectedLeft = true;
        this.isCommitTriggered = false;
        this.diffRows = diffService.ToDiffRows(diffs);

        Toplevel diffView = new Toplevel() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        contentView = new ContentView(OnGetContent)
        { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), IsShowCursor = false, IsScrollMode = true, IsCursorMargin = true };

        diffView.Add(contentView);
        RegisterShortcuts(contentView);

        contentView.TriggerUpdateContent(diffRows.Rows.Count);
        UI.RunDialog(diffView);
        return isCommitTriggered;
    }


    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.Esc, () => Application.RequestStop());
        view.RegisterKeyHandler(Key.CursorLeft, OnMoveLeft);
        view.RegisterKeyHandler(Key.CursorRight, OnMoveRight);
        view.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);
        view.RegisterKeyHandler(Key.m, () => ShowMainMenu());

        view.RegisterKeyHandler(Key.r, () => RefreshDiff());
        view.RegisterKeyHandler(Key.d, () => RefreshDiff());
        view.RegisterKeyHandler(Key.s, () => ShowScrollMenu());
        view.RegisterKeyHandler(Key.u, () => ShowUndoMenu());
        view.RegisterKeyHandler(Key.c, () => TriggerCommit());
    }


    void ShowMainMenu()
    {
        var undoItems = GetUndoItems();
        var scrollToItems = GetScrollToItems();

        Menu.Show(1, 2, Menu.Items
            .SubMenu("Scroll to", "S", scrollToItems)
            .SubMenu("Undo/Restore Uncommitted", "U", undoItems)
            .Item("Refresh", "R", () => RefreshDiff(), () => undoItems.Any())
            .Item("Commit", "C", () => TriggerCommit(), () => undoItems.Any())
            .Item("Toggle Select Mode", "I", () => contentView.ToggleShowCursor())
            .Item("Copy Selected Text", "Ctrl+C", () => OnCopy(), () => IsSelected)
            .Item("Select in Left Column", "←", () => OnMoveLeft(), () => IsSelected)
            .Item("Select in Right Column", "→", () => OnMoveRight(), () => IsSelected)
            .Item("Close", "Esc", () => Application.RequestStop()),
            "Diff Menu");
    }


    void ShowScrollMenu()
    {
        var scrollToItems = GetScrollToItems();
        if (!scrollToItems.Any()) return;

        Menu.Show(1, 2, scrollToItems, "Scroll to");
    }

    void ShowUndoMenu()
    {
        var undoItems = GetUndoItems();
        if (!undoItems.Any()) return;
        Menu.Show(1, 2, undoItems, "Undo/Restore Uncommitted");
    }

    void TriggerCommit()
    {
        isCommitTriggered = true;
        Application.RequestStop();
    }

    IEnumerable<Common.MenuItem> GetScrollToItems()
    {
        if (diffs.Length > 1)
        {
            return diffs.Select(cd =>
                 Menu.Item($"{cd.Time.IsoDate()} {cd.Message}", "", () => ScrollToCommit(cd.Id)));
        }

        var paths = diffService.GetDiffFilePaths(diffs[0]);
        if (!paths.Any()) return Menu.Items;

        return Menu.Items.Items(paths.Select(p => Menu.Item(p, "", () => ScrollToFile(p))));
    }

    void ScrollToCommit(string commitId)
    {
        var lineIndex = diffRows.Rows.FindIndexOf(r => r.CommitId == commitId);
        contentView.ScrollToShowIndex(lineIndex - 1);
    }

    void ScrollToFile(string path)
    {
        // Find the row indexes where the file diff starts
        var lineIndex = diffRows.Rows.FindIndexOf(r => r.FilePath == path);
        contentView.ScrollToShowIndex(lineIndex - 1);
    }

    IEnumerable<Common.MenuItem> GetUndoItems()
    {
        var paths = diffService.GetDiffFilePaths(diffs[0]);
        if (commitId != Repo.UncommittedId || paths.Count == 0) return new List<Common.MenuItem>();

        var binaryPaths = diffService.GetDiffBinaryFilePaths(diffs[0]);

        var undoItems = paths.Select(p => new Common.MenuItem(p, "", () => UndoFile(p)));
        if (undoItems.Count() > 10)
        {   // Show files ith sub menu
            undoItems = new[] { new SubMenu("Uncommitted Files", "", undoItems) };
        }

        return Menu.Items
            .Items(undoItems)
            .Separator()
            .Item("All Uncommitted Binary Files", "", () => UndoAllBinaryFiles(binaryPaths), () => binaryPaths.Any())
            .Item("All Uncommitted Changes", "", () => UndoAll());
    }

    async void UndoAllBinaryFiles(IReadOnlyList<string> binaryPaths)
    {
        using (progress.Show())
        {
            foreach (var path in binaryPaths)
            {
                if (!Try(out var e, await server.UndoUncommittedFileAsync(path, repoPath)))
                {
                    UI.ErrorMessage($"Failed to undo file:\n{path}\n{e.AllErrorMessages()}");
                }
            }
        }

        RefreshDiff();
    }

    async void UndoFile(string path)
    {
        using (progress.Show())
        {
            if (!Try(out var e, await server.UndoUncommittedFileAsync(path, repoPath)))
            {
                UI.ErrorMessage($"Failed to undo file\n{e.AllErrorMessages()}");
            }
        }

        RefreshDiff();
    }

    async void UndoAll()
    {
        using (progress.Show())
        {
            if (!Try(out var e, await server.UndoAllUncommittedChangesAsync(repoPath)))
            {
                UI.ErrorMessage($"Failed to undo all changes\n{e.AllErrorMessages()}");
            }
        }

        RefreshDiff();
    }

    async void RefreshDiff()
    {
        using (progress.Show())
        {
            if (!Try(out var diff, out var e, await server.GetCommitDiffAsync(commitId, repoPath)))
            {
                UI.ErrorMessage($"Failed to get diff\n{e.AllErrorMessages()}");
            }

            diffs = new[] { diff! };
            diffRows = diffService.ToDiffRows(diff!);
            contentView.TriggerUpdateContent(diffRows.Rows.Count);
        }
    }


    // Move both sides in view left, or select left side text if text is selected
    void OnMoveLeft()
    {
        if (!IsSelectedLeft && contentView.SelectCount > 0)
        {   // Text is selected, lets move selection from right to left side
            IsSelectedLeft = true;
            contentView.SetNeedsDisplay();
            return;
        }

        // Move both sides in view left
        if (rowStartX > 0)
        {
            rowStartX--;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }


    // Move both sides in view right, or select right side text if text is selected
    void OnMoveRight()
    {
        if (IsSelectedLeft && contentView.SelectCount > 0)
        {   // Text is selected, lets move selection from left to right side
            IsSelectedLeft = false;
            contentView.SetNeedsDisplay();
            return;
        }

        // Move both sides in view right
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
        var isHighlighted = contentView.IsRowSelected(index);
        if (row.Mode == DiffRowMode.DividerLine)
        {   // A line in the view, e.g. ━━━━, ══════, that need to be expanded to the full view width
            var line = row.Left.ToLine(viewWidth);
            return isHighlighted ? line.ToHighlight() : line;
        }

        if (row.Mode == DiffRowMode.SpanBoth)
        {   // The left text spans over full width 
            var text = row.Left.Subtext(0, viewWidth);
            return isHighlighted ? text.ToHighlight() : text;
        }

        // The left and right text is shown side by side with a gray vertical line char in between
        var left = (row.Left.Length - rowStartX <= columnWidth || row.Left == DiffService.NoLine) ?
            row.Left.Subtext(rowStartX, columnWidth, true) :
            row.Left.Subtext(rowStartX, columnWidth - 1, true).ToTextBuilder().Add(Text.Dark("…"));

        var right = (row.Right.Length - rowStartX <= columnWidth || row.Right == DiffService.NoLine) ?
            row.Right.Subtext(rowStartX, columnWidth, true) :
            row.Right.Subtext(rowStartX, columnWidth - 1, true).ToTextBuilder().Add(Text.Dark("…"));

        return Text
            .Add(isHighlighted && IsSelectedLeft ? left.ToHighlight() : left)
            .Add(splitLineChar)
            .Add(isHighlighted && !IsSelectedLeft ? right.ToHighlight() : right);
    }


    // Copy selected text to clipboard and clear selection
    void OnCopy()
    {
        if (!IsSelected) return;

        var rows = diffRows.Rows.Skip(contentView.SelectStartIndex).Take(contentView.SelectCount);

        // Convert left or right rows to text, remove empty lines and line numbers
        var text = string.Join("\n", rows
            .Where(r => r.Mode != DiffRowMode.DividerLine)
            .Select(r => IsSelectedLeft || r.Mode != DiffRowMode.SideBySide ? r.Left : r.Right)
            .Where(l => l != DiffService.NoLine)
            .Select(l => l.ToString())
            .Select(t => t.Length > 4 && char.IsNumber(t[3]) ? t.Substring(5) : t)
        );

        if (!Try(out var e, Utils.Clipboard.Set(text)))
        {
            UI.ErrorMessage($"Failed to copy to clipboard\nError: {e}");
        }

        contentView.ClearSelection();
    }
}
