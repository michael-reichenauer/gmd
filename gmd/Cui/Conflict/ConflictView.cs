using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Conflict;

interface IConflictView
{
    // The file is loaded by the caller, which is what keeps this synchronous: a Terminal.Gui
    // SynchronizationContext posts an await's continuation to the main loop, so blocking the main
    // loop on a Task here would deadlock rather than merely stall.
    bool Show(ConflictFile file, GitOperation operation, string repoPath);
}

// Resolves the conflicts of one file: the two sides (and the common ancestor, on request) side by
// side, one decision per conflict, and the result of the one under the cursor in a pane below.
//
// The columns are titled with the names git wrote into the markers rather than 'ours' and 'theirs',
// because during a rebase those two mean the opposite of what is expected — see ConflictRowService.
class ConflictView : IConflictView
{
    // How many rows the result pane takes when it is open
    const int ResultHeight = 8;

    readonly IServer server;
    readonly IProgress progress;
    readonly IConflictRowService rowService;

    ContentView contentView = null!;
    ContentView resultView = null!;
    UILabel header = null!;
    ConflictFile file = null!;
    ConflictResolution resolution = null!;
    ConflictRows rows = new ConflictRows();
    GitOperation operation;
    string repoPath = "";
    int rowStartX;
    bool isShowBase;
    bool isResolved;

    public ConflictView(IServer server, IProgress progress, IConflictRowService rowService)
    {
        this.server = server;
        this.progress = progress;
        this.rowService = rowService;
    }

    // The conflict the cursor is in, or the nearest one before it, so the commands and the result
    // pane always act on something even when the cursor is in the text between two conflicts
    int CurrentHunk
    {
        get
        {
            for (int i = Math.Min(contentView.CurrentIndex, rows.Count - 1); i >= 0; i--)
            {
                if (rows.Rows[i].HunkIndex >= 0)
                    return rows.Rows[i].HunkIndex;
            }

            return resolution.HunkCount > 0 ? 0 : -1;
        }
    }

    public bool Show(ConflictFile file, GitOperation operation, string repoPath)
    {
        this.file = file;
        this.operation = operation;
        this.repoPath = repoPath;
        this.rowStartX = 0;
        this.isShowBase = false;
        this.isResolved = false;
        this.resolution = new ConflictResolution(file);

        // A file with nothing to merge as text is a whole file choice, not a three pane view: a
        // three pane view with nothing in it reads as gmd being broken
        if (file.IsBinary || file.Hunks.Count == 0)
            return ShowWholeFileChoice();

        Toplevel view = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        header = new UILabel(0, 0);
        var border = new Label(0, 1, new string('─', 500)) { ColorScheme = ColorSchemes.Border };

        contentView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(ResultHeight + 1),
            IsShowCursor = false,
            IsScrollMode = false,
            IsCursorMargin = false,
            IsHighlightCurrentIndex = true,
        };

        // Not the Label(x, y, text) constructor: that fixes an absolute frame, so a Pos.AnchorEnd
        // assigned afterwards is ignored and the rule is drawn at the top of the view instead
        var resultBorder = new Label(new string('─', 500))
        {
            X = 0,
            Y = Pos.AnchorEnd(ResultHeight + 1),
            ColorScheme = ColorSchemes.Border,
        };
        resultView = new ContentView(OnGetResultContent)
        {
            X = 0,
            Y = Pos.AnchorEnd(ResultHeight),
            Width = Dim.Fill(),
            Height = ResultHeight,
            IsShowCursor = false,
            IsScrollMode = true,
            IsCursorMargin = false,
            IsFocus = false,
        };

        view.Add(header, border, contentView, resultBorder, resultView);
        RegisterShortcuts(contentView);

        // The result pane follows the cursor, so walking the file walks through the decisions
        contentView.CurrentIndexChange += OnCurrentIndexChange;

        SetRows();
        UI.RunDialog(view);

