using gmd.Cui.Common;
using gmd.Cui.Diff;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Blame;

interface IBlameView
{
    void Show(Server.Blame blame, string repoPath);
}

// Shows which commit last changed each line of a file. Consecutive lines of the same commit are
// bracketed into one run in the gutter and the commit is named once per run, which is what makes
// this readable where the console 'git blame' is not.
class BlameView : IBlameView
{
    readonly IBlameService blameService;
    readonly IServer server;
    readonly IProgress progress;
    readonly IDiffView diffView;
    readonly IClipboardService clipboard;

    ContentView contentView = null!;
    UILabel header = null!;
    Server.Blame blame = null!;
    BlameRows blameRows = new BlameRows();
    string repoPath = "";
    int rowStartX = 0;
    BlameDetails details = BlameDetails.Full;

    // Where the user came from, so a drill down into previous versions can be stepped back out of.
    // A stack within the one Show call rather than nested views, so Esc still means 'close'.
    readonly Stack<BlameState> backStack = new();

    record BlameState(string Path, string Reference, int Index, int RowStartX);

    public BlameView(
        IBlameService blameService,
        IServer server,
        IProgress progress,
        IDiffView diffView,
        IClipboardService clipboard
    )
    {
        this.blameService = blameService;
        this.server = server;
        this.progress = progress;
        this.diffView = diffView;
        this.clipboard = clipboard;
    }

    bool IsSelected => contentView.SelectCount > 0;

    BlameRow? CurrentRow =>
        contentView.CurrentIndex >= 0 && contentView.CurrentIndex < blameRows.Rows.Count
            ? blameRows.Rows[contentView.CurrentIndex]
            : null;

    public void Show(Server.Blame blame, string repoPath)
    {
        this.blame = blame;
        this.blameRows = blameService.ToBlameRows(blame);
        this.repoPath = repoPath;
        this.rowStartX = 0;
        this.details = BlameDetails.Full;
        this.backStack.Clear();

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
        var border = new Label(0, 1, new string('─', 500)) { ColorScheme = ColorSchemes.Border };

        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            IsShowCursor = false,
            IsScrollMode = false,
            IsCursorMargin = false,
            IsCustomShowSelection = true,
        };

        blameView.Add(header, border, contentView);
        RegisterShortcuts(contentView);

        SetHeader();
        contentView.SetNeedsDisplay();
        UI.RunDialog(blameView);
    }

    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.Esc, () => Application.RequestStop());
        // Both cases, they are separate keys and the lower case would otherwise fall through to
        // the log view below and quit the application, see the same note in DiffView
        view.RegisterKeyHandler(Key.Q, () => Application.RequestStop());
        view.RegisterKeyHandler(Key.q, () => Application.RequestStop());

        view.RegisterKeyHandler(Key.CursorLeft, OnMoveLeft);
        view.RegisterKeyHandler(Key.CursorRight, OnMoveRight);
        view.RegisterKeyHandler(Key.C | Key.CtrlMask, OnCopy);

        view.RegisterKeyHandler(Key.m, () => ShowMainMenu());
        view.RegisterKeyHandler(Key.i, CycleDetails);
        view.RegisterKeyHandler(Key.d, ShowLineCommitDiff);
        view.RegisterKeyHandler(Key.p, BlamePrevious);
        view.RegisterKeyHandler(Key.Backspace, Back);
        view.RegisterKeyHandler(Key.c, CopyLineSha);

        view.RegisterMouseHandler(MouseFlags.Button1Pressed, (x, y) => OnMouseClick(y));
        view.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => ShowMainMenu(x - 1, y - 1));
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstRow, int rowCount, int currentIndex, int contentWidth)
    {
        var cw = BlameColumns.Calculate(details, contentWidth, blameRows.Rows.Count);

        var rows = blameRows
            .Rows.Skip(firstRow)
            .Take(rowCount)
            .Select(
                (r, i) =>
                    blameService.ToRowText(
                        r,
                        cw,
                        rowStartX,
                        i + firstRow == currentIndex,
                        contentView.IsRowSelected(i + firstRow)
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

    // Shows the commit that last changed the current line. The uncommitted lines carry the all '0'
    // sha, which the server routes to the uncommitted diff, so they need no special case here.
    async void ShowLineCommitDiff()
    {
        var row = CurrentRow;
        if (row == null)
            return;

        Server.CommitDiff? diff;
        using (progress.Show())
        {
            if (!Try(out diff, out var e, await server.GetCommitDiffAsync(row.Commit.Id, repoPath)))
            {
                UI.ErrorMessage($"Failed to get diff\n{e.AllErrorMessages()}");
                return;
            }
        }

        // A read only view two dialogs deep cannot usefully act on the result, so it is ignored
        diffView.Show(diff!, row.Commit.Id, repoPath);
        contentView.SetNeedsDisplay();
    }

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
        Server.Blame? newBlame;
        using (progress.Show())
        {
            if (!Try(out newBlame, out var e, await server.GetBlameAsync(path, reference, repoPath)))
            {
                UI.ErrorMessage($"Failed to blame {path}\n{e.AllErrorMessages()}");
                return false;
            }
        }

        blame = newBlame!;
        blameRows = blameService.ToBlameRows(blame);
        rowStartX = startX;
        contentView.MoveToTop();
        contentView.SetCurrentIndex(Math.Min(index, Math.Max(0, blameRows.Rows.Count - 1)));
        SetHeader();
        contentView.SetNeedsDisplay();
        return true;
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

        if (!Try(out var e, clipboard.Set(text)))
            UI.ErrorMessage(e.AllErrorMessages());

        contentView.ClearSelection();
    }

    void CopyLineSha()
    {
        var row = CurrentRow;
        if (row == null || row.Commit.IsUncommitted)
            return;

        if (!Try(out var e, clipboard.Set(row.Commit.Id)))
            UI.ErrorMessage(e.AllErrorMessages());
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
                    c == null ? "Commit Diff ..." : $"Commit Diff of {(c.IsUncommitted ? "uncommitted" : c.Sid)} ...",
                    "D",
                    () => ShowLineCommitDiff(),
                    () => c != null
                )
                .Item(
                    hasPrevious ? $"Blame Previous Version ({c!.PreviousId.Sid()}) ..." : "Blame Previous Version ...",
                    "P",
                    () => BlamePrevious(),
                    () => hasPrevious
                )
                .Item("Back", "Backspace", () => Back(), () => backStack.Count > 0)
                .Separator()
                .SubMenu("Scroll to Commit", "", GetScrollToItems())
                .Item($"Gutter Detail ({details}) ...", "I", () => CycleDetails())
                .Item("Reset Horizontal Scroll", "", () => ResetScroll(), () => rowStartX > 0)
                .Separator()
                .Item("Copy Selected Lines", "Ctrl-C", () => OnCopy(), () => IsSelected)
                .Item("Copy Commit Sha of Line", "C", () => CopyLineSha(), () => c != null && !c.IsUncommitted)
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

        contentView.SetCurrentIndex(index);
        contentView.ScrollToShowIndex(index);
    }

    void ResetScroll()
    {
        rowStartX = 0;
        contentView.SetNeedsDisplay();
    }
}
