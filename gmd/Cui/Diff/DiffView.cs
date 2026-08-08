using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Diff;

enum DiffResult
{
    None,
    Commit,
    Refresh,
}

interface IDiffView
{
    DiffResult Show(CommitDiff diff, string commitId, string repoPath, DiffReload reload);
    void Show(CommitDiff[] diffs, string repoPath, DiffReload reload);
}

class DiffView : IDiffView
{
    static readonly Text splitLineChar = Text.Dark("│");

    readonly IDiffService diffService;
    private readonly IProgress progress;
    private readonly IServer server;
    readonly IClipboardService clipboard;
    ContentView contentView = null!;
    CommitDiff[] diffs = null!;
    DiffRows diffRows = new DiffRows();
    DiffReload reload = null!;

    // The context lines of each file that is not at the default, keyed on path. Only the file the
    // cursor is on is ever stepped, so the others keep whatever they were last shown with.
    readonly Dictionary<string, int> fileContext = [];
    int rowStartX = 0;
    string commitId = "";
    string repoPath = "";
    bool isFocusLeft = true;
    bool isCommitTriggered = false;
    bool isRefreshNeeded;

    bool IsSelected => contentView.SelectCount > 0;

    public DiffView(IDiffService diffService, IProgress progress, IServer server, IClipboardService clipboard)
    {
        this.diffService = diffService;
        this.progress = progress;
        this.server = server;
        this.clipboard = clipboard;
    }

    public DiffResult Show(CommitDiff diff, string commitId, string repoPath, DiffReload reload) =>
        Show([diff], commitId, repoPath, reload);

    public void Show(CommitDiff[] diffs, string repoPath, DiffReload reload) => Show(diffs, "", repoPath, reload);

    DiffResult Show(CommitDiff[] diffs, string commitId, string repoPath, DiffReload reload)
    {
        this.diffs = diffs;
        this.reload = reload;
        this.rowStartX = 0;
        this.commitId = commitId;
        this.repoPath = repoPath;
        this.isFocusLeft = true;
        this.isCommitTriggered = false;
        this.isRefreshNeeded = false;
        this.fileContext.Clear();
        this.diffRows = diffService.ToDiffRows(diffs, fileContext);

        Toplevel diffView = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            IsShowCursor = false,
            IsScrollMode = false,
            IsCursorMargin = false,
            IsCustomShowSelection = true,
        };

        diffView.Add(contentView);
        RegisterShortcuts(contentView);

