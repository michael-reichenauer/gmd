using gmd.Common;
using gmd.Cui.App;
using gmd.Cui.Common;
using gmd.Cui.Conflict;
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
    // diffs is what source read at the default context. conflicts is what git reports as unmerged,
    // read by the caller — only the uncommitted diff can have any. Passed in rather than read here
    // so it is awaited off the main loop: a Terminal.Gui SynchronizationContext posts an await's
    // continuation to that loop, so blocking it deadlocks.
    DiffResult Show(CommitDiff[] diffs, DiffSource source, string repoPath, ConflictState? conflicts = null);
}

class DiffView : IDiffView
{
    static readonly Text splitLineChar = Text.Dark("│");

    readonly IDiffRowService diffRowService;
    private readonly IProgress progress;
    private readonly IServer server;
    readonly IClipboardService clipboard;
    readonly IConflictView conflictView;
    readonly Config config;
    ContentView contentView = null!;
    KeyHintBar hintBar = null!;
    CommitDiff[] diffs = null!;
    DiffRows diffRows = new DiffRows();
    DiffSource source = null!;

    // The context lines of each file that is not at the default, keyed on path. Only the file the
    // cursor is on is ever stepped, so the others keep whatever they were last shown with.
    readonly Dictionary<string, int> fileContext = [];
    readonly IHelpPage helpPage;
    int rowStartX = 0;
    string repoPath = "";
    bool isFocusLeft = true;
    bool isCommitTriggered = false;
    bool isRefreshNeeded;

    // The search of the lines, f and then n and Shift-N, and the status line of this view, which says
    // what it found, as the log's says what a command did
    RowSearch search = RowSearch.None;
    IStatusLine status = new StatusLine();

    bool IsSelected => contentView.SelectCount > 0;

    // How wide each of the two columns is, as OnGetContent lays them out
    int ColumnWidth => (contentView.ContentWidth + 1 - 2) / 2;

    // The conflicted paths and their kinds, re-read whenever the diff is. Taken from the status
    // rather than from the diff itself, so a conflict git wrote no markers for — a modify/delete,
    // a binary file — is offered like any other; the diff can only see the ones with markers.
    ConflictState conflictState = ConflictState.None;

    public DiffView(
        IDiffRowService diffRowService,
        IProgress progress,
        IServer server,
        IClipboardService clipboard,
        IConflictView conflictView,
        IHelpPage helpPage,
        Config config
    )
    {
        this.helpPage = helpPage;
        this.config = config;
        this.diffRowService = diffRowService;
        this.progress = progress;
        this.server = server;
        this.clipboard = clipboard;
        this.conflictView = conflictView;
    }

    public DiffResult Show(CommitDiff[] diffs, DiffSource source, string repoPath, ConflictState? conflicts = null)
    {
        this.diffs = diffs;
        this.source = source;
        this.rowStartX = 0;
        this.repoPath = repoPath;
        this.isFocusLeft = true;
        this.isCommitTriggered = false;
        this.isRefreshNeeded = false;
        this.fileContext.Clear();
        this.conflictState = conflicts ?? ConflictState.None;
        this.diffRows = diffRowService.ToDiffRows(diffs, fileContext, ConflictPaths());
        this.search = RowSearch.None;
        this.status = new StatusLine();

        Toplevel diffView = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        // The keys worth knowing here on the bottom row, as the log view has them, unless turned off
        var hintsHeight = config.ShowKeyHints ? 1 : 0;
        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(hintsHeight),
            IsShowCursor = false,
            IsScrollMode = false,
            IsCustomShowSelection = true,
        };

        diffView.Add(contentView);
        // The bar is there with the hints off too, for what a search found, as the log has it
        hintBar = new KeyHintBar(
            () =>
                DiffHints.For(
                    this.source.CanCommit,
                    this.source.HasContext,
                    conflictState.Files.Count > 0,
                    search.IsActive
                ),
            () => status.Current
        );
        diffView.Add(hintBar);
        hintBar.FollowStatus(status, config.ShowKeyHints, diffView);
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

