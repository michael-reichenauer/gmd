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
    Continue, // The operation, its conflicts resolved, as the resolver asked
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
    readonly IConflictWorkspace conflictWorkspace;
    readonly Config config;
    readonly LeftOutLines leftOut;

    // The left-out lines of each file not found in the diff since it was read again, which stop a
    // commit until found again or put back (LeftOutLines.Refind)
    readonly Dictionary<string, int> notFound = [];
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
    bool isContinueTriggered;
    bool isRefreshNeeded;

    // The search of the lines, f and then n and Shift-N, and the status line of this view, which says
    // what it found, as the log's says what a command did
    RowSearch search = RowSearch.None;
    IStatusLine status = new StatusLine();

    // Whether changes of whitespace alone are hidden, 'w', for this diff: a diff opened later shows
    // them again, rather than hide a change where it was not asked for
    bool isIgnoreWhitespace;

    // Whether the rows are in one column, a change's removed lines above its added ones, which a
    // narrow view is, or as v chose for this diff, null while the width chooses
    bool isOneColumn;
    bool? isOneColumnChosen;
    bool isLayoutPosted;

    // A view narrower than this has too little of a line in each of two columns
    const int OneColumnBelow = 110;

    bool IsOneColumnFor(int contentWidth) => isOneColumnChosen ?? contentWidth < OneColumnBelow;

    // The list of files beside a wide diff, or of the commits of a file history, how wide it is, 0 when
    // it is not shown, and whether it has the keys, as Tab moves them there and back; whether the rows
    // were made with it beside them, which leaves out their own list of them at the top
    bool isRowsBesideList;
    ContentView listView = null!;
    DiffFileList fileList = DiffFileList.None;

    // The files whose lines are hidden, h, which are drawn as their header alone, for this diff
    readonly HashSet<string> hiddenFiles = [];
    int listWidth;
    bool isListFocus;

    // Whether the list is wanted, as l last left it, read from the config as the diff opens. Kept
    // here and saved, not read from the config, which another gmd can change under it, and which a
    // save of any setting copies in whole: l flipped what the config said rather than what was shown.
    bool isListWanted;

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
        IConflictWorkspace conflictWorkspace,
        IHelpPage helpPage,
        Config config,
        LeftOutLines leftOut
    )
    {
        this.helpPage = helpPage;
        this.config = config;
        this.leftOut = leftOut;
        this.diffRowService = diffRowService;
        this.progress = progress;
        this.server = server;
        this.clipboard = clipboard;
        this.conflictWorkspace = conflictWorkspace;
    }

    public DiffResult Show(CommitDiff[] diffs, DiffSource source, string repoPath, ConflictState? conflicts = null)
    {
        this.diffs = diffs;
        this.source = source;
        this.rowStartX = 0;
        this.repoPath = repoPath;
        this.isFocusLeft = true;
        this.isCommitTriggered = false;
        this.isContinueTriggered = false;
        this.isRefreshNeeded = false;
        this.fileContext.Clear();
        this.conflictState = conflicts ?? ConflictState.None;
        this.isOneColumnChosen = null;
        this.hiddenFiles.Clear();
        this.isOneColumn = IsOneColumnFor(Application.Driver.Cols - 1); // The view's width, but for its scrollbar
        this.diffRows = diffRowService.ToDiffRows(diffs, fileContext, ConflictPaths(), false, isOneColumn);
        this.fileList = DiffFileList.Of(diffs, diffRows);
        this.isListWanted = config.ShowDiffFileList;
        this.listWidth = ListWidth();
        this.isListFocus = false;
        if (listWidth > 0)
        { // Narrowed by the list, and the files or commits listed in it rather than above the diff
            isOneColumn = IsOneColumnFor(Application.Driver.Cols - listWidth - 1);
            diffRows = diffRowService.ToDiffRows(diffs, fileContext, ConflictPaths(), false, isOneColumn, null, true);
            fileList = DiffFileList.Of(diffs, diffRows);
        }
        this.isRowsBesideList = listWidth > 0;
        this.search = RowSearch.None;
        this.status = new StatusLine();
        this.isIgnoreWhitespace = false;
        leftOut.ForRepo(repoPath);
        RefindLeftOutLines();

        Toplevel diffView = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        // The keys worth knowing here on the bottom row, as the log view has them, unless turned off
        var hintsHeight = config.ShowKeyHints ? 1 : 0;
        // Never focused, so the keys stay with the diff, which hands them on while the list has them
        listView = new ContentView(OnGetListContent)
        {
            X = 0,
            Y = 0,
            Width = listWidth,
            Height = Dim.Fill(hintsHeight),
            IsShowCursor = false,
            IsScrollMode = false,
            IsFocus = false,
            CanFocus = false,
        };
        listView.RegisterMouseHandler(MouseFlags.Button1Pressed, (_, y) => OnListClick(y));
        contentView = new ContentView(OnGetContent)
        {
            X = listWidth,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(hintsHeight),
            IsShowCursor = false,
            IsScrollMode = false,
            IsCustomShowSelection = true,
            TopRow = TopRowText,
        };

        diffView.Add(listView, contentView);
        // The bar is there with the hints off too, for what a search found, as the log has it
        hintBar = new KeyHintBar(
            () =>
                DiffHints.For(
                    this.source.CanCommit,
                    this.source.HasContext,
                    conflictState.Files.Count > 0,
                    search.IsActive,
                    isIgnoreWhitespace,
                    LeaveOutHint(),
                    isOneColumn,
                    listWidth > 0,
                    IsListHidden
                ),
            () => status.Current
        );
        diffView.Add(hintBar);
        hintBar.FollowStatus(status, config.ShowKeyHints, diffView);
        contentView.CurrentIndexChange += () => hintBar.SetNeedsDisplay(); // x leaves out or puts back
        contentView.CurrentIndexChange += FollowCursorInList;
        RegisterShortcuts(contentView);

        contentView.SetNeedsDisplay();
        using (Menu.SayingWhyNotOn(status))
            UI.RunDialog(diffView);
        return isCommitTriggered ? DiffResult.Commit
            : isContinueTriggered ? DiffResult.Continue
            : isRefreshNeeded ? DiffResult.Refresh
            : DiffResult.None;
    }

    void RegisterShortcuts(ContentView view)
    {
        // Esc leaves the file list for the diff, and closes the diff
        view.RegisterKeyHandler(
            Key.Esc,
            () =>
            {
                if (isListFocus)
                    ToggleListFocus();
                else
                    Application.RequestStop();
            }
        );

        // Tab moves the keys to the list of files beside the diff and back, and while the list has
        // them, up and down go to the file above and below, which the diff shows as they do
        view.RegisterKeyHandler(Key.Tab, ToggleListFocus);
        view.RegisterKeyHandler(Key.CursorUp, () => MoveInList(-1));
        view.RegisterKeyHandler(Key.CursorDown, () => MoveInList(1));
        view.RegisterKeyHandler(Key.PageUp, () => MoveInList(-ListPage));
        view.RegisterKeyHandler(Key.PageDown, () => MoveInList(ListPage));
        view.RegisterKeyHandler(Key.Home, () => MoveInList(-fileList.Items.Count));
        view.RegisterKeyHandler(Key.End, () => MoveInList(fileList.Items.Count));

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
        view.RegisterLetterHandler(Key.w, ToggleWhitespace);
        view.RegisterLetterHandler(Key.v, ToggleLayout);
        view.RegisterLetterHandler(Key.h, ToggleHidden);
        view.RegisterLetterHandler(Key.l, ToggleList);
        view.RegisterLetterHandler(Key.x, ToggleLeftOut);

        // The next and previous change, as the resolver's ] and [ go to a conflict, and file. Terminal.Gui
        // 1.x has no Key value for these, so use the ascii code, as for '+' and '-' below.
        view.RegisterKeyHandler((Key)93, () => GoToChange(1)); // ']'
        view.RegisterKeyHandler((Key)91, () => GoToChange(-1)); // '['
        view.RegisterKeyHandler((Key)125, () => GoToFile(1)); // '}'
        view.RegisterKeyHandler((Key)123, () => GoToFile(-1)); // '{'

        // 'Open what the cursor is on', which is what Enter means everywhere else in gmd. Nothing
        // was bound to it in this view before.
        view.RegisterKeyHandler(
            Key.Enter,
            () =>
            {
                if (isListFocus)
                    ToggleListFocus();
                else
                    ResolveCurrentConflict();
            }
        );

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
        // A click in the diff gives it the keys back from the list of files, as a click on a side is
        // where the keys then act
        if (isListFocus)
            ToggleListFocus();

        if (y < contentView.ContentY)
            return true; // The top row, which is no row of the diff

        // Switch focus sides if clicking on other side
        int columnWidth = (contentView.ContentWidth - 2) / 2;
        if (x > columnWidth && isFocusLeft && !isOneColumn)
            isFocusLeft = false;
        if (x < columnWidth && !isFocusLeft && !isOneColumn)
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
            Menu.Items.SubMenu("Go to", "s", scrollToItems)
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
                .Item(
                    IsLeavingOut() ? "Leave Out of Commit" : "Put Back in Commit",
                    "x",
                    ToggleLeftOut,
                    () => WhyNotLeaveOut(LinesToLeaveOut()) == "",
                    () => WhyNotLeaveOut(LinesToLeaveOut())
                )
                // Named after the file they act on, since that is the part of these that is not
                // obvious, and disabled when the cursor is on no file or that file is at an end
                .Item(
                    ContextItemText("More Context", 1),
                    "+",
                    () => StepContext(1),
                    () => CanStepContext(1),
                    () => WhyNotStepContext(CurrentAnchor(), 1)
                )
                .Item(
                    ContextItemText("Less Context", -1),
                    "-",
                    () => StepContext(-1),
                    () => CanStepContext(-1),
                    () => WhyNotStepContext(CurrentAnchor(), -1)
                )
                .Item(isIgnoreWhitespace ? "Show Whitespace Changes" : "Hide Whitespace Changes", "w", ToggleWhitespace)
                .Item(isOneColumn ? "Show Side by Side" : "Show in One Column", "v", ToggleLayout)
                .Item(HiddenItemText(), "h", ToggleHidden, () => CurrentAnchor().FilePath != "", () => NoFile)
                .Item("Focus Left Column", "←", () => OnMoveLeft(), () => IsSelected && !isOneColumn)
                .Item("Focus Right Column", "→", () => OnMoveRight(), () => IsSelected && !isOneColumn)
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
            status.Notice($"'{text}' is not in {(hiddenFiles.Count > 0 ? "the lines shown" : "the diff")}");
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
            var start = RowSearch.IndexOf(text, search.Text);
            var width = ColumnWidth - DiffRowService.LineNbrWidth(lineNbr); // The gutter does not scroll
            rowStartX = RowSearch.ScrollToShow(start, start + search.Text.Length, width);
        }
        else if (row.LineNbr != 0)
        { // A line across the view, in one column
            var start = RowSearch.IndexOf(DiffRowService.SearchText(row).Left, search.Text);
            var width = ViewWidth - DiffRowService.LineNbrWidth(row.LineNbr);
            rowStartX = RowSearch.ScrollToShow(start, start + search.Text.Length, width);
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

        Menu.Show("Go to", 1, 2, scrollToItems);
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

    // The next and previous change and file, and then each commit of a file history, or each file
    IEnumerable<Common.MenuItem> GetScrollToItems()
    {
        var moves = Menu
            .Items.Item("Next Change", "]", () => GoToChange(1), () => CanGo(1, true), () => NoMore(1, true))
            .Item("Previous Change", "[", () => GoToChange(-1), () => CanGo(-1, true), () => NoMore(-1, true))
            .Item("Next File", "}", () => GoToFile(1), () => CanGo(1, false), () => NoMore(1, false))
            .Item("Previous File", "{", () => GoToFile(-1), () => CanGo(-1, false), () => NoMore(-1, false))
            .Item(
                isListFocus ? "Back to the Diff" : "File List",
                "Tab",
                ToggleListFocus,
                () => listWidth > 0,
                WhyNoList
            )
            .Item(
                listWidth > 0 ? "Hide File List" : "Show File List",
                "l",
                ToggleList,
                () => fileList.WidthFor(Application.Driver.Cols) > 0,
                WhyNoList
            )
            .Separator();

        if (diffs.Length > 1)
        {
            return moves.Items(
                diffs.Select(cd => Menu.Item($"{cd.Time.IsoDate()} {cd.Message}", "", () => ScrollToCommit(cd.Id)))
            );
        }

        var paths = diffRowService.GetDiffFilePaths(diffs[0]);
        return moves.Items(paths.Select(p => Menu.Item(p, "", () => ScrollToFile(p))));
    }

    // Goes to where the next or the previous change starts, ] and [, a few rows below the top of the
    // view when it is not in sight, and says which of how many it is
    void GoToChange(int step)
    {
        var index = diffRows.NextChange(contentView.CurrentIndex, step);
        if (index == -1)
        {
            status.Notice(NoMore(step, true));
            return;
        }

        contentView.ClearSelection();
        contentView.ShowCurrentIndex(index);
        var (number, count) = diffRows.ChangeNumber(index);
        status.Info($"Change {number} of {count}");
    }

    // Goes to the header of the next or the previous file, } and {, at the top of the view
    void GoToFile(int step)
    {
        var index = diffRows.NextFile(contentView.CurrentIndex, step);
        if (index == -1)
        {
            status.Notice(NoMore(step, false));
            return;
        }

        contentView.ClearSelection();
        ShowAtTop(index);
        var (number, count) = diffRows.FileNumber(index);
        status.Info($"File {number} of {count}: {diffRows.Rows[index].FilePath}");
    }

    bool CanGo(int step, bool isChange) =>
        (
            isChange
                ? diffRows.NextChange(contentView.CurrentIndex, step)
                : diffRows.NextFile(contentView.CurrentIndex, step)
        ) != -1;

    // Why there is nowhere to go, and the key that goes the other way
    static string NoMore(int step, bool isChange) =>
        (isChange, step > 0) switch
        {
            (true, true) => "No more changes below: [ goes up",
            (true, false) => "No more changes above: ] goes down",
            (false, true) => "No more files below: { goes up",
            (false, false) => "No more files above: } goes down",
        };

    // Moves the keys to the list of files and back, and away from it when it is hidden
    void ToggleListFocus()
    {
        if (listWidth == 0 && !isListFocus)
        {
            status.Notice(WhyNoList());
            return;
        }

        isListFocus = !isListFocus;
        listView.IsFocus = isListFocus;
        contentView.IsFocus = !isListFocus;
        listView.SetNeedsDisplay();
        contentView.SetNeedsDisplay();
    }

    // Shows or hides the list of files, l. Remembered for the diffs after, since it is hidden for the
    // width it takes, which is as wanted in the next diff as in this one
    void ToggleList()
    {
        if (fileList.WidthFor(Application.Driver.Cols) == 0)
        {
            status.Notice(WhyNoList());
            return;
        }

        isListWanted = !isListWanted;
        var isWanted = isListWanted;
        config.Set(c => c.ShowDiffFileList = isWanted);
        SetListWidth(ListWidth());
        SetLayout(IsOneColumnFor(Application.Driver.Cols - listWidth - 1));
        FollowCursorInList();
        hintBar.SetNeedsDisplay();
        status.Info(listWidth > 0 ? "File list shown: Tab moves into it, l hides it" : "File list hidden: l shows it");
    }

    // How wide the list of files is: as wide as it can be at the terminal's width, unless hidden
    int ListWidth() => isListWanted ? fileList.WidthFor(Application.Driver.Cols) : 0;

    // Hidden by l, where it would be shown, which the hints then say l shows
    bool IsListHidden => listWidth == 0 && fileList.WidthFor(Application.Driver.Cols) > 0;

    string WhyNoList() =>
        IsListHidden ? "The file list is hidden: l shows it"
        : fileList.Items.Count == 0 ? "The diff has no files to list"
        : $"The file list is beside a diff on a terminal {DiffFileList.MinTerminalWidth} columns wide or more";

    // Goes to the file a step above or below in the list, while it has the keys, true then; with the
    // diff having them the key is left to it
    bool MoveInList(int step)
    {
        if (!isListFocus || fileList.Items.Count == 0)
            return false;

        var current = fileList.IndexAt(contentView.CurrentIndex);
        var index = Math.Clamp(current == -1 && step > 0 ? step - 1 : current + step, 0, fileList.Items.Count - 1);
        ShowAtTop(fileList.Items[index].Row);
        return true;
    }

    int ListPage => Math.Max(1, listView.ContentHeight - 2);

    // The list's title is its first row, the files after it
    void OnListClick(int y)
    {
        var index = listView.FirstIndex + y - 1;
        if (index >= 0 && index < fileList.Items.Count)
            ShowAtTop(fileList.Items[index].Row);
    }

    // The list keeps the file the diff's cursor is in, which it marks, in sight
    void FollowCursorInList()
    {
        if (listWidth == 0)
            return;

        listView.ScrollToShowIndex(fileList.IndexAt(contentView.CurrentIndex) + 1, 1);
        listView.SetNeedsDisplay();
    }

    (IEnumerable<Text> rows, int total) OnGetListContent(int firstRow, int rowCount, int _, int contentWidth)
    {
        var current = fileList.IndexAt(contentView.CurrentIndex);
        var rows = fileList.Rows(
            contentWidth,
            current,
            isListFocus,
            path => source.CanCommit ? leftOut.CountOf(path) : 0
        );
        return (rows.Skip(firstRow).Take(rowCount), rows.Count);
    }

    // The top row: the commit the diff is of, and once its header has scrolled out of sight, the file
    // the top of the view is in, so that a line deep in a long diff still says where it is, e.g.
    // 'f44b73c6  VAT rate from config › M Pricing.cs  +5 -5'
    Text TopRowText(int firstRow, int width)
    {
        var where = new TextBuilder();
        var file = diffRows.FileHeaderOf(firstRow);
        if (file != -1 && file < firstRow)
        { // The file's header without its bar, e.g. 'M Pricing.cs  +5 -5'
            var header = diffRows.Rows[file].Left;
            where.Dark(" › ").Add(header.Subtext(2, Math.Max(0, header.Length - 2)));
        }

        // The commit's subject is cut short first, since where in the commit the view is, is what
        // changes as it scrolls
        var title = TitleAt(firstRow);
        var titleWidth = Math.Max(Math.Min(title.Length, MinTitleWidth), width - where.Length - 2);
        var text =
            title.Length > titleWidth
                ? title.Subtext(0, titleWidth - 1).ToTextBuilder().Dark("…")
                : title.ToTextBuilder();
        text.Add(title.Length > 0 ? where : where.ToText().Subtext(3, Math.Max(0, where.Length - 3)));

        // The line in the color of the log's under its application bar and of the key hints' below
        return text.Length + 1 >= width
            ? text
            : text.Dark(" ").Add(Text.BrightMagenta("─").ToText().ToLine(width - text.Length - 1));
    }

    // As short as the commit's id and subject are cut, to make room for the file
    const int MinTitleWidth = 20;

    // The commit at the top of the view: the diff's own, or in a file history the one the top of the
    // view is in, and above them all how many there are
    Text TitleAt(int firstRow)
    {
        if (diffs.Length == 1)
            return DiffRowService.TitleOf(diffs[0]);

        var id = diffRows.AnchorAt(firstRow).CommitId;
        return id != "" && diffs.FirstOrDefault(d => d.Id == id) is CommitDiff diff
            ? DiffRowService.TitleOf(diff)
            : Text.White($"{diffs.Length} commits");
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

    // Resolves the conflicts in the resolver (ConflictWorkspace), from the file, and the other
    // conflicted files after it, and finishes the operation once none is left, if asked to: the diff
    // closes for the commit or the continue, which the log does
    async void ResolveConflict(ConflictedFile conflict)
    {
        var next = await conflictWorkspace.ResolveAsync(repoPath, conflict.Path);
        if (next == ResolveNext.Commit)
        {
            isCommitTriggered = true;
            Application.RequestStop();
        }
        else if (next == ResolveNext.Continue)
        {
            isContinueTriggered = true;
            Application.RequestStop();
        }
        else
        {
            RefreshDiff();
        }
    }

    // The cursor is put on the commit or the file, which + and - and Enter act on: scrolled to alone
    // it moved with the rows, and stayed in the file before it
    void ScrollToCommit(string commitId) => ScrollTo(diffRows.Rows.FindIndexBy(r => r.CommitId == commitId));

    void ScrollToFile(string path) => ScrollTo(diffRows.Rows.FindIndexBy(r => r.FilePath == path));

    void ScrollTo(int index)
    {
        if (index == -1)
            return;
        ShowAtTop(index);
    }

    // Puts the cursor on a row at the top of the view, as far as the rows after it fill the view
    void ShowAtTop(int index)
    {
        contentView.Scroll(index - contentView.FirstIndex);
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

        // First the lines the cursor is on, which is where the eye is, then the files whole
        var lines = ChosenLines();
        var whyNotLines = WhyNotDiscardLines(lines);
        return Menu
            .Items.Item(
                DiscardLinesText(lines),
                "",
                () => DiscardLines(lines),
                () => whyNotLines == "",
                () => whyNotLines
            )
            .Separator()
            .Items(undoItems)
            .Separator()
            .Item("Binary Files", "", () => UndoAllBinaryFiles(binaryPaths), () => binaryPaths.Any())
            .Item("All Changes", "", () => UndoAll());
    }

    // The lines a discard of lines is of: the selected rows, or else the change the cursor is on
    IReadOnlyList<FileLines> ChosenLines()
    {
        var (first, count) = IsSelected
            ? (contentView.SelectStartIndex, contentView.SelectCount)
            : DiffLines.ChangeAt(diffRows.Rows, contentView.CurrentIndex);
        return DiffLines.Of(diffRows, first, count);
    }

    // Why the lines cannot be discarded, or "" when they can. While whitespace is hidden the diff does
    // not show every change, and git's patch of a file moved, or of a conflict, is not of its lines:
    // the patch of a moved file's new path has none of the old one, and a conflict's lines are markers.
    string WhyNotDiscardLines(IReadOnlyList<FileLines> files) =>
        isIgnoreWhitespace ? "Show whitespace first with w: while it is hidden, not every change is shown"
        : files.Count == 0 && IsOnHiddenFile(out var hidden) ? HiddenText(hidden)
        : files.Count == 0
            ? IsSelected ? "The selected lines have no change"
                : "Move to a change first, or select lines with ⇧↑ ⇧↓"
        : files.Select(f => WhyNotLinesOf(f.Path, "discarded")).FirstOrDefault(why => why != "") ?? "";

    // done is what is done with the file, e.g. "discarded"
    string WhyNotLinesOf(string path, string done) =>
        ConflictPaths().Contains(path) ? $"{path} has conflicts: resolve them first, or it is {done} whole" : "";

    FileDiff? FileOf(string path) => diffs[0].FileDiffs.FirstOrDefault(f => f.PathAfter == path);

    // E.g. 'This Change in a.txt', or 'Selected Lines in 2 Files'
    string DiscardLinesText(IReadOnlyList<FileLines> files)
    {
        var name = IsSelected ? "Selected Lines" : "This Change";
        return files.Count switch
        {
            0 => name,
            1 => $"{name} in {files[0].Path.Max(40)}",
            _ => $"{name} in {files.Count} Files",
        };
    }

    // Discards the lines, asked first, and reads the diff again: the rest of each file's changes stay
    async void DiscardLines(IReadOnlyList<FileLines> files)
    {
        var lines = files.SelectMany(f => f.Lines).ToList();
        var where = files.Count == 1 ? files[0].Path : $"{files.Count} files";
        var what = $"{(IsSelected ? "the selected lines" : "this change")} in {where}";
        var shown = lines.Select(l => $"{(l.IsAdded ? '+' : '-')}{l.Text.TrimEnd('\r')}").ToList();
        if (!Confirm.DiscardLines(what, shown, IsNewFileWhole(files)))
            return;

        using (progress.Show())
        {
            foreach (var file in files)
            {
                if (await server.DiscardLinesAsync(file.Path, file.Lines, repoPath) is Error e)
                {
                    UI.ErrorMessage($"Failed to discard the lines of {file.Path}\n{e.AllMessages()}");
                    RefreshDiff();
                    return;
                }
                leftOut.Remove(file.Path, file.Lines); // Gone, so not to be left out of a commit
            }
        }

        contentView.ClearSelection();
        RefreshDiff();
        status.Info(Confirm.Discarded($"{lines.Count} line{(lines.Count == 1 ? "" : "s")} of {where}"));
    }

    // All the lines of a new file are the file, which their discard deletes
    bool IsNewFileWhole(IReadOnlyList<FileLines> files) =>
        files is [var file]
        && FileOf(file.Path) is { DiffMode: DiffMode.DiffAdded } added
        && file.Lines.Count == added.SectionDiffs.Sum(s => s.LineDiffs.Count(l => l.DiffMode == DiffMode.DiffAdded));

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
        status.Info(Confirm.Discarded($"{binaryPaths.Count} binary file{(binaryPaths.Count == 1 ? "" : "s")}"));
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
                RefreshDiff();
                return;
            }
        }

        RefreshDiff();
        status.Info(Confirm.Discarded(path));
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
                RefreshDiff();
                return;
            }
        }

        RefreshDiff();
        status.Info(Confirm.Discarded("all changes"));
    }

    // Shows the file the cursor is on with more (step 1) or less (step -1) context around its
    // changes. Git only has whole-diff options, so the diff is re-fetched at the new context and
    // just this one file taken from it, leaving the other files at whatever they were shown with.
    async void StepContext(int step)
    {
        var anchor = CurrentAnchor();
        var wanted = WantedContext(anchor, step);
        if (wanted == 0)
        {
            status.Notice(WhyNotStepContext(anchor, step));
            return;
        }

        using (progress.Show())
        {
            var fetchedResult = await source.ReadAsync(Options(wanted));
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
    // no context to step, there is no file under the cursor, that file has no lines shown, its changes
    // being whitespace that is hidden, its lines hidden with h, or it being in conflict, which is a
    // row saying so, or it is already at the widest or the narrowest. Also what the menu asks to know
    // whether to offer the item.
    int WantedContext(DiffAnchor anchor, int step)
    {
        var filePath = anchor.FilePath;
        if (!source.HasContext)
            return 0;
        if (filePath == "")
            return 0; // The cursor is above the first file, so there is nothing to step
        if (IsWhitespaceOnly(anchor) || hiddenFiles.Contains(filePath) || IsInConflict(filePath))
            return 0;

        var current = fileContext.GetValueOrDefault(filePath, DiffContext.Default);
        var wanted = DiffContext.Step(current, step);

        return wanted == current ? 0 : wanted;
    }

    bool CanStepContext(int step) => WantedContext(CurrentAnchor(), step) != 0;

    bool IsInConflict(string path) => conflictState.Files.Any(c => c.Path == path);

    // Why + or - does nothing where the cursor is, in the order WantedContext rules it out
    string WhyNotStepContext(DiffAnchor anchor, int step) =>
        !source.HasContext ? "A line's history has no context to show more or less of"
        : anchor.FilePath == "" ? NoFile
        : IsWhitespaceOnly(anchor) ? "Its changes are whitespace alone, which is hidden: w shows them"
        : hiddenFiles.Contains(anchor.FilePath) ? HiddenText(anchor.FilePath)
        : IsInConflict(anchor.FilePath) ? $"{anchor.FilePath} is in conflict: Enter resolves it"
        : step > 0 ? "The whole file is shown"
        : "The least context is shown";

    // Whether the file the anchor is in, in the commit it is in, has whitespace changes alone, which
    // it is listed with no lines for while whitespace is hidden
    bool IsWhitespaceOnly(DiffAnchor anchor) =>
        diffs.FirstOrDefault(d => anchor.CommitId == "" || d.Id == anchor.CommitId) is CommitDiff diff
        && diff.FileDiffs.Any(f => f.PathAfter == anchor.FilePath && f.IsWhitespaceOnly);

    // Names the file the item would act on and what it would then show, since 'the file the cursor
    // is on' is the part of these two commands that the name alone does not give away. Falls back
    // to the bare name when there is nothing to step, where the item is disabled anyway.
    string ContextItemText(string name, int step)
    {
        var anchor = CurrentAnchor();
        var path = anchor.FilePath;
        var wanted = WantedContext(anchor, step);
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
            var refreshedResult = await source.ReadAsync(Options(DiffContext.Default));
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

    // Whether x would leave lines out, or else put them back in: every line it acts on is out already
    bool IsLeavingOut()
    {
        var files = LinesToLeaveOut();
        if (files.Count == 0 && IsOnFileHeader(out var path))
            return leftOut.CountOf(path) == 0;
        return !files.All(f => f.Lines.All(l => leftOut.IsLeftOut(f.Path, l))) || files.Count == 0;
    }

    string LeaveOutHint() =>
        !source.CanCommit || source.IsCommittedWhole ? ""
        : IsLeavingOut() ? "leave out"
        : "put back";

    bool IsOnFileHeader(out string path)
    {
        var row =
            contentView.CurrentIndex >= 0 && contentView.CurrentIndex < diffRows.Count
                ? diffRows.Rows[contentView.CurrentIndex]
                : null;
        path = row?.Mode == DiffRowMode.SpanBoth ? row.FilePath : "";
        return path != "";
    }

    // The lines x acts on: the selected rows, the change the cursor is on, or on a file's header the
    // whole file
    IReadOnlyList<FileLines> LinesToLeaveOut()
    {
        if (IsSelected || !IsOnFileHeader(out _))
            return ChosenLines();
        var (first, count) = DiffLines.FileAt(diffRows.Rows, contentView.CurrentIndex);
        return DiffLines.Of(diffRows, first, count);
    }

    // Why x cannot leave the lines out, or "" when it can
    string WhyNotLeaveOut(IReadOnlyList<FileLines> files) =>
        !source.CanCommit ? "Lines are left out of a commit in the diff of the uncommitted changes"
        : source.IsCommittedWhole ? "What is in progress is committed whole, as git commits it"
        : isIgnoreWhitespace ? "Show whitespace first with w: while it is hidden, not every change is shown"
        : files.Count == 0 && IsOnFileHeader(out var path) && leftOut.CountOf(path) > 0 ? ""
        : files.Count == 0 && IsOnHiddenFile(out var hidden) ? HiddenText(hidden)
        : files.Count == 0
            ? IsSelected ? "The selected lines have no change"
                : "Move to a change first, or select lines with ⇧↑ ⇧↓"
        : files.Select(f => WhyNotLinesOf(f.Path, "committed")).FirstOrDefault(why => why != "") ?? "";

    // Leaves the lines out of the commit, or puts them back in, x (LeftOutLines): the selected rows,
    // the change the cursor is on, or on a file's header every line of the file, which puts back too
    // the lines left out that are not found any more, the file having changed since
    void ToggleLeftOut()
    {
        var files = LinesToLeaveOut();
        if (WhyNotLeaveOut(files) is var why && why != "")
        {
            status.Notice(why);
            return;
        }

        if (!IsSelected && IsOnFileHeader(out var path) && leftOut.CountOf(path) > 0)
        {
            leftOut.PutBack([path]);
            notFound.Remove(path);
            status.Info($"Put every line of {path} back in the commit");
        }
        else
        {
            // One way for all of them, out unless every one is out already
            var isOut = IsLeavingOut();
            foreach (var file in files)
            {
                if (isOut)
                    leftOut.LeaveOut(file.Path, file.Lines);
                else
                    leftOut.Remove(file.Path, file.Lines);
            }
            var count = files.Sum(f => f.Lines.Count);
            var lines =
                $"{count} line{(count == 1 ? "" : "s")} of {(files.Count == 1 ? files[0].Path : $"{files.Count} files")}";
            status.Info(
                isOut ? $"Left {lines} out of the commit: x puts them back" : $"Put {lines} back in the commit"
            );
        }

        contentView.ClearSelection();
        contentView.SetNeedsDisplay();
        hintBar.SetNeedsDisplay();
    }

    // The lines left out found again in the rows, each file's, and a file no longer changed forgotten.
    // Not while whitespace is hidden, which shows a line whose spaces alone changed as unchanged, so it
    // would not be found, or another line with its text found in its place.
    void RefindLeftOutLines()
    {
        notFound.Clear();
        if (!source.CanCommit || isIgnoreWhitespace)
            return;

        var paths = diffs.SelectMany(d => d.FileDiffs).Select(f => f.PathAfter).ToHashSet();
        leftOut.PutBack(leftOut.Paths.Where(p => !paths.Contains(p)).ToList());
        var byFile = DiffLines.Of(diffRows, 0, diffRows.Count).ToDictionary(f => f.Path, f => f.Lines);
        foreach (var path in leftOut.Paths.ToList())
        {
            // Not drawn, so not to be found: the commit itself checks them, as it does every line
            if (hiddenFiles.Contains(path))
                continue;

            var count = leftOut.Refind(path, byFile.GetValueOrDefault(path, []));
            if (count > 0)
                notFound[path] = count;
        }
    }

    // The text of a line left out, dark, the bar of its change a '╎'
    static Text LeftOutText(Text text, int lineNbr)
    {
        var line = text.ToString();
        if (DiffRowService.LineNbrWidth(lineNbr) > 0 && line.Length > 0)
            line = $"╎{line[1..]}";
        return Text.Dark(line);
    }

    bool IsLeftOut(string path, bool isAdded, int lineNbr, string? text) =>
        text != null && leftOut.IsLeftOut(path, new ChosenLine(isAdded, lineNbr, text));

    // A file's header says how many of its lines are left out, and how many of those are not found
    Text WithLeftOutNote(Text header, string path)
    {
        var count = leftOut.CountOf(path);
        if (!source.CanCommit || count == 0)
            return header;
        var lines = $"{count} line{(count == 1 ? "" : "s")} left out";
        var note = notFound.TryGetValue(path, out var lost)
            ? $"  ({lines}, {lost} not found since: x here puts them all back)"
            : $"  ({lines})";
        return header.ToTextBuilder().Add(Text.Yellow(note));
    }

    // Hides the changes of whitespace alone, or shows them again, as GitHub's 'Hide whitespace' does:
    // the diff is read again with or without them, every file at the default context as after a
    // refresh, and a file whose changes are all whitespace is listed with no lines (DiffWhitespace)
    async void ToggleWhitespace()
    {
        var anchor = CurrentAnchor();
        var options = new Git.DiffOptions(DiffContext.Default, !isIgnoreWhitespace);

        using (progress.Show())
        {
            var readResult = await source.ReadAsync(options);
            if (readResult is not CommitDiff[] read)
            {
                UI.ErrorMessage($"Failed to get diff\n{readResult.Error.AllMessages()}");
                return;
            }

            isIgnoreWhitespace = options.IsIgnoreWhitespace;
            fileContext.Clear();
            SetDiffs(read, anchor);
        }

        status.Info(isIgnoreWhitespace ? "Whitespace changes hidden: w shows them" : "Whitespace changes shown");
    }

    // Hides the lines of the file the cursor is in, h, which is then drawn as its header alone, e.g. a
    // file that is generated, or shows them again, for this diff
    void ToggleHidden()
    {
        var path = CurrentAnchor().FilePath;
        if (path == "")
        {
            status.Notice(NoFile);
            return;
        }

        var isHidden = !hiddenFiles.Remove(path);
        if (isHidden)
            hiddenFiles.Add(path);
        SetDiffs(diffs, CurrentAnchor());
        status.Info(isHidden ? $"The lines of {path} are hidden: h shows them" : $"The lines of {path} are shown");
    }

    const string NoFile = "Move to a file first";

    string HiddenItemText()
    {
        var path = CurrentAnchor().FilePath;
        return path == "" ? "Hide the Lines of a File"
            : hiddenFiles.Contains(path) ? $"Show the Lines of {path.Max(40)}"
            : $"Hide the Lines of {path.Max(40)}";
    }

    bool IsOnHiddenFile(out string path) => IsOnFileHeader(out path) && hiddenFiles.Contains(path);

    static string HiddenText(string path) => $"The lines of {path} are hidden: h shows them";

    // Shows the diff in one column, or side by side again, v, for this diff: the next one is shown as
    // its width chooses
    void ToggleLayout()
    {
        isOneColumnChosen = !isOneColumn;
        SetLayout(!isOneColumn);
        status.Info(
            isOneColumn ? "In one column: v shows the sides beside each other" : "Side by side: v shows one column"
        );
    }

    // The layout the view's width calls for, once it is drawn at a width other than the rows were
    // made for, e.g. the terminal resized. Posted, since the rows cannot change while they are drawn.
    void FollowWidth(int contentWidth)
    {
        var isListChanged = ListWidth() != listWidth;
        if (isLayoutPosted || (!isListChanged && IsOneColumnFor(contentWidth) == isOneColumn))
            return;

        isLayoutPosted = true;
        UI.Post(() =>
        {
            isLayoutPosted = false;
            SetListWidth(ListWidth());
            SetLayout(IsOneColumnFor(Application.Driver.Cols - listWidth - 1));
        });
    }

    // Shows the list of files at the width, or hides it at 0, the diff beside it taking the rest
    void SetListWidth(int width)
    {
        if (width == listWidth)
            return;

        listWidth = width;
        if (listWidth == 0 && isListFocus)
            ToggleListFocus();
        listView.Width = listWidth;
        contentView.X = listWidth;
        listView.SuperView?.LayoutSubviews();
        listView.SuperView?.SetNeedsDisplay();
    }

    // The rows for a layout, made again where it is not theirs: in one column or two, and with the
    // list beside them or not, which lists the files or commits the rows would otherwise list at the top
    void SetLayout(bool isOneColumn)
    {
        if (isOneColumn == this.isOneColumn && (listWidth > 0) == isRowsBesideList)
            return;

        this.isOneColumn = isOneColumn;
        SetDiffs(diffs, CurrentAnchor());
        hintBar.SetNeedsDisplay();
    }

    // How wide a row across the view is drawn, as OnGetContent lays it out
    int ViewWidth => ColumnWidth * 2 + 1;

    // The options the diff is read with, at a context
    Git.DiffOptions Options(int contextLines) => new(contextLines, isIgnoreWhitespace);

    // Replaces the shown diffs and redraws, keeping the cursor on the content it was on rather
    // than on the row index it was at, which means nothing once the rows are rebuilt.
    void SetDiffs(CommitDiff[] newDiffs, DiffAnchor anchor)
    {
        diffs = newDiffs;
        isRowsBesideList = listWidth > 0;
        diffRows = diffRowService.ToDiffRows(
            diffs,
            fileContext,
            ConflictPaths(),
            isIgnoreWhitespace,
            isOneColumn,
            hiddenFiles,
            isRowsBesideList
        );
        fileList = DiffFileList.Of(diffs, diffRows);
        // Counted now rather than as the views draw, for the cursor to be put back in the rows that are
        contentView.SetTotalCount(diffRows.Count);
        listView.SetTotalCount(fileList.Items.Count + 1);
        search = FindInRows(search.Text); // The rows the search found are other rows now
        RefindLeftOutLines(); // As the lines left out may have moved
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
        if (!isFocusLeft && !isOneColumn)
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
        if (isFocusLeft && !isOneColumn)
        { // Text is selected, lets move selection from left to right side
            isFocusLeft = false;
            contentView.SetNeedsDisplay();
            return;
        }

        // Move both sides in view right
        int maxColumnWidth = isOneColumn ? contentView.ContentWidth : contentView.ContentWidth / 2;
        if (diffRows!.MaxLength - rowStartX > maxColumnWidth)
        {
            rowStartX++;
            contentView.SetNeedsDisplay();
        }
    }

    // Returns the content for the view
    (IEnumerable<Text> rows, int total) OnGetContent(int firstRow, int rowCount, int currentIndex, int contentWidth)
    {
        FollowWidth(contentWidth);
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

        var path = source.CanCommit ? diffRows.FileAt(index) : "";
        var isLeftOutLeft = path != "" && IsLeftOut(path, false, row.LeftLineNbr, row.RemovedText);
        var isLeftOutRight = path != "" && IsLeftOut(path, true, row.RightLineNbr, row.AddedText);

        if (row.Mode == DiffRowMode.SpanBoth)
        { // The left text spans over full width
            var spanText =
                isLeftOutLeft || isLeftOutRight ? LeftOutText(row.Left, row.LineNbr)
                : row.FilePath != "" ? WithLeftOutNote(row.Left, row.FilePath)
                : Marked(row.Left, row.LineNbr);
            // A line across the view, of a file added or removed whole or in one column, scrolls
            // sideways as a side does, its gutter staying put
            var text =
                row.LineNbr != 0 ? SideText(spanText, row.LineNbr, false, viewWidth)
                : row.IsSeparator && !isOneColumn ? SeparatorText(spanText, row.Fill!, columnWidth, viewWidth)
                : WithFill(spanText.Subtext(0, viewWidth), row.Fill, viewWidth);
            return isSelectedRow ? text.ToSelect()
                : !isCurrentRow ? text
                // A line of a file drawn in one column has the cursor on its number, as a side has
                : row.LineNbr != 0 ? WithCursor(text, DiffRowService.LineNbrWidth(row.LineNbr))
                : text.ToHighlight();
        }

        // The left and right text is shown side by side with a gray vertical line char in between
        var leftText = isLeftOutLeft ? LeftOutText(row.Left, row.LeftLineNbr) : Marked(row.Left, row.LeftLineNbr);
        var rightText = isLeftOutRight ? LeftOutText(row.Right, row.RightLineNbr) : Marked(row.Right, row.RightLineNbr);
        var left = SideText(leftText, row.LeftLineNbr, row.Left == DiffRowService.NoLine, columnWidth);
        var right = SideText(rightText, row.RightLineNbr, row.Right == DiffRowService.NoLine, columnWidth);

        return Text.Add(Focused(left, true, isSelectedRow, isCurrentRow, row.LeftLineNbr, row.Left))
            .Add(splitLineChar)
            .Add(Focused(right, false, isSelectedRow, isCurrentRow, row.RightLineNbr, row.Right));
    }

    // A side of a row in its column: its gutter, which stays where it is, and its line, scrolled
    // sideways with the other side's, with a '…' where it is cut off. The gutter scrolled away with
    // the line, which left a line scrolled to with no number to say which it was.
    Text SideText(Text side, int lineNbr, bool isNoLine, int columnWidth)
    {
        if (isNoLine)
            return Text.Black(new string(' ', Math.Max(0, columnWidth)));

        var gutterWidth = Math.Min(DiffRowService.LineNbrWidth(lineNbr), columnWidth);
        var code = side.Subtext(gutterWidth, Math.Max(0, side.Length - gutterWidth));
        var width = columnWidth - gutterWidth;

        var isCutLeft = rowStartX > 0 && code.Length > 0;
        var isCutRight = code.Length - rowStartX > width;
        var shownWidth = width - (isCutLeft ? 1 : 0) - (isCutRight ? 1 : 0);

        var text = side.Subtext(0, gutterWidth).ToTextBuilder();
        if (isCutLeft)
            text.Dark("…");
        text.Add(code.Subtext(rowStartX + (isCutLeft ? 1 : 0), Math.Max(0, shownWidth)));
        if (isCutRight)
            text.Dark("…");
        return text.ToText().Subtext(0, columnWidth, isFillRest: true);
    }

    // The side in focus is the one the cursor and a selection are drawn on, and the one a copy takes
    // the lines of. The cursor is on the line's number, or where a side with no line would have it; a
    // row of no line, e.g. a conflict's marker, is highlighted whole.
    Text Focused(Text side, bool isLeft, bool isSelectedRow, bool isCurrentRow, int lineNbr, Text rowSide)
    {
        if (isLeft != isFocusLeft || (!isSelectedRow && !isCurrentRow))
            return side;
        if (isSelectedRow)
            return side.ToSelect();

        return lineNbr != 0 ? WithCursor(side, DiffRowService.LineNbrWidth(lineNbr))
            : rowSide == DiffRowService.NoLine ? WithCursor(side, DiffRowService.LineNbrWidth(1))
            : side.ToHighlight();
    }

    static readonly Common.Color CursorColor = new(Terminal.Gui.Color.Black, Terminal.Gui.Color.White);

    // The cursor on a line: its number reversed, black on white, with the bar beside it and the
    // line itself as they are. A highlight of the whole side painted over the colors of the change.
    static Text WithCursor(Text text, int gutterWidth) =>
        text.Subtext(0, 1)
            .ToTextBuilder()
            .Color(CursorColor, text.Subtext(1, gutterWidth - 1, isFillRest: true).ToString())
            .Add(text.Subtext(gutterWidth, Math.Max(0, text.Length - gutterWidth)));

    // Where unchanged lines are left out between two hunks, side by side: '⋯' where each side's line
    // numbers are, the function git found the hunk in after the left one, cut short of the right one,
    // and the line through both on to the end of the row
    static Text SeparatorText(Text text, Text fill, int columnWidth, int viewWidth)
    {
        var right = columnWidth + 1 + DiffRowService.LeftOutIndent.Length; // The right side's '⋯'
        if (right + 2 >= viewWidth)
            return WithFill(text.Subtext(0, viewWidth), fill, viewWidth);

        // Room for a space, a rune of line and a space before the right '⋯'
        var left = text.Length <= right - 3 ? text : text.Subtext(0, right - 4).ToTextBuilder().Dark("…").ToText();
        return WithFill(left, fill, right - 1)
            .ToTextBuilder()
            .Black(" ")
            .Dark(DiffRowService.LeftOutMark)
            .Black(" ")
            .Add(fill.ToLine(viewWidth - right - 2));
    }

    // A header row followed to the end of the row by its rule, e.g. a file's
    static Text WithFill(Text text, Text? fill, int width) =>
        fill == null || text.Length + 1 >= width
            ? text
            : text.ToTextBuilder().Black(" ").Add(fill.ToLine(width - text.Length - 1));

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