        contentView.SetNeedsDisplay();
        UI.RunDialog(diffView);
        return isCommitTriggered ? DiffResult.Commit
            : isRefreshNeeded ? DiffResult.Refresh
            : DiffResult.None;
    }

    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.Esc, () => Application.RequestStop());

        // Both cases close the diff. Lower case did already, but only by accident: it is not
        // registered here, so it fell through to the log view's quit handler further down the
        // toplevel chain, which is Application.RequestStop() and so happened to stop this view
        // rather than the application. Registering it makes that intended rather than a side
        // effect of which view the key reached.
        view.RegisterKeyHandler(Key.Q, () => Application.RequestStop());
        view.RegisterKeyHandler(Key.q, () => Application.RequestStop());
        view.RegisterKeyHandler(Key.CursorLeft, OnMoveLeft);
        view.RegisterKeyHandler(Key.CursorRight, OnMoveRight);
        view.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);
        view.RegisterKeyHandler(Key.m, () => ShowMainMenu());

        view.RegisterKeyHandler(Key.r, () => RefreshDiff());
        view.RegisterKeyHandler(Key.d, () => RefreshDiff());
        view.RegisterKeyHandler(Key.s, () => ShowScrollMenu());
        view.RegisterKeyHandler(Key.u, () => ShowUndoMenu());
        view.RegisterKeyHandler(Key.c, () => TriggerCommit());

        // More/less context around the changes of the file the cursor is on. Terminal.Gui 1.x has
        // no Key value for these, so use the ascii code, as the '?' key does in RepoViewInput.
        // '=' is '+' without the shift, which is where the key is on most layouts.
        view.RegisterKeyHandler((Key)43, () => StepContext(1)); // '+' key
        view.RegisterKeyHandler((Key)61, () => StepContext(1)); // '=' key
        view.RegisterKeyHandler((Key)45, () => StepContext(-1)); // '-' key

        view.RegisterMouseHandler(MouseFlags.Button1Pressed, (x, y) => OnMouseClick(x, y));
        view.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => ShowMainMenu(x - 1, y - 1));
    }

    private bool OnMouseClick(int x, int y)
    {
        // Switch focus sides if clicking on other side
        int columnWidth = (contentView.ContentWidth - 2) / 2;
        if (x > columnWidth && isFocusLeft)
            isFocusLeft = false;
        if (x < columnWidth && !isFocusLeft)
            isFocusLeft = true;

        contentView.SetIndexAtViewY(y);

        contentView.SetNeedsDisplay();
        return false;
    }

    void ShowMainMenu(int x = Menu.Center, int y = 0)
    {
        var undoItems = GetUndoItems();
        var scrollToItems = GetScrollToItems();
        var diffItems = GetDiffItems();
        var conflictItems = GetConflictItems();

        Menu.Show(
            "Diff Menu",
            x,
            y,
            Menu.Items.SubMenu("Scroll to", "S", scrollToItems)
                .SubMenu("Diff File", "", diffItems)
                .SubMenu("Merge Conflict File", "", conflictItems)
                .SubMenu("Undo/Restore Uncommitted", "U", undoItems)
                // Refresh knows how to re-fetch whatever kind of diff this is, so it is always valid
                .Item("Refresh", "R", () => RefreshDiff())
                .Item("Commit", "C", () => TriggerCommit(), () => undoItems.Any())
                .Item("Focus Left Column", "←", () => OnMoveLeft(), () => IsSelected)
                .Item("Focus Right Column", "→", () => OnMoveRight(), () => IsSelected)
                .Item("Close", "Esc", () => Application.RequestStop())
        );
    }

    void ShowScrollMenu()
    {
        var scrollToItems = GetScrollToItems();
        if (!scrollToItems.Any())
            return;

        Menu.Show("Scroll to", 1, 2, scrollToItems);
    }

    void ShowUndoMenu()
    {
        var undoItems = GetUndoItems();
        if (!undoItems.Any())
            return;
        Menu.Show("Undo/Restore Uncommitted", 1, 2, undoItems);
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
            return diffs.Select(cd => Menu.Item($"{cd.Time.IsoDate()} {cd.Message}", "", () => ScrollToCommit(cd.Id)));
        }

        var paths = diffService.GetDiffFilePaths(diffs[0]);
        if (!paths.Any())
            return Menu.Items;

        return Menu.Items.Items(paths.Select(p => Menu.Item(p, "", () => ScrollToFile(p))));
    }

    IEnumerable<Common.MenuItem> GetDiffItems()
    {
        if (diffs.Length > 1 || diffs[0].Id != "")
            return Menu.Items;

        var paths = diffs[0].FileDiffs.Select(fd => fd.PathAfter).ToList();
        if (!paths.Any())
            return Menu.Items;

        return Menu.Items.Items(paths.Select(p => Menu.Item(p, "", () => RunDiffTool(p))));
    }

    IEnumerable<Common.MenuItem> GetConflictItems()
    {
        if (diffs.Length > 1)
            return Menu.Items;

        var paths = diffs[0]
            .FileDiffs.Where(fd => fd.DiffMode == DiffMode.DiffConflicts)
            .Select(fd => fd.PathAfter)
            .ToList();
        if (!paths.Any())
            return Menu.Items;

        return Menu.Items.Items(paths.Select(p => Menu.Item(p, "", () => RunMergeTool(p))));
    }

    void ScrollToCommit(string commitId)
    {
        var lineIndex = diffRows.Rows.FindIndexBy(r => r.CommitId == commitId);
        contentView.ScrollToShowIndex(lineIndex - 1);
    }

    void ScrollToFile(string path)
    {
        // Find the row indexes where the file diff starts
        var lineIndex = diffRows.Rows.FindIndexBy(r => r.FilePath == path);
        contentView.ScrollToShowIndex(lineIndex - 1);
    }

    void RunDiffTool(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (!Try(out var e, await server.RunDiffToolAsync(path, repoPath)))
                UI.ErrorMessage($"Failed to run diff tool\n{e.AllErrorMessages()}");
        });
    }

    void RunMergeTool(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (!Try(out var e, await server.RunMergeToolAsync(path, repoPath)))
                UI.ErrorMessage($"Failed to run merger tool\n{e.AllErrorMessages()}");

            RefreshDiff();
        });
    }

    IEnumerable<Common.MenuItem> GetUndoItems()
    {
        var paths = diffService.GetDiffFilePaths(diffs[0]);
        if (commitId != Repo.UncommittedId || paths.Count == 0)
            return new List<Common.MenuItem>();

        var binaryPaths = diffService.GetDiffBinaryFilePaths(diffs[0]);

        var undoItems = paths.Select(p => new Common.MenuItem(p, "", () => UndoFile(p)));
        if (undoItems.Count() > 10)
        { // Show files ith sub menu
            undoItems = new[] { new SubMenu("Uncommitted Files", "", undoItems) };
        }

        return Menu
            .Items.Items(undoItems)
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

    // Shows the file the cursor is on with more (step 1) or less (step -1) context around its
    // changes. Git only has whole-diff options, so the diff is re-fetched at the new context and
    // just this one file taken from it, leaving the other files at whatever they were shown with.
    async void StepContext(int step)
    {
        var anchor = CurrentAnchor();
        if (anchor.FilePath == "")
            return; // The cursor is above the first file, so there is nothing to widen

        var current = fileContext.GetValueOrDefault(anchor.FilePath, DiffContext.Default);
        var wanted = DiffContext.Step(current, step);
        if (wanted == current)
            return; // Already at the widest or the narrowest

        using (progress.Show())
        {
            if (!Try(out var fetched, out var e, await reload(wanted)))
            {
                UI.ErrorMessage($"Failed to get diff\n{e.AllErrorMessages()}");
                return;
            }

            fileContext[anchor.FilePath] = wanted;
            SetDiffs(DiffService.ReplaceFileDiff(diffs, anchor.FilePath, fetched), anchor);
        }
    }

    // Re-fetches the whole diff, e.g. after undoing a file or running the merge tool. Every file
    // goes back to the default context, since the diff itself may now be a different shape.
    async void RefreshDiff()
    {
        var anchor = CurrentAnchor();

        using (progress.Show())
        {
            if (!Try(out var refreshed, out var e, await reload(DiffContext.Default)))
            {
                UI.ErrorMessage($"Failed to get diff\n{e.AllErrorMessages()}");
                return;
            }

            fileContext.Clear();
            SetDiffs(refreshed, anchor);
        }

        isRefreshNeeded = true;
    }

    // Replaces the shown diffs and redraws, keeping the cursor on the content it was on rather
    // than on the row index it was at, which means nothing once the rows are rebuilt.
    void SetDiffs(CommitDiff[] newDiffs, DiffAnchor anchor)
    {
        diffs = newDiffs;
        diffRows = diffService.ToDiffRows(diffs, fileContext);

        // The old selection covers rows that no longer hold what they did, and a copy would take
        // whatever now sits there
        contentView.ClearSelection();

        RestoreAnchor(anchor);
        contentView.SetNeedsDisplay();
    }

    // Where the cursor is, expressed as content: the file it is in and the source line it is on
    DiffAnchor CurrentAnchor()
    {
        var rows = diffRows.Rows;
        var index = contentView.CurrentIndex;
        if (index < 0 || index >= rows.Count)
            return DiffAnchor.None;

        return new DiffAnchor(FilePathAt(rows, index), rows[index].LineNbr);
    }

    // Puts the cursor back where the anchor points, or on the nearest line of that file
    void RestoreAnchor(DiffAnchor anchor)
    {
        if (anchor.FilePath == "")
            return;

        var rows = diffRows.Rows;
        var fileIndex = rows.FindIndexBy(r => r.FilePath == anchor.FilePath);
        if (fileIndex == -1)
            return;

        var index = DiffService.FindClosestLineIndex(rows, fileIndex, anchor.LineNbr);

        // Scroll first and set the cursor after: scrolling moves the cursor by the same amount as
        // the rows, so setting it first would leave it somewhere else entirely
        contentView.ScrollToShowIndex(index);
        contentView.SetCurrentIndex(index);
    }

    // The file the row is part of, i.e. the closest file header at or above it ("" above them all)
    static string FilePathAt(IReadOnlyList<DiffRow> rows, int index)
    {
        for (int i = index; i >= 0; i--)
        {
            if (rows[i].FilePath != "")
                return rows[i].FilePath;
        }

        return "";
    }

    // Move both sides in view left, or select left side text if text is selected
    void OnMoveLeft()
    {
        if (!isFocusLeft)
        { // Text is selected, lets move selection from right to left side
            isFocusLeft = true;
            contentView.SetNeedsDisplay();
            return;
        }

        // Move both sides in view left
        if (rowStartX > 0)
        {
            rowStartX--;
            contentView.SetNeedsDisplay();
        }
    }

    // Move both sides in view right, or select right side text if text is selected
    void OnMoveRight()
    {
        if (isFocusLeft)
        { // Text is selected, lets move selection from left to right side
            isFocusLeft = false;
            contentView.SetNeedsDisplay();
            return;
        }

        // Move both sides in view right
        int maxColumnWidth = contentView!.ContentWidth / 2;
        if (diffRows!.MaxLength - rowStartX > maxColumnWidth)
        {
            rowStartX++;
            contentView.SetNeedsDisplay();
        }
    }

    // Returns the content for the view
    (IEnumerable<Text> rows, int total) OnGetContent(int firstRow, int rowCount, int currentIndex, int contentWidth)
    {
        contentWidth++;
        int columnWidth = (contentWidth - 2) / 2;
        int viewWidth = columnWidth * 2 + 1;

        var rows = diffRows
            .Rows.Skip(firstRow)
            .Take(rowCount)
            .Select((r, i) => ToDiffRowText(r, i + firstRow, columnWidth, currentIndex, viewWidth));
        return (rows, diffRows.Rows.Count);
    }

    // Returns a row with either a line, a span or two columns of side by side text
    Text ToDiffRowText(DiffRow row, int index, int columnWidth, int currentIndex, int viewWidth)
    {
        var isSelectedRow = contentView.IsRowSelected(index);
        var isCurrentRow = !isSelectedRow && index == currentIndex;
        if (row.Mode == DiffRowMode.DividerLine)
        { // A line in the view, e.g. ━━━━, ══════, that need to be expanded to the full view width
            var line = row.Left.ToLine(viewWidth);
            if (isCurrentRow)
                line = line.ToHighlight();
            return line;
        }

        if (row.Mode == DiffRowMode.SpanBoth)
        { // The left text spans over full width
            var text = row.Left.Subtext(0, viewWidth);
            return isSelectedRow ? text.ToSelect()
                : isCurrentRow ? text.ToHighlight()
                : text;
        }

        // The left and right text is shown side by side with a gray vertical line char in between
        var left =
            (row.Left.Length - rowStartX <= columnWidth || row.Left == DiffService.NoLine)
                ? row.Left != DiffService.NoLine && rowStartX > 0
                    ? Text.Dark("…").Add(row.Left.Subtext(rowStartX + 1, columnWidth - 1, true)).ToText()
                    : row.Left.Subtext(rowStartX, columnWidth, true)
                : row.Left != DiffService.NoLine && rowStartX > 0
                    ? Text.Dark("…")
                        .Add(row.Left.Subtext(rowStartX + 1, columnWidth - 2, true).ToTextBuilder().Add(Text.Dark("…")))
                    : row.Left.Subtext(rowStartX, columnWidth - 1, true).ToTextBuilder().Add(Text.Dark("…"));

        var right =
            (row.Right.Length - rowStartX <= columnWidth || row.Right == DiffService.NoLine)
                ? row.Right != DiffService.NoLine && rowStartX > 0
                    ? Text.Dark("…").Add(row.Right.Subtext(rowStartX + 1, columnWidth - 1, true)).ToText()
                    : row.Right.Subtext(rowStartX, columnWidth, true)
                : row.Right != DiffService.NoLine && rowStartX > 0
                    ? Text.Dark("…")
                        .Add(
                            row.Right.Subtext(rowStartX + 1, columnWidth - 2, true).ToTextBuilder().Add(Text.Dark("…"))
                        )
                    : row.Right.Subtext(rowStartX, columnWidth - 1, true).ToTextBuilder().Add(Text.Dark("…"));

        return Text.Add(
                isSelectedRow && isFocusLeft ? left.ToSelect()
                : isCurrentRow && isFocusLeft ? left.ToHighlight()
                : left
            )
            .Add(splitLineChar)
            .Add(
                isSelectedRow && !isFocusLeft ? right.ToSelect()
                : isCurrentRow && !isFocusLeft ? right.ToHighlight()
                : right
            );
    }

    // Copy selected text to clipboard and clear selection
    void OnCopy()
    {
        if (!IsSelected)
            return;

        var rows = diffRows.Rows.Skip(contentView.SelectStartIndex).Take(contentView.SelectCount);

        // Convert left or right rows to text, remove empty lines and the line number gutter
        var text = string.Join(
            "\n",
            rows.Where(r => r.Mode != DiffRowMode.DividerLine)
                .Select(r =>
                {
                    var isLeft = isFocusLeft || r.Mode != DiffRowMode.SideBySide;
                    return (Text: isLeft ? r.Left : r.Right, LineNbr: isLeft ? r.LeftLineNbr : r.RightLineNbr);
                })
                .Where(r => r.Text != DiffService.NoLine)
                .Select(r => DiffService.WithoutLineNbr(r.Text.ToString(), r.LineNbr))
        );

        if (!Try(out var e, clipboard.Set(text)))
        {
            UI.ErrorMessage(e.AllErrorMessages());
        }

        contentView.ClearSelection();
    }
}