        // Letters in both cases, since the menu writes them in upper case. The view is modal (see
        // UI.RunDialog), so a key not registered here does nothing rather than reaching the log view.
        view.RegisterLetterHandler(Key.q, () => Application.RequestStop());
        view.RegisterKeyHandler(Key.CursorLeft, OnMoveLeft);
        view.RegisterKeyHandler(Key.CursorRight, OnMoveRight);
        view.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);
        view.RegisterLetterHandler(Key.m, () => ShowMainMenu());

        view.RegisterLetterHandler(Key.r, () => RefreshDiff());
        // The help, as in every view, at the part about this one
        view.RegisterKeyHandler((Key)'?', () => helpPage.ShowAsync(HelpPage.DiffSection).RunInBackground());
        view.RegisterKeyHandler(Key.F1, () => helpPage.ShowAsync(HelpPage.DiffSection).RunInBackground());
        view.RegisterLetterHandler(Key.s, () => ShowScrollMenu());
        view.RegisterLetterHandler(Key.u, () => ShowUndoMenu());
        view.RegisterLetterHandler(Key.c, () => TriggerCommit());

        // Search the lines with the keys the log searches its commits with. n and Shift-N are one case
        // each, as they are in the log, rather than a letter in both cases.
        view.RegisterLetterHandler(Key.f, Search);
        view.RegisterKeyHandler((Key)'/', Search);
        view.RegisterKeyHandler(Key.n, () => StepMatch(1));
        view.RegisterKeyHandler(Key.N, () => StepMatch(-1));

        // 'Open what the cursor is on', which is what Enter means everywhere else in gmd. Nothing
        // was bound to it in this view before.
        view.RegisterKeyHandler(Key.Enter, ResolveCurrentConflict);

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
            Menu.Items.SubMenu("Scroll to", "s", scrollToItems)
                .Item("Search ...", "f", () => Search())
                .Item("Next Match", "n", () => StepMatch(1), () => search.IsActive, () => NoSearch)
                .Item("Previous Match", "⇧n", () => StepMatch(-1), () => search.IsActive, () => NoSearch)
                .SubMenu("Diff File", "", diffItems)
                .SubMenu("Resolve Conflicts", "Enter", conflictItems)
                .SubMenu("Run External Merge Tool", "", GetMergeToolItems())
                .SubMenu("Discard Changes", "u", undoItems)
                // Refresh knows how to re-fetch whatever kind of diff this is, so it is always valid
                .Item("Refresh", "r", () => RefreshDiff())
                .Item("Commit", "c", () => TriggerCommit(), () => undoItems.Any())
                // Named after the file they act on, since that is the part of these that is not
                // obvious, and disabled when the cursor is on no file or that file is at an end
                .Item(ContextItemText("More Context", 1), "+", () => StepContext(1), () => CanStepContext(1))
                .Item(ContextItemText("Less Context", -1), "-", () => StepContext(-1), () => CanStepContext(-1))
                .Item("Focus Left Column", "←", () => OnMoveLeft(), () => IsSelected)
                .Item("Focus Right Column", "→", () => OnMoveRight(), () => IsSelected)
                .Item("Close", "Esc", () => Application.RequestStop())
        );
    }

    const string NoSearch = "No search to go on with: search with f first";

    // Asks for a text and goes to where it is, from the cursor down, and n and Shift-N then go on
    // from wherever the cursor is, as RowSearch steps
    void Search()
    {
        if (SearchDlg.Show("diff") is not string text)
            return;

        search = FindInRows(text);
        if (!search.IsActive)
        {
            status.Notice($"'{text}' is not in the diff");
            return;
        }

        ShowMatch(search.First(contentView.CurrentIndex));
    }

    void StepMatch(int direction)
    {
        if (!search.IsActive)
        {
            status.Notice(NoSearch);
            return;
        }

        var index = search.Next(contentView.CurrentIndex, direction);
        if (index == -1)
        {
            status.Notice(search.NoMoreText(direction));
            return;
        }

        ShowMatch(index);
    }

    // Each side of a row is searched, and not the two as one line, so a match never runs from the end
    // of the left one into the right one
    RowSearch FindInRows(string text) =>
        RowSearch.Find(
            text,
            diffRows.Count,
            i =>
            {
                var (left, right) = DiffRowService.SearchText(diffRows.Rows[i]);
                return $"{left}\n{right}";
            }
        );

    // The cursor is put on the row, on the side the match is on, the left one when both have it, which
    // is where a copy then takes the lines from too, and the columns are scrolled sideways to show it
    void ShowMatch(int index)
    {
        var row = diffRows.Rows[index];
        if (row.Mode == DiffRowMode.SideBySide)
        {
            var (left, right) = DiffRowService.SearchText(row);
            isFocusLeft = RowSearch.IndexOf(left, search.Text) != -1;
            var (text, lineNbr) = isFocusLeft ? (left, row.LeftLineNbr) : (right, row.RightLineNbr);
            var start = DiffRowService.LineNbrWidth(lineNbr) + RowSearch.IndexOf(text, search.Text);
            rowStartX = RowSearch.ScrollToShow(start, start + search.Text.Length, ColumnWidth);
        }

        contentView.ShowCurrentIndex(index);
        contentView.SetNeedsDisplay();
        status.Info(search.MatchText(index));
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
        Menu.Show("Discard Changes", 1, 2, undoItems);
    }

    // Only the uncommitted changes, as the menu's Commit and the hints say: 'c' in the diff of a
    // commit or a range closed it and opened the commit dialog for the working tree
    void TriggerCommit()
    {
        if (!source.CanCommit)
            return;
        isCommitTriggered = true;
        Application.RequestStop();
    }

    IEnumerable<Common.MenuItem> GetScrollToItems()
    {
        if (diffs.Length > 1)
        {
            return diffs.Select(cd => Menu.Item($"{cd.Time.IsoDate()} {cd.Message}", "", () => ScrollToCommit(cd.Id)));
        }

        var paths = diffRowService.GetDiffFilePaths(diffs[0]);
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

    // Only the uncommitted diff can have conflicts, and only when one is actually in progress
    IReadOnlyList<string> ConflictPaths() => conflictState.Files.Select(c => c.Path).ToList();

    IEnumerable<Common.MenuItem> GetConflictItems() =>
        Menu.Items.Items(
            conflictState.Files.Select(c => Menu.Item(ToConflictItemText(c), "", () => ResolveConflict(c)))
        );

    // Naming the kind is the part that is not obvious: a modify/delete has no text to merge, so
    // picking it opens a keep-or-delete question rather than the three pane view
    static string ToConflictItemText(ConflictedFile file) =>
        file.Kind switch
        {
            ConflictKind.BothModified => file.Path,
            ConflictKind.BothAdded => $"{file.Path}  (added on both sides)",
            ConflictKind.BothDeleted => $"{file.Path}  (deleted on both sides)",
            ConflictKind.AddedByUs => $"{file.Path}  (added by us)",
            ConflictKind.AddedByThem => $"{file.Path}  (added by them)",
            ConflictKind.DeletedByThem => $"{file.Path}  (deleted by them)",
            ConflictKind.DeletedByUs => $"{file.Path}  (deleted by us)",
            _ => file.Path,
        };

    IEnumerable<Common.MenuItem> GetMergeToolItems() =>
        Menu.Items.Items(conflictState.Files.Select(c => Menu.Item(c.Path, "", () => RunMergeTool(c.Path))));

    // The conflicted file the cursor is on, which is what Enter resolves
    ConflictedFile? CurrentConflict()
    {
        var path = CurrentAnchor().FilePath;
        return conflictState.Files.FirstOrDefault(c => c.Path == path);
    }

    // Enter resolves the conflicted file the cursor is on. With the cursor elsewhere — above the
    // first file, or on a file that is not conflicted — it offers the list instead of doing
    // nothing, which would read as a dropped keystroke.
    void ResolveCurrentConflict()
    {
        var conflict = CurrentConflict();
        if (conflict != null)
        {
            ResolveConflict(conflict);
            return;
        }

        if (conflictState.Files.Count > 0)
            Menu.Show("Resolve Conflicts", Menu.Center, 2, GetConflictItems());
    }

    // Loaded here rather than inside the resolver, so the read is awaited off the main loop: a
    // Terminal.Gui SynchronizationContext posts an await's continuation to that loop, so blocking
    // it on a Task deadlocks the application outright
    async void ResolveConflict(ConflictedFile conflict)
    {
        ConflictFile file;
        using (progress.Show())
        {
            var read = await server.GetConflictFileAsync(conflict.Path, conflict.Kind, false, repoPath);
            if (read is not ConflictFile conflictFile)
            {
                UI.ErrorMessage($"Failed to open {conflict.Path}\n{read.Error.AllMessages()}");
                return;
            }
            file = conflictFile;
        }

        conflictView.Show(file, conflictState.Operation, repoPath, RefreshDiff);
    }

    // The cursor is put on the commit or the file, which + and - and Enter act on: scrolled to alone
    // it moved with the rows, and stayed in the file before it
    void ScrollToCommit(string commitId) => ScrollTo(diffRows.Rows.FindIndexBy(r => r.CommitId == commitId));

    void ScrollToFile(string path) => ScrollTo(diffRows.Rows.FindIndexBy(r => r.FilePath == path));

    void ScrollTo(int index)
    {
        if (index == -1)
            return;
        contentView.ScrollToShowIndex(Math.Max(0, index - 1));
        contentView.SetCurrentIndex(index);
    }

    void RunDiffTool(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (await server.RunDiffToolAsync(path, repoPath) is Error e)
                UI.ErrorMessage($"Failed to run diff tool\n{e.AllMessages()}");
        });
    }

    void RunMergeTool(string path)
    {
        UI.RunInBackground(async () =>
        {
            if (await server.RunMergeToolAsync(path, repoPath) is Error e)
                UI.ErrorMessage($"Failed to run merger tool\n{e.AllMessages()}");

            RefreshDiff();
        });
    }

    IEnumerable<Common.MenuItem> GetUndoItems()
    {
        var paths = diffRowService.GetDiffFilePaths(diffs[0]);
        if (!source.CanCommit || paths.Count == 0)
            return new List<Common.MenuItem>();

        var binaryPaths = diffRowService.GetDiffBinaryFilePaths(diffs[0]);

        var addedPaths = diffs[0]
            .FileDiffs.Where(fd => fd.DiffMode == DiffMode.DiffAdded)
            .Select(fd => fd.PathAfter)
            .ToHashSet();
        var undoItems = paths.Select(p => new Common.MenuItem(p, "", () => UndoFile(p, addedPaths.Contains(p))));
        if (undoItems.Count() > 10)
        { // Show files ith sub menu
            undoItems = [new SubMenu("Files", "", undoItems)];
        }

        return Menu
            .Items.Items(undoItems)
            .Separator()
            .Item("Binary Files", "", () => UndoAllBinaryFiles(binaryPaths), () => binaryPaths.Any())
            .Item("All Changes", "", () => UndoAll());
    }

    async void UndoAllBinaryFiles(IReadOnlyList<string> binaryPaths)
    {
        if (!Confirm.UndoFiles(binaryPaths))
            return;

        using (progress.Show())
        {
            foreach (var path in binaryPaths)
            {
                if (await server.UndoUncommittedFileAsync(path, repoPath) is Error e)
                {
                    UI.ErrorMessage($"Failed to undo file:\n{path}\n{e.AllMessages()}");
                }
            }
        }

        RefreshDiff();
    }

    async void UndoFile(string path, bool isNew)
    {
        if (!Confirm.UndoFile(path, isNew))
            return;

        using (progress.Show())
        {
            if (await server.UndoUncommittedFileAsync(path, repoPath) is Error e)
            {
                UI.ErrorMessage($"Failed to undo file\n{e.AllMessages()}");
            }
        }

        RefreshDiff();
    }

    async void UndoAll()
    {
        if (!Confirm.UndoAllUncommitted())
            return;

        using (progress.Show())
        {
            if (await server.UndoAllUncommittedChangesAsync(repoPath) is Error e)
            {
                UI.ErrorMessage($"Failed to undo all changes\n{e.AllMessages()}");
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
        var wanted = WantedContext(anchor.FilePath, step);
        if (wanted == 0)
            return;

        using (progress.Show())
        {
            var fetchedResult = await source.Reload(wanted);
            if (fetchedResult is not CommitDiff[] fetched)
            {
                UI.ErrorMessage($"Failed to get diff\n{fetchedResult.Error.AllMessages()}");
                return;
            }

            fileContext[anchor.FilePath] = wanted;
            SetDiffs(DiffRowService.ReplaceFileDiff(diffs, anchor.FilePath, fetched), anchor);
        }
    }

    // The context the file would then be shown with, or 0 if the step would do nothing: the diff has
    // no context to step, there is no file under the cursor, or that file is already at the widest or
    // the narrowest. Also what the menu asks to know whether to offer the item.
    int WantedContext(string filePath, int step)
    {
        if (!source.HasContext)
            return 0;
        if (filePath == "")
            return 0; // The cursor is above the first file, so there is nothing to step

        var current = fileContext.GetValueOrDefault(filePath, DiffContext.Default);
        var wanted = DiffContext.Step(current, step);

        return wanted == current ? 0 : wanted;
    }

    bool CanStepContext(int step) => WantedContext(CurrentAnchor().FilePath, step) != 0;

    // Names the file the item would act on and what it would then show, since 'the file the cursor
    // is on' is the part of these two commands that the name alone does not give away. Falls back
    // to the bare name when there is nothing to step, where the item is disabled anyway.
    string ContextItemText(string name, int step)
    {
        var path = CurrentAnchor().FilePath;
        var wanted = WantedContext(path, step);
        if (wanted == 0)
            return name;

        return $"{name} of {path.Max(40)} ({DiffContext.ToItemText(wanted)})";
    }

    // Re-fetches the whole diff, e.g. after undoing a file or running the merge tool. Every file
    // goes back to the default context, since the diff itself may now be a different shape.
    async void RefreshDiff()
    {
        var anchor = CurrentAnchor();

        using (progress.Show())
        {
            var refreshedResult = await source.Reload(DiffContext.Default);
            if (refreshedResult is not CommitDiff[] refreshed)
            {
                UI.ErrorMessage($"Failed to get diff\n{refreshedResult.Error.AllMessages()}");
                return;
            }

            // What is still conflicted may have changed, e.g. a file was just resolved, and a stale
            // list would keep heading it 'Conflicts:' and keep offering it in the resolve menu
            if (source.CanCommit)
            {
                if (await server.GetConflictStateAsync(repoPath) is ConflictState state)
                    conflictState = state;
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
        diffRows = diffRowService.ToDiffRows(diffs, fileContext, ConflictPaths());
        search = FindInRows(search.Text); // The rows the search found are other rows now
        hintBar.SetNeedsDisplay(); // A conflict resolved, or one more, changes what Enter offers

        // The old selection covers rows that no longer hold what they did, and a copy would take
        // whatever now sits there
        contentView.ClearSelection();

        RestoreAnchor(anchor);
        contentView.SetNeedsDisplay();
    }

    // Where the cursor is, expressed as content: the commit and the file it is in and the source
    // line it is on
    DiffAnchor CurrentAnchor() => diffRows.AnchorAt(contentView.CurrentIndex);

    // Puts the cursor back where the anchor points, or on the nearest line of that file
    void RestoreAnchor(DiffAnchor anchor)
    {
        var index = diffRows.IndexOf(anchor);
        if (index == -1)
            return;

        contentView.ShowCurrentIndex(index);
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
            var text = Marked(row.Left, row.LineNbr).Subtext(0, viewWidth);
            return isSelectedRow ? text.ToSelect()
                : isCurrentRow ? text.ToHighlight()
                : text;
        }

        // The left and right text is shown side by side with a gray vertical line char in between
        var (leftText, rightText) = (Marked(row.Left, row.LeftLineNbr), Marked(row.Right, row.RightLineNbr));
        var left =
            (row.Left.Length - rowStartX <= columnWidth || row.Left == DiffRowService.NoLine)
                ? row.Left != DiffRowService.NoLine && rowStartX > 0
                    ? Text.Dark("…").Add(leftText.Subtext(rowStartX + 1, columnWidth - 1, true)).ToText()
                    : leftText.Subtext(rowStartX, columnWidth, true)
                : row.Left != DiffRowService.NoLine && rowStartX > 0
                    ? Text.Dark("…")
                        .Add(leftText.Subtext(rowStartX + 1, columnWidth - 2, true).ToTextBuilder().Add(Text.Dark("…")))
                    : leftText.Subtext(rowStartX, columnWidth - 1, true).ToTextBuilder().Add(Text.Dark("…"));

        var right =
            (row.Right.Length - rowStartX <= columnWidth || row.Right == DiffRowService.NoLine)
                ? row.Right != DiffRowService.NoLine && rowStartX > 0
                    ? Text.Dark("…").Add(rightText.Subtext(rowStartX + 1, columnWidth - 1, true)).ToText()
                    : rightText.Subtext(rowStartX, columnWidth, true)
                : row.Right != DiffRowService.NoLine && rowStartX > 0
                    ? Text.Dark("…")
                        .Add(
                            rightText.Subtext(rowStartX + 1, columnWidth - 2, true).ToTextBuilder().Add(Text.Dark("…"))
                        )
                    : rightText.Subtext(rowStartX, columnWidth - 1, true).ToTextBuilder().Add(Text.Dark("…"));

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

    // The text with what the search found marked, after the line number, which is not searched
    Text Marked(Text text, int lineNbr) =>
        !search.IsActive || text == DiffRowService.NoLine
            ? text
            : text.WithMatches(search.Text, DiffRowService.LineNbrWidth(lineNbr));

    // Copy selected text to clipboard and clear selection
    void OnCopy()
    {
        if (!IsSelected)
            return;

        var rows = diffRows.Rows.Skip(contentView.SelectStartIndex).Take(contentView.SelectCount);
        var text = DiffRowService.CopyText(rows, isFocusLeft);

        if (clipboard.Set(text) is Error e)
        {
            UI.ErrorMessage(e.AllMessages());
        }

        contentView.ClearSelection();
    }
}
