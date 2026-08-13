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
    bool isMovedToFirstHunk;

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
        this.isMovedToFirstHunk = false;
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

        // Terminal.Gui 1.x has no Key value for these, so use the ascii code, as the diff view's
        // '+' and '-' do
        view.RegisterKeyHandler((Key)49, () => Choose(HunkChoice.Ours)); // '1'
        view.RegisterKeyHandler((Key)50, () => Choose(HunkChoice.Theirs)); // '2'
        view.RegisterKeyHandler((Key)51, () => Choose(HunkChoice.OursThenTheirs)); // '3'
        view.RegisterKeyHandler((Key)52, () => Choose(HunkChoice.TheirsThenOurs)); // '4'
        view.RegisterKeyHandler((Key)48, ChooseBase); // '0'

        // Every letter in both cases. Not politeness: an unhandled key falls through to the log
        // view below, where the upper case letters are commands of their own — 'U' pulls every
        // branch and 'P' pushes every branch, both of them things to do to a repository that is
        // *not* in the middle of a stopped merge. The menu and the help name these keys the way
        // shortcuts are always written, in upper case, so those are the ones a user presses.
        RegisterLetter(view, Key.q, Key.Q, Close);
        RegisterLetter(view, Key.u, Key.U, () => Choose(HunkChoice.None));
        RegisterLetter(view, Key.n, Key.N, () => GotoHunk(1));
        RegisterLetter(view, Key.p, Key.P, () => GotoHunk(-1));
        RegisterLetter(view, Key.e, Key.E, EditCurrentHunk);
        RegisterLetter(view, Key.b, Key.B, ToggleBase);
        RegisterLetter(view, Key.s, Key.S, () => Save());
        RegisterLetter(view, Key.a, Key.A, ShowWholeFileMenu);
        RegisterLetter(view, Key.m, Key.M, () => ShowMainMenu());

        view.RegisterKeyHandler((Key)93, () => GotoHunk(1)); // ']'
        view.RegisterKeyHandler((Key)91, () => GotoHunk(-1)); // '['

        view.RegisterKeyHandler(Key.CursorLeft, () => Scroll(-1));
        view.RegisterKeyHandler(Key.CursorRight, () => Scroll(1));

        view.RegisterMouseHandler(MouseFlags.Button1Pressed, (x, y) => OnMouseClick(y));
        view.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => ShowMainMenu(x - 1, y - 1));
    }

    static void RegisterLetter(ContentView view, Key lower, Key upper, OnKeyCallback action)
    {
        view.RegisterKeyHandler(lower, action);
        view.RegisterKeyHandler(upper, action);
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
        MoveToFirstHunkOnce();

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
            return ([Text.Dark("(nothing — the whole region is removed)")], 1);

        var texts = lines.Skip(firstRow).Take(rowCount).Select(l => (Text)Text.White(l.Text));
        return (texts, lines.Count);
    }

    // The first conflict is what the file was opened for, and it can be a long way down a file that
    // is mostly text both sides agree on, so the view opens on it rather than at the top.
    //
    // Not done before the dialog runs: the scroll needs the view's height and its row count, and
    // neither exists until it has been laid out and asked for its first rows — which is this call.
    // The move is posted rather than done here, so it moves the rows after that draw rather than
    // during it.
    void MoveToFirstHunkOnce()
    {
        if (isMovedToFirstHunk)
            return;

        isMovedToFirstHunk = true;
        UI.Post(() => GotoRow(rows.IndexOfHunk(0)));
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
    void GotoHunk(int step) => GotoRow(rows.NextHunkRow(contentView.CurrentIndex, step));

    void GotoRow(int index)
    {
        if (index == -1)
            return;

        // Scroll first and set the cursor after: scrolling moves the cursor by the same amount as
        // the rows, so setting it first would leave it somewhere else entirely
        contentView.ScrollToShowIndex(index);
        contentView.SetCurrentIndex(index);
    }

    // Hand edits the result of one conflict, for the merge that is neither side but something of
    // both — and for dropping the region altogether, by emptying the box. A modal box rather than
    // editing in the view itself: Terminal.Gui's text view needs the real keyboard focus, and this
    // is the one arrangement of it the codebase already relies on — the commit dialog is the same
    // thing with a different label.
    void EditCurrentHunk()
    {
        var hunk = CurrentHunkOrNull();
        if (hunk == null)
            return;

        var width = Math.Min(100, Application.Driver.Cols - 8);
        var height = Math.Min(26, Application.Driver.Rows - 4);
        var dlg = new UIDialog($"Edit Conflict {hunk.Index + 1} of {file.Path}", width, height);

        dlg.AddLabel(1, 0, SeedNote(hunk));
        var edit = dlg.AddMultiLineInputView(1, 2, width - 4, height - 6, SeedText(hunk));

        if (!dlg.ShowOkCancel(edit))
            return;

        // RawText and not Text: the latter trims, and indentation is part of code
        resolution.Set(hunk.Index, HunkChoice.Manual, edit.RawText);
        SetRows();
    }

    // Says where the text in the box came from, so it is never a surprise
    string SeedNote(ConflictHunk hunk) =>
        resolution.ChoiceOf(hunk.Index) != HunkChoice.None
            ? "What this conflict resolves to now:"
            : $"Both sides, {hunk.OursLabel.Max(20)} first, to cut down to what you want:";

    string SeedText(ConflictHunk hunk)
    {
        // Something already chosen is what to start from, so '1' then 'e' means "ours, but tweaked"
        if (resolution.ChoiceOf(hunk.Index) != HunkChoice.None)
            return string.Join('\n', resolution.ResultOf(hunk).Select(l => l.Text));

        // Otherwise both sides, which is the usual starting point for a merge written by hand
        return string.Join('\n', hunk.Ours.Concat(hunk.Theirs).Select(l => l.Text));
    }

    void ToggleBase()
    {
        if (isShowBase)
        {
            isShowBase = false;
            SetRows();
            return;
        }

        WithBase(() =>
        {
            isShowBase = true;
            SetRows();
        });
    }

    // Resolves the conflict to the common ancestor, i.e. undoes what both sides did to it. The
    // ancestor is shown as it is taken if it is not on screen already: a decision made from text the
    // user cannot see is one they cannot check.
    void ChooseBase()
    {
        var hunk = CurrentHunkOrNull();
        if (hunk == null)
            return;

        WithBase(() =>
        {
            isShowBase = true;
            // By index rather than through Choose, since the file object is replaced by the one the
            // ancestor was recovered into, and the hunk above then belongs to the previous one
            resolution.Set(hunk.Index, HunkChoice.Base);
            SetRows();
        });
    }

    // Runs the action once the file has its common ancestor, recovering it first if it does not.
    // That is fetched when it is first asked for rather than when the view opens: it costs five git
    // commands, and most conflicts are settled without ever wanting it. Once fetched it stays on the
    // file, so asking again is free.
    void WithBase(Action onBase)
    {
        if (file.HasBase || file.Hunks.Any(h => h.HasBase))
        {
            onBase();
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

            // The file's own flag and not 'no hunk has one': a conflict where both sides added lines
            // has an empty ancestor, and a file can be nothing but those and still have an ancestor
            if (!withBase.HasBase)
            {
                UI.InfoMessage(
                    "No Common Ancestor",
                    $"{file.Path} has no common ancestor.\n\n"
                        + "This happens when both sides added the file, so there is no earlier\n"
                        + "version of it that they both started from."
                );
                return;
            }

            file = withBase;
            onBase();
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
    //
    // A save that leaves conflicts undecided writes their markers back, so the file on disk no
    // longer holds the conflicts this view was built from: the decided ones are stable text now,
    // and the rest have moved up to fill the numbers. The decisions are held by conflict number, so
    // the view is re-read rather than kept — without that a second save was refused with "the file
    // has changed on disk" and the only way on was to close the resolver and open it again.
    void Save(bool isClosing = false) =>
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
            var left = resolution.UnresolvedCount;
            if (left == 0)
            {
                Application.RequestStop();
                return;
            }

            UI.InfoMessage(
                "Saved",
                $"{file.Path} was saved and marked resolved, but {left} of its "
                    + $"conflicts still have their markers in it."
            );

            if (isClosing)
            {
                Application.RequestStop();
                return;
            }

            await ReloadAsync();
        });

    // Reads the file back off disk and starts again from what it holds now, which is what makes a
    // partly saved file go on being resolvable. The decisions are not carried over: the ones that
    // were saved are part of the file now, and the numbers of the ones that were not have changed.
    async Task ReloadAsync()
    {
        ConflictFile reread;
        using (progress.Show())
        {
            var result = await server.GetConflictFileAsync(file.Path, file.Kind, isShowBase, repoPath);
            if (!Try(out reread!, out var e, result))
            {
                // There is nothing left to show the file as, so staying open would show the one it
                // no longer is
                UI.ErrorMessage($"Failed to re-read {file.Path}\n{e.AllErrorMessages()}");
                Application.RequestStop();
                return;
            }
        }

        if (reread.Hunks.Count == 0)
        {
            Application.RequestStop();
            return;
        }

        file = reread;
        resolution = new ConflictResolution(file);

        // The ancestor is recovered per read, so a pane that cannot be filled is closed rather than
        // left drawing three empty columns
        isShowBase = isShowBase && (file.HasBase || file.Hunks.Any(h => h.HasBase));

        // The rows have moved, so where the cursor was means nothing — open on the first conflict
        // that is left, as the view did when it opened
        isMovedToFirstHunk = false;
        SetRows();
    }

    // Nothing is written until Save, so closing is what throws decisions away — which is asked
    // about whenever there are any, including when every conflict has been decided. That last case
    // used to close without a word, and it is the most likely moment to press Esc: the file looks
    // finished on screen, and all of it was lost.
    void Close()
    {
        if (resolution.IsChanged)
        {
            AskAboutUnsavedDecisions();
            return;
        }

        // Nothing decided, so there is nothing to lose — but leaving the file conflicted is still
        // worth a word, since the operation cannot be finished until it is resolved
        if (resolution.UnresolvedCount > 0 && !ConfirmLeaveUnresolved())
            return;

        Application.RequestStop();
    }

    void AskAboutUnsavedDecisions()
    {
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
            Save(isClosing: true);
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

    // A file with no text to merge is resolved whole. What can be offered depends on which sides
    // the index actually holds, not on the kind: 'git checkout --theirs' on a path the other side
    // deleted answers "does not have their version", so offering both sides would be an error every
    // time. A rename against a rename leaves a path with neither side, where the only thing left to
    // say is that it is gone.
    bool ShowWholeFileChoice()
    {
        if (!file.HasOurs && !file.HasTheirs)
            return ShowGoneChoice();

        if (file.HasOurs != file.HasTheirs)
            return ShowKeepOrDeleteChoice();

        return ShowSideChoice();
    }

    // Both sides exist, so the question is which one to take. A binary file is the usual case.
    bool ShowSideChoice()
    {
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

    // Only one side has the file, so it is kept as that side left it, or the deletion is accepted
    bool ShowKeepOrDeleteChoice()
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

    // Neither side has it. There is no version to keep, so 'keep' is not offered — it would run the
    // command that records the deletion, i.e. do the opposite of what it says.
    bool ShowGoneChoice()
    {
        var choice = UI.InfoMessage(
            $"Resolve {file.Path}",
            $"{file.Path} is gone on both sides, but they did not remove it the same way\n"
                + "— it was renamed on one side, or renamed differently on each.\n\n"
                + "There is no version of it left to keep.",
            1,
            ["Accept the Deletion", "Cancel"]
        );

        if (choice != 0)
            return false;

        Run(() => server.DeleteConflictedAsync(file.Path, repoPath));
        return true;
    }

    string WhyWholeFile() => file.IsBinary ? "is binary" : "has no text conflicts";

    // Which side did what, since 'ours' and 'theirs' is exactly the wrong way round to say it here
    string DeleteQuestion() =>
        file.Kind switch
        {
            ConflictKind.DeletedByThem => $"{file.Path} was changed here but deleted on the other side.",
            ConflictKind.DeletedByUs => $"{file.Path} was deleted here but changed on the other side.",
            ConflictKind.AddedByUs => $"{file.Path} was added here but is not on the other side.",
            ConflictKind.AddedByThem => $"{file.Path} was added on the other side but is not here.",
            _ => $"{file.Path} is only on one side.",
        } + "\n\nKeep it, or accept the deletion?";

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
                .Item("Use the Common Ancestor", "0", ChooseBase, () => hasHunk)
                .Item("Edit by Hand ...", "E", EditCurrentHunk, () => hasHunk)
                .Item("Un-choose", "U", () => Choose(HunkChoice.None), () => hasHunk)
                .Separator()
                .Item(
                    "Next Conflict",
                    "], N",
                    () => GotoHunk(1),
                    () => rows.NextHunkRow(contentView.CurrentIndex, 1) != -1
                )
                .Item(
                    "Previous Conflict",
                    "[, P",
                    () => GotoHunk(-1),
                    () => rows.NextHunkRow(contentView.CurrentIndex, -1) != -1
                )
                .Item(isShowBase ? "Hide Common Ancestor" : "Show Common Ancestor", "B", ToggleBase)
                .Separator()
                .Item("Save and Mark Resolved", "S", () => Save())
                .SubMenu("Whole File", "A", WholeFileItems())
                .Item("Close", "Esc", Close)
        );
    }

    IEnumerable<Common.MenuItem> WholeFileItems() =>
        Menu
            .Items.Item(file.HasOurs, $"Use {OursLabel()} for the Whole File", "", () => UseWholeFile(true))
            .Item(file.HasTheirs, $"Use {TheirsLabel()} for the Whole File", "", () => UseWholeFile(false))
            .Separator()
            .Item("Un-resolve This File", "", Unresolve);
}