        return isResolved;
    }

    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.Esc, Close);
        // Both cases, as the diff and blame views do — the lower case would otherwise fall through
        // to the log view below and quit the application
        view.RegisterKeyHandler(Key.Q, Close);
        view.RegisterKeyHandler(Key.q, Close);

        // Terminal.Gui 1.x has no Key value for these, so use the ascii code, as the diff view's
        // '+' and '-' do
        view.RegisterKeyHandler((Key)49, () => Choose(HunkChoice.Ours)); // '1'
        view.RegisterKeyHandler((Key)50, () => Choose(HunkChoice.Theirs)); // '2'
        view.RegisterKeyHandler((Key)51, () => Choose(HunkChoice.OursThenTheirs)); // '3'
        view.RegisterKeyHandler((Key)52, () => Choose(HunkChoice.TheirsThenOurs)); // '4'
        view.RegisterKeyHandler((Key)48, () => Choose(HunkChoice.Neither)); // '0'
        view.RegisterKeyHandler(Key.u, () => Choose(HunkChoice.None));

        view.RegisterKeyHandler((Key)93, () => GotoHunk(1)); // ']'
        view.RegisterKeyHandler((Key)91, () => GotoHunk(-1)); // '['
        view.RegisterKeyHandler(Key.n, () => GotoHunk(1));
        view.RegisterKeyHandler(Key.p, () => GotoHunk(-1));

        view.RegisterKeyHandler(Key.b, ToggleBase);
        view.RegisterKeyHandler(Key.s, Save);
        view.RegisterKeyHandler(Key.a, ShowWholeFileMenu);
        view.RegisterKeyHandler(Key.m, () => ShowMainMenu());
        view.RegisterKeyHandler(Key.CursorLeft, () => Scroll(-1));
        view.RegisterKeyHandler(Key.CursorRight, () => Scroll(1));

        view.RegisterMouseHandler(MouseFlags.Button1Pressed, (x, y) => OnMouseClick(y));
        view.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => ShowMainMenu(x - 1, y - 1));
    }

    void SetRows()
    {
        rows = rowService.ToRows(file, resolution, isShowBase);
        SetHeader();
        contentView.SetNeedsDisplay();
        resultView.SetNeedsDisplay();
    }

    void SetHeader()
    {
        header.Text = rowService.ToHeader(file, resolution, CurrentHunk, operation);
        // The Text setter sizes the label from the text it replaces, so a longer header is clipped
        header.Width = Dim.Fill();
    }

    void OnCurrentIndexChange()
    {
        SetHeader();
        resultView.SetNeedsDisplay();
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstRow, int rowCount, int currentIndex, int contentWidth)
    {
        var columns = ConflictColumns.Calculate(contentWidth, isShowBase);
        var texts = rows.Rows.Skip(firstRow).Take(rowCount).Select(r => rowService.ToRowText(r, columns, rowStartX));

        return (texts, rows.Count);
    }

    // What the conflict under the cursor currently resolves to, which is the whole point of the
    // pane: the decision is visible as text rather than only as a word in the header
    (IEnumerable<Text> rows, int total) OnGetResultContent(int firstRow, int rowCount, int _, int __)
    {
        var hunk = CurrentHunkOrNull();
        if (hunk == null)
            return ([Text.Dark("No conflict here")], 1);

        if (resolution.ChoiceOf(hunk.Index) == HunkChoice.None)
            return ([Text.Dark($"Conflict {hunk.Index + 1} is not resolved yet — press 1, 2, 3, 4 or 0")], 1);

        var lines = resolution.ResultOf(hunk);
        if (lines.Count == 0)
            return ([Text.Dark("(nothing — the conflict is dropped)")], 1);

        var texts = lines.Skip(firstRow).Take(rowCount).Select(l => (Text)Text.White(l.Text));
        return (texts, lines.Count);
    }

    ConflictHunk? CurrentHunkOrNull()
    {
        var index = CurrentHunk;
        return index >= 0 && index < file.Hunks.Count ? file.Hunks[index] : null;
    }

    void Choose(HunkChoice choice)
    {
        var hunk = CurrentHunkOrNull();
        if (hunk == null)
            return;

        resolution.Set(hunk.Index, choice);
        SetRows();
    }

    // Moves to the next or previous conflict, keeping the cursor on its header row
    void GotoHunk(int step)
    {
        var next = resolution.NextHunk(CurrentHunk, step);
        if (next == -1)
            return;

        var index = rows.IndexOfHunk(next);
        if (index == -1)
            return;

        // Scroll first and set the cursor after: scrolling moves the cursor by the same amount as
        // the rows, so setting it first would leave it somewhere else entirely
        contentView.ScrollToShowIndex(index);
        contentView.SetCurrentIndex(index);
    }

    // The common ancestor is fetched the first time it is asked for rather than when the view opens:
    // recovering it costs five git commands, and most conflicts are settled without ever looking at
    // it. Once fetched it stays on the file, so toggling it off and on again is free.
    void ToggleBase()
    {
        if (isShowBase)
        {
            isShowBase = false;
            SetRows();
            return;
        }

        if (file.Hunks.Any(h => h.HasBase))
        {
            isShowBase = true;
            SetRows();
            return;
        }

        UI.RunInBackground(async () =>
        {
            ConflictFile withBase;
            using (progress.Show())
            {
                var result = await server.GetConflictFileAsync(file.Path, file.Kind, true, repoPath);
                if (!Try(out withBase!, out var e, result))
                {
                    UI.ErrorMessage($"Failed to get the common ancestor\n{e.AllErrorMessages()}");
                    return;
                }
            }

            // The decisions are held by conflict number, so a file that gained or lost conflicts
            // while the resolver was open would silently re-point them at different conflicts
            if (withBase.Hunks.Count != file.Hunks.Count)
            {
                UI.ErrorMessage(
                    $"{file.Path} has changed on disk since it was opened.\n\nClose the resolver and open it again."
                );
                return;
            }

            if (!withBase.Hunks.Any(h => h.HasBase))
            {
                UI.InfoMessage(
                    "No Common Ancestor",
                    $"{file.Path} has no common ancestor to show.\n\n"
                        + "This happens when both sides added the file, so there is no earlier\n"
                        + "version of it that they both started from."
                );
                return;
            }

            file = withBase;
            isShowBase = true;
            SetRows();
        });
    }

    void Scroll(int step)
    {
        rowStartX = Math.Max(0, rowStartX + step);
        contentView.SetNeedsDisplay();
    }

    bool OnMouseClick(int y)
    {
        contentView.SetIndexAtViewY(y - 2);
        contentView.SetNeedsDisplay();
        return false;
    }

    // Writes the decisions and marks the file resolved. Nothing is written until this is pressed,
    // so a resolver that is closed without saving has changed nothing on disk.
    void Save() =>
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                var result = await server.ResolveConflictFileAsync(
                    file.Path,
                    file.Kind,
                    resolution.ToResolutions(),
                    repoPath
                );
                if (!Try(out var e, result))
                {
                    UI.ErrorMessage($"Failed to resolve {file.Path}\n{e.AllErrorMessages()}");
                    return;
                }
            }

            isResolved = true;
            if (resolution.IsFullyResolved)
            {
                Application.RequestStop();
                return;
            }

            UI.InfoMessage(
                "Saved",
                $"{file.Path} was saved and marked resolved, but {resolution.UnresolvedCount} of its "
                    + $"conflicts still have their markers in it."
            );
        });

    void Close()
    {
        if (resolution.IsFullyResolved || !resolution.IsChanged)
        {
            if (!resolution.IsFullyResolved && resolution.HunkCount > 0 && !ConfirmLeaveUnresolved())
                return;

            Application.RequestStop();
            return;
        }

        var choice = UI.InfoMessage(
            "Unsaved Decisions",
            $"{resolution.HunkCount - resolution.UnresolvedCount} of {resolution.HunkCount} conflicts "
                + "have been decided but not saved.\n\nNothing has been written to the file yet.",
            0,
            ["Stay", "Save and Close", "Discard and Close"]
        );
        if (choice == 0)
            return;
        if (choice == 1)
        {
            Save();
            return;
        }

        Application.RequestStop();
    }

    bool ConfirmLeaveUnresolved() =>
        UI.InfoMessage(
            "Unresolved Conflicts",
            $"{resolution.UnresolvedCount} of {resolution.HunkCount} conflicts are still unresolved.",
            0,
            ["Stay", "Close Anyway"]
        ) == 1;

    // A file with no text to merge is resolved whole, and what "whole" means depends on the kind.
    // A modify/delete has only one side in the index — 'git checkout --theirs' on a UD path answers
    // "does not have their version" — so offering both sides there would be an error every time.
    // The choice is keep it or accept the deletion.
    bool ShowWholeFileChoice()
    {
        if (IsDeleteConflict(file.Kind))
            return ShowDeleteChoice();

        var choice = UI.InfoMessage(
            $"Resolve {file.Path}",
            $"{file.Path} {WhyWholeFile()}, so it is resolved as a whole file.",
            2,
            ["Use Ours", "Use Theirs", "Cancel"]
        );

        if (choice == 0)
            Run(() => server.UseWholeFileAsync(file.Path, true, repoPath));
        else if (choice == 1)
            Run(() => server.UseWholeFileAsync(file.Path, false, repoPath));
        else
            return false;

        return true;
    }

    bool ShowDeleteChoice()
    {
        var choice = UI.InfoMessage(
            $"Resolve {file.Path}",
            DeleteQuestion(),
            2,
            ["Keep the File", "Delete It", "Cancel"]
        );

        if (choice == 0)
            Run(() => server.KeepConflictedFileAsync(file.Path, repoPath));
        else if (choice == 1)
            Run(() => server.DeleteConflictedAsync(file.Path, repoPath));
        else
            return false;

        return true;
    }

    string WhyWholeFile() => file.IsBinary ? "is binary" : "has no text conflicts";

    // Which side did what, since 'ours' and 'theirs' is exactly the wrong way round to say it here
    string DeleteQuestion() =>
        file.Kind switch
        {
            ConflictKind.DeletedByThem => $"{file.Path} was changed here but deleted on the other side.",
            ConflictKind.DeletedByUs => $"{file.Path} was deleted here but changed on the other side.",
            ConflictKind.BothDeleted => $"{file.Path} was deleted on both sides, but not identically.",
            _ => $"{file.Path} has no text to merge.",
        } + "\n\nKeep it, or accept the deletion?";

    static bool IsDeleteConflict(ConflictKind kind) =>
        kind is ConflictKind.DeletedByUs or ConflictKind.DeletedByThem or ConflictKind.BothDeleted;

    void ShowWholeFileMenu()
    {
        Menu.Show(
            $"Whole File",
            Menu.Center,
            2,
            Menu.Items.Item($"Use {OursLabel()} for the Whole File", "", () => UseWholeFile(true))
                .Item($"Use {TheirsLabel()} for the Whole File", "", () => UseWholeFile(false))
                .Separator()
                .Item("Un-resolve This File", "", Unresolve)
        );
    }

    string OursLabel() => file.Hunks.Count > 0 ? file.Hunks[0].OursLabel : "ours";

    string TheirsLabel() => file.Hunks.Count > 0 ? file.Hunks[0].TheirsLabel : "theirs";

    void UseWholeFile(bool isOurs)
    {
        isResolved = true;
        Run(() => server.UseWholeFileAsync(file.Path, isOurs, repoPath), () => Application.RequestStop());
    }

    // Puts the conflict back as git left it, discarding whatever was resolved
    void Unresolve()
    {
        if (UI.InfoMessage("Un-resolve", $"Put the conflicts back into {file.Path}?", 1, ["Yes", "No"]) != 0)
            return;

        isResolved = true;
        Run(() => server.UnresolveAsync(file.Path, repoPath), () => Application.RequestStop());
    }

    // Every git call goes through here rather than being awaited on the main loop, which a
    // Terminal.Gui SynchronizationContext would deadlock
    void Run(Func<Task<R>> action, Action? onDone = null) =>
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await action()))
                {
                    UI.ErrorMessage($"Failed\n{e.AllErrorMessages()}");
                    return;
                }
            }

            onDone?.Invoke();
        });

    void ShowMainMenu(int x = Menu.Center, int y = 0)
    {
        var hunk = CurrentHunkOrNull();
        var ours = hunk?.OursLabel ?? "ours";
        var theirs = hunk?.TheirsLabel ?? "theirs";
        var hasHunk = hunk != null;

        Menu.Show(
            "Resolve Menu",
            x,
            y,
            Menu.Items.Separator(hasHunk ? $"Conflict {hunk!.Index + 1} of {resolution.HunkCount}" : "No conflict here")
                .Item($"Use {ours}", "1", () => Choose(HunkChoice.Ours), () => hasHunk)
                .Item($"Use {theirs}", "2", () => Choose(HunkChoice.Theirs), () => hasHunk)
                .Item($"Use {ours} then {theirs}", "3", () => Choose(HunkChoice.OursThenTheirs), () => hasHunk)
                .Item($"Use {theirs} then {ours}", "4", () => Choose(HunkChoice.TheirsThenOurs), () => hasHunk)
                .Item("Use Neither", "0", () => Choose(HunkChoice.Neither), () => hasHunk)
                .Item("Un-choose", "U", () => Choose(HunkChoice.None), () => hasHunk)
                .Separator()
                .Item("Next Conflict", "], N", () => GotoHunk(1), () => resolution.NextHunk(CurrentHunk, 1) != -1)
                .Item("Previous Conflict", "[, P", () => GotoHunk(-1), () => resolution.NextHunk(CurrentHunk, -1) != -1)
                .Item(isShowBase ? "Hide Common Ancestor" : "Show Common Ancestor", "B", ToggleBase)
                .Separator()
                .Item("Save and Mark Resolved", "S", Save)
                .SubMenu("Whole File", "A", WholeFileItems())
                .Item("Close", "Esc", Close)
        );
    }

    IEnumerable<Common.MenuItem> WholeFileItems() =>
        Menu
            .Items.Item(
                !IsDeleteConflict(file.Kind),
                $"Use {OursLabel()} for the Whole File",
                "",
                () => UseWholeFile(true)
            )
            .Item(
                !IsDeleteConflict(file.Kind),
                $"Use {TheirsLabel()} for the Whole File",
                "",
                () => UseWholeFile(false)
            )
            .Separator()
            .Item("Un-resolve This File", "", Unresolve);
}
