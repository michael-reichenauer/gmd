using gmd.Common;
using gmd.Cui.App;
using gmd.Cui.Commits;
using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Blame;

interface IBlameView
{
    void Show(Server.Blame blame, Repo repo);
}

// Shows which commit last changed each line of a file. Consecutive lines of the same commit are
// bracketed into one run in the gutter and the commit is named once per run, which is what makes
// this readable where the console 'git blame' is not.
class BlameView : IBlameView
{
    readonly IBlameRowService blameRowService;
    readonly IServer server;
    readonly IProgress progress;
    readonly IDiffView diffView;
    readonly IClipboardService clipboard;

    // A new details view per Show, since it creates its own view in its constructor and that view
    // is added to the toplevel this Show builds, which the next Show replaces
    readonly Func<ICommitDetailsView> newDetailsView;

    ContentView contentView = null!;
    ICommitDetailsView detailsView = null!;
    UILabel header = null!;
    Server.Blame blame = null!;
    Repo repo = null!;
    BlameRows blameRows = new BlameRows();
    int rowStartX = 0;
    bool isShowDetails = false;
    BlameDetails details = BlameDetails.Full;

    // Where the user came from, so a drill down into previous versions can be stepped back out of.
    // A stack within the one Show call rather than nested views, so Esc still means 'close'.
    readonly Stack<BlameState> backStack = new();

    record BlameState(string Path, string Reference, int Index, int RowStartX);

    readonly IHelpPage helpPage;
    readonly Config config;
    KeyHintBar hintBar = null!;

    // The search of the lines, f and then n and Shift-N, and the status line of this view, which says
    // what it found, as in the diff
    RowSearch search = RowSearch.None;
    IStatusLine status = new StatusLine();

    // The bottom row, for the key hints, unless they are turned off
    int HintsHeight => config.ShowKeyHints ? 1 : 0;

    public BlameView(
        IBlameRowService blameRowService,
        IServer server,
        IProgress progress,
        IDiffView diffView,
        IClipboardService clipboard,
        Func<ICommitDetailsView> newDetailsView,
        IHelpPage helpPage,
        Config config
    )
    {
        this.helpPage = helpPage;
        this.config = config;
        this.blameRowService = blameRowService;
        this.server = server;
        this.progress = progress;
        this.diffView = diffView;
        this.clipboard = clipboard;
        this.newDetailsView = newDetailsView;
    }

    bool IsSelected => contentView.SelectCount > 0;

    BlameRow? CurrentRow =>
        contentView.CurrentIndex >= 0 && contentView.CurrentIndex < blameRows.Rows.Count
            ? blameRows.Rows[contentView.CurrentIndex]
            : null;

    public void Show(Server.Blame blame, Repo repo)
    {
        this.blame = blame;
        this.blameRows = blameRowService.ToBlameRows(blame);
        this.repo = repo;
        this.rowStartX = 0;
        this.isShowDetails = false;
        this.details = BlameDetails.Full;
        this.backStack.Clear();
        this.search = RowSearch.None;
        this.status = new StatusLine();

        Toplevel blameView = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        // A header row of its own rather than a first row of content, so it stays put while the
        // file scrolls. Same label plus border pair as the application bar of the log view.
        header = new UILabel(0, 0);
        var border = new HorizontalLine()
        {
            X = 0,
            Y = 1,
            ColorScheme = ColorSchemes.Border,
        };

        // The bar is there with the hints off too, for what a search found, as in the diff
        hintBar = new KeyHintBar(
            () =>
                BlameHints.For(
                    isShowDetails,
                    CurrentRow?.Commit.PreviousId is not (null or ""),
                    backStack.Count > 0,
                    search.IsActive
                ),
            () => status.Current
        );

        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(HintsHeight),
            IsShowCursor = false,
            IsScrollMode = false,
            IsCustomShowSelection = true,
        };

        // Above the key hints, as the log view has its details
        detailsView = newDetailsView();
        detailsView.View.Y = Pos.AnchorEnd(CommitDetailsView.ContentHeight + HintsHeight);

        blameView.Add(header, border, contentView, detailsView.View, hintBar);
        hintBar.FollowStatus(status, config.ShowKeyHints, blameView);
        // 'p' and Backspace are offered for the line's commit, and for having stepped back
        contentView.CurrentIndexChange += () => hintBar.SetNeedsDisplay();
        RegisterShortcuts(contentView);
        detailsView.View.RegisterKeyHandler(Key.Tab, ToggleDetailsFocus);
        detailsView.View.RegisterKeyHandler(Key.Enter, ToggleDetails);

        // The details follow the cursor, so walking down the lines walks through the commits
        contentView.CurrentIndexChange += OnCurrentIndexChange;

        SetHeader();
        contentView.SetNeedsDisplay();
        UI.RunDialog(blameView);
    }

    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.Esc, () => Application.RequestStop());
        // Letters in both cases, since the menu writes them in upper case. The view is modal (see
        // UI.RunDialog), so a key not registered here does nothing rather than reaching the log view.
        view.RegisterLetterHandler(Key.q, () => Application.RequestStop());
        // The help, as in every view, at the part about this one
        view.RegisterKeyHandler((Key)'?', () => helpPage.ShowAsync(HelpPage.DiffSection).RunInBackground());
        view.RegisterKeyHandler(Key.F1, () => helpPage.ShowAsync(HelpPage.DiffSection).RunInBackground());

        view.RegisterKeyHandler(Key.CursorLeft, OnMoveLeft);
        view.RegisterKeyHandler(Key.CursorRight, OnMoveRight);
        view.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);

        view.RegisterKeyHandler(Key.Enter, ToggleDetails);
        view.RegisterKeyHandler(Key.Tab, ToggleDetailsFocus);

        // While the details pane is the focused one, the scroll keys are forwarded to it. They
        // have to be taken here, since this is the view that actually has the keyboard, see
        // ToggleDetailsFocus. Returning false leaves the key to this view, i.e. moves the cursor.
        view.RegisterKeyHandler(Key.CursorUp, () => ScrollDetails(-1));
        view.RegisterKeyHandler(Key.CursorDown, () => ScrollDetails(1));
        view.RegisterKeyHandler(Key.PageUp, () => ScrollDetails(-CommitDetailsView.ContentHeight));
        view.RegisterKeyHandler(Key.PageDown, () => ScrollDetails(CommitDetailsView.ContentHeight));
        view.RegisterLetterHandler(Key.m, () => ShowMainMenu());
        view.RegisterLetterHandler(Key.g, CycleDetails); // The gutter
        view.RegisterLetterHandler(Key.d, ShowLineCommitDiff);
        view.RegisterLetterHandler(Key.l, ShowLineHistory);
        view.RegisterLetterHandler(Key.p, BlamePrevious);
        view.RegisterKeyHandler(Key.Backspace, Back);
        // The line's commit id and message, as 'i' and Shift-I copy a commit's in the log view, so
        // one case each rather than both
        view.RegisterKeyHandler(Key.i, CopyLineId);
        view.RegisterKeyHandler(Key.I, CopyLineMessage);

        // Search the lines with the keys of the diff and the log, n and Shift-N one case each as there
        view.RegisterLetterHandler(Key.f, Search);
        view.RegisterKeyHandler((Key)'/', Search);
        view.RegisterKeyHandler(Key.n, () => StepMatch(1));
        view.RegisterKeyHandler(Key.N, () => StepMatch(-1));

        view.RegisterMouseHandler(MouseFlags.Button1Pressed, (x, y) => OnMouseClick(y));
        // At the screen row above the click, so the first item is beside it: the view is two rows down
        view.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => ShowMainMenu(x - 1, y + 1));
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstRow, int rowCount, int currentIndex, int contentWidth)
    {
        var cw = BlameColumns.Calculate(details, contentWidth, blameRows.Rows.Count);

        var rows = blameRows
            .Rows.Skip(firstRow)
            .Take(rowCount)
            .Select(
                (r, i) =>
                    blameRowService.ToRowText(
                        r,
                        cw,
                        rowStartX,
                        i + firstRow == currentIndex,
                        contentView.IsRowSelected(i + firstRow),
                        search.IsActive ? search.Text : ""
                    )
            );

        return (rows, blameRows.Rows.Count);
    }

    void SetHeader()
    {
        var text = Text.Dark("Blame  ").White(blame.Path);
        if (blame.Reference != "")
            text.Dark("  @").Cyan(blame.Reference.Sid());
        text.Dark($"   {Count(blameRows.Rows.Count, "line")}, {Count(blame.CommitById.Count, "commit")}");
        if (backStack.Count > 0)
            text.Dark("   ").Yellow($"{backStack.Count} back");

        header.Text = text;
        // The Text setter sizes the label from the text it is replacing, so a longer header would
        // be clipped to the length of the previous one
        header.Width = Dim.Fill();
    }

    static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    void OnMoveLeft()
    {
        if (rowStartX > 0)
        {
            rowStartX--;
            contentView.SetNeedsDisplay();
        }
    }

    void OnMoveRight()
    {
        var cw = BlameColumns.Calculate(details, contentView.ContentWidth, blameRows.Rows.Count);
        if (blameRows.MaxLength - rowStartX > cw.Code)
        {
            rowStartX++;
            contentView.SetNeedsDisplay();
        }
    }

    bool OnMouseClick(int y)
    {
        contentView.SetIndexAtViewY(y);
        contentView.SetNeedsDisplay();
        return false;
    }

    void CycleDetails()
    {
        details = details == BlameDetails.Rail ? BlameDetails.Full : details + 1;
        contentView.SetNeedsDisplay();
    }

    // The commit details of the current line, in a pane at the bottom, as the log view shows them
    // for the current commit and with the same key
    void ToggleDetails()
    {
        isShowDetails = !isShowDetails;

        if (isShowDetails)
        {
            contentView.Height = Dim.Fill(CommitDetailsView.ContentHeight + HintsHeight);
            detailsView.View.Height = CommitDetailsView.ContentHeight;
            OnCurrentIndexChange();
        }
        else
        {
            contentView.Height = Dim.Fill(HintsHeight);
            detailsView.View.Height = 0;
            contentView.IsFocus = true;
            detailsView.View.IsFocus = false;
        }

        contentView.SetNeedsDisplay();
        detailsView.View.SetNeedsDisplay();
        hintBar.SetNeedsDisplay(); // Enter shows or hides the details
    }

    // Focus moves to the details so a long commit message can be scrolled, as in the log view.
    // Only the drawn focus moves: Terminal.Gui's own focus stays on the blame, since SetFocus does
    // not move it (the log view says the same), which is why ScrollDetails forwards the keys.
    void ToggleDetailsFocus()
    {
        if (!isShowDetails)
            return;

        contentView.IsFocus = !contentView.IsFocus;
        detailsView.View.IsFocus = !detailsView.View.IsFocus;

        contentView.SetNeedsDisplay();
        detailsView.View.SetNeedsDisplay();
    }

    // True when the key was consumed by the details pane, i.e. it is the focused one
    bool ScrollDetails(int count)
    {
        if (!isShowDetails || !detailsView.View.IsFocus)
            return false;

        detailsView.View.Scroll(count);
        return true;
    }

    void OnCurrentIndexChange()
    {
        if (!isShowDetails)
            return;

        var row = CurrentRow;
        if (row == null)
            return;

        // The blame only knows the first line of the commit message and nothing about branches, so
        // the shown log is what the full details come from. Only the log is capped, not the blame,
        // so a very large repo can blame a commit that is not in it, hence the fallback.
        if (
            repo.CommitById.TryGetValue(row.Commit.Id, out var commit)
            && repo.BranchByName.TryGetValue(commit.BranchName, out var branch)
        )
        {
            detailsView.Set(repo, commit, branch);
            return;
        }

        detailsView.SetRows(blameRowService.ToDetailsRows(row.Commit));
    }

    // Shows the commit that last changed the current line. The uncommitted lines carry the all '0'
    // sha, which the server routes to the uncommitted diff, shown read only: a view two dialogs deep
    // cannot act on a commit, and a discard would change the lines this blame shows.
    async void ShowLineCommitDiff()
    {
        var row = CurrentRow;
        if (row == null)
            return;

        var source = DiffSource.Single(n => server.GetCommitDiffAsync(row.Commit.Id, n, repo.Path));
        Server.CommitDiff[] diffs;
        using (progress.Show())
        {
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not Server.CommitDiff[] loaded)
            {
                UI.ErrorMessage($"Failed to get diff\n{diffsResult.Error.AllMessages()}");
                return;
            }
            diffs = loaded;
        }

        diffView.Show(diffs, source, repo.Path);
        contentView.SetNeedsDisplay();
    }

    // The lines a line history is of: the selected ones, or the current one, as indexes
    (int First, int Last) HistoryLines =>
        IsSelected
            ? (contentView.SelectStartIndex, contentView.SelectStartIndex + contentView.SelectCount - 1)
            : (contentView.CurrentIndex, contentView.CurrentIndex);

    string HistoryName()
    {
        var (first, last) = HistoryLines;
        return first == last ? $"History of Line {first + 1}" : $"History of Lines {first + 1}-{last + 1}";
    }

    // Every commit that changed the selected lines, or the current one, each with the diff of those
    // lines alone ('git log -L'), shown as a file history is: how they came to be as they are, where
    // the blame says only who changed each last. The lines are numbered as the file was blamed, and
    // for the working tree git counts them in the last commit, so a file with changes not committed,
    // which can have moved them, is refused rather than shown the history of other lines.
    async void ShowLineHistory()
    {
        if (CurrentRow == null)
            return;
        var path = blame.Path;
        if (blame.Reference == "" && IsChanged(path))
        {
            UI.InfoMessage(
                "History of Lines",
                $"'{path}' has changes not committed, which can move its lines:\n"
                    + "commit or stash them first, or blame the file from a commit."
            );
            return;
        }

        var (first, last) = HistoryLines;
        var (firstLine, lastLine) = (blame.Lines[first].LineNbr, blame.Lines[last].LineNbr);
        var reference = blame.Reference;
        // 'git log -L' takes no context, so there is none to step
        var source = new DiffSource(_ => server.GetLineHistoryAsync(path, firstLine, lastLine, reference, repo.Path))
        {
            HasContext = false,
        };
        Server.CommitDiff[] diffs;
        using (progress.Show())
        {
            var diffsResult = await source.Reload(DiffContext.Default);
            if (diffsResult is not Server.CommitDiff[] loaded)
            {
                UI.ErrorMessage($"Failed to get the history of the lines\n{diffsResult.Error.AllMessages()}");
                return;
            }
            diffs = loaded;
        }

        diffView.Show(diffs, source, repo.Path);
        contentView.SetNeedsDisplay();
    }

    bool IsChanged(string path) =>
        repo.Status.ModifiedFiles.Contains(path)
        || repo.Status.RenamedTargetFiles.Contains(path)
        || repo.Status.AddedFiles.Contains(path);

    // Blames the file as it was before the current line's commit, which is how a reformat or a
    // rename is stepped past to the change that actually matters. The porcelain 'previous' key is
    // used rather than '<sha>^' and the same path, since it carries the name before a rename.
    async void BlamePrevious()
    {
        var row = CurrentRow;
        if (row == null)
            return;

        var c = row.Commit;
        if (c.PreviousId == "")
        {
            var reason = c.IsUncommitted ? "is not committed yet" : $"was added in {c.Sid}";
            UI.InfoMessage("Blame", $"Line {row.LineNbr} {reason},\nthere is no previous version to blame.");
            return;
        }

        backStack.Push(new BlameState(blame.Path, blame.Reference, contentView.CurrentIndex, rowStartX));
        if (!await ReBlameAsync(c.PreviousPath, c.PreviousId, 0, 0))
            backStack.Pop();
    }

    void Back()
    {
        if (backStack.Count == 0)
            return;

        var state = backStack.Pop();
        ReBlameAsync(state.Path, state.Reference, state.Index, state.RowStartX).RunInBackground();
    }

    async Task<bool> ReBlameAsync(string path, string reference, int index, int startX)
    {
        Server.Blame newBlame;
        using (progress.Show())
        {
            var blameResult = await server.GetBlameAsync(path, reference, repo.Path);
            if (blameResult is not Server.Blame loaded)
            {
                UI.ErrorMessage($"Failed to blame {path}\n{blameResult.Error.AllMessages()}");
                return false;
            }
            newBlame = loaded;
        }

        blame = newBlame;
        blameRows = blameRowService.ToBlameRows(blame);
        search = FindInRows(search.Text); // Searched again, in the version now blamed
        rowStartX = startX;
        // Scrolled to the line too: Back put the cursor on it with the view at the top of the file
        contentView.MoveToTop();
        contentView.ShowCurrentIndex(Math.Min(index, Math.Max(0, blameRows.Rows.Count - 1)));
        SetHeader();
        contentView.SetNeedsDisplay();
        return true;
    }

    const string NoSearch = "No search to go on with: search with f first";

    // Asks for a text and goes to where it is in the code, from the cursor down, and n and Shift-N
    // then go on from wherever the cursor is, as in the diff
    void Search()
    {
        if (SearchDlg.Show("file") is not string text)
            return;

        search = FindInRows(text);
        if (!search.IsActive)
        {
            status.Notice($"'{text}' is not in {blame.Path}");
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

    // The code is searched, not the gutter's commits beside it
    RowSearch FindInRows(string text) => RowSearch.Find(text, blameRows.Rows.Count, i => blameRows.Rows[i].Text);

    // The cursor is put on the line, and the code scrolled sideways to show the match
    void ShowMatch(int index)
    {
        var start = RowSearch.IndexOf(blameRows.Rows[index].Text, search.Text);
        var cw = BlameColumns.Calculate(details, contentView.ContentWidth, blameRows.Rows.Count);
        rowStartX = RowSearch.ScrollToShow(start, start + search.Text.Length, cw.Code);

        contentView.ShowCurrentIndex(index);
        contentView.SetNeedsDisplay();
        status.Info(search.MatchText(index));
    }

    void OnCopy()
    {
        if (!IsSelected)
            return;

        // Copied from the blamed lines rather than the drawn rows, so it is the file's own text
        // with no gutter to strip and no expanded tabs
        var text = string.Join(
            "\n",
            blame.Lines.Skip(contentView.SelectStartIndex).Take(contentView.SelectCount).Select(l => l.Text)
        );

        if (clipboard.Set(text) is Error e)
            UI.ErrorMessage(e.AllMessages());

        contentView.ClearSelection();
    }

    void CopyLineId()
    {
        var row = CurrentRow;
        if (row == null || row.Commit.IsUncommitted)
            return;

        if (clipboard.Set(row.Commit.Id) is Error e)
            UI.ErrorMessage(e.AllMessages());
    }

    // The whole message, which the blame does not have, only its subject: the shown log has it, as
    // for the details pane (OnCurrentIndexChange), and the subject is the fallback for a commit
    // beyond the log's cap
    void CopyLineMessage()
    {
        var row = CurrentRow;
        if (row == null || row.Commit.IsUncommitted)
            return;

        var message = repo.CommitById.TryGetValue(row.Commit.Id, out var commit)
            ? commit.Message.TrimEnd()
            : row.Commit.Subject;
        if (clipboard.Set(message) is Error e)
            UI.ErrorMessage(e.AllMessages());
    }

    void ShowMainMenu(int x = Menu.Center, int y = 0)
    {
        var row = CurrentRow;
        var c = row?.Commit;
        var hasPrevious = c != null && c.PreviousId != "";

        Menu.Show(
            $"Blame: {blame.Path}",
            x,
            y,
            Menu.Items.Item(
                    c == null ? "Commit Diff" : $"Commit Diff of {(c.IsUncommitted ? "uncommitted" : c.Sid)}",
                    "d",
                    () => ShowLineCommitDiff(),
                    () => c != null
                )
                .Item(HistoryName(), "l", () => ShowLineHistory(), () => c != null)
                .Item(
                    hasPrevious ? $"Blame Previous Version ({c!.PreviousId.Sid()})" : "Blame Previous Version",
                    "p",
                    () => BlamePrevious(),
                    () => hasPrevious
                )
                .Item("Back", "Backspace", () => Back(), () => backStack.Count > 0)
                .Separator()
                .Item("Search ...", "f", () => Search())
                .Item("Next Match", "n", () => StepMatch(1), () => search.IsActive, () => NoSearch)
                .Item("Previous Match", "⇧n", () => StepMatch(-1), () => search.IsActive, () => NoSearch)
                .SubMenu("Scroll to Commit", "", GetScrollToItems())
                .Item("Commit Details", "Enter", () => ToggleDetails())
                .Item($"Gutter Detail ({details})", "g", () => CycleDetails())
                .Item("Reset Horizontal Scroll", "", () => ResetScroll(), () => rowStartX > 0)
                .Separator()
                .Item("Copy Selected Lines", "Ctrl-C", () => OnCopy(), () => IsSelected)
                .Item("Copy Commit Id of Line", "i", () => CopyLineId(), () => c != null && !c.IsUncommitted)
                .Item("Copy Commit Message of Line", "⇧i", () => CopyLineMessage(), () => c != null && !c.IsUncommitted)
                .Item("Close", "Esc", () => Application.RequestStop())
        );
    }

    // The distinct commits of this blame, newest first, jumping to where each one starts
    IEnumerable<Common.MenuItem> GetScrollToItems() =>
        blame
            .CommitById.Values.Where(c => blameRows.Rows.Any(r => r.Commit.Id == c.Id))
            .OrderByDescending(c => c.IsUncommitted)
            .ThenByDescending(c => c.AuthorTime)
            .Select(c =>
                Menu.Item(
                    c.IsUncommitted ? "uncommitted" : $"{c.AuthorTime.IsoDate()} {c.Sid} {c.Subject.Max(50, true)}",
                    "",
                    () => ScrollToCommit(c.Id)
                )
            );

    void ScrollToCommit(string commitId)
    {
        var index = blameRows.Rows.FindIndexBy(r => r.Commit.Id == commitId);
        if (index == -1)
            return;

        contentView.ShowCurrentIndex(index);
    }

    void ResetScroll()
    {
        rowStartX = 0;
        contentView.SetNeedsDisplay();
    }
}
