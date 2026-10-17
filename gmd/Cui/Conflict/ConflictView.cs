using gmd.Common;
using gmd.Cui.App;
using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.Conflict;

// How the resolver of a file ended, which the workspace goes on from (ConflictWorkspace): closed,
// leaving the conflicts that are left; gone to another file, Path; done with the file, resolved or
// decided whole, e.g. a side taken for all of it; or, for a file with no text to merge, its question
// cancelled, the file passed over for now
enum ConflictEnd
{
    Closed,
    GoTo,
    Done,
    Skipped,
}

record ConflictOutcome(ConflictEnd End, string Path = "");

interface IConflictView
{
    // The file is loaded by the caller, which is what keeps this synchronous: a Terminal.Gui
    // SynchronizationContext posts an await's continuation to the main loop, so blocking the main
    // loop on a Task here would deadlock rather than merely stall. Whatever it writes is on disk by
    // the time it returns. sides are what git knows of the two sides, which name them
    // (ConflictNames), and files the workspace's conflicted files, listed beside the resolver on a
    // wide terminal, which } { and the list go to another of.
    ConflictOutcome Show(
        ConflictFile file,
        ConflictSides sides,
        GitOperation operation,
        string repoPath,
        ConflictFileList files
    );

    // For a file with no text to merge, which is resolved whole: asks how, and gives what does it,
    // null where the question was cancelled. Given rather than run, so that the workspace waits for
    // git before it reads what is left.
    Func<Task<Result>>? AskWholeFile(ConflictFile file, string repoPath);
}

// Resolves the conflicts of one file: the two sides (and the common ancestor, on request) side by
// side, one decision per conflict, and the result of the one under the cursor in a pane below.
//
// The columns are titled with the names git wrote into the markers rather than 'ours' and 'theirs',
// because during a rebase those two mean the opposite of what is expected — see ConflictRowService.
class ConflictView : IConflictView
{
    // How many rows the result pane takes, its title below the file included: about two fifths of the
    // view, and 8 at least, worked out as the view opens and again as the terminal is resized
    int resultHeight = MinResultHeight;
    const int MinResultHeight = 8;

    // A layout posted for the size the view was drawn at, which is not laid out while it is drawn
    bool isLayoutPosted;

    // The file as it will be saved, which the result pane shows, made again with every decision
    ConflictResult result = ConflictResult.None;

    readonly IServer server;
    readonly IProgress progress;
    readonly IConflictRowService rowService;

    ContentView contentView = null!;
    ContentView resultView = null!;
    readonly IHelpPage helpPage;
    UILabel header = null!;
    ConflictFile file = null!;
    ConflictSides sides = ConflictSides.None;
    ConflictNames names = null!;

    // Both sides' changes in one, for each conflict they can be had for, null where not, found once
    // per file read (ConflictCombine)
    readonly Dictionary<int, IReadOnlyList<CombinedLine>?> combinations = [];

    // What a key did or why it did nothing, said over the key hints, as in the diff
    IStatusLine status = new StatusLine();
    ConflictResolution resolution = null!;
    ConflictRows rows = new ConflictRows();
    GitOperation operation;
    string repoPath = "";
    int rowStartX;
    bool isShowBase;
    bool isMovedToFirstHunk;

    // The rows of the result at the cursor, which the result pane marks
    (int Start, int End) resultAtCursor;

    // How the view ended, which Show returns
    ConflictOutcome outcome = new(ConflictEnd.Closed);

    // The text of a conflict being typed by hand in the result pane, null when none is: the view it is
    // typed in, which conflict, what it started as and where that came from, which the pane's title
    // says, where it goes in the pane, the result with it, and the row the text is scrolled to, which
    // the gutter numbers its rows from
    Toplevel top = null!;
    UITextView? typingView;
    int typingHunk = -1;
    string typingSeed = "";
    string typingFrom = "";
    ConflictTyping typing = new(0, 0, 0, 0);
    ConflictResult typingResult = ConflictResult.None;
    int typingTopRow;

    // The workspace's conflicted files, listed beside the resolver, how wide, 0 when not, whether
    // the list has the keys, as Tab moves them there and back, and which file is selected in it
    ConflictFileList files = ConflictFileList.None;
    ContentView listView = null!;
    int listWidth;
    bool isListFocus;
    int listSelected;

    readonly Config config;

    public ConflictView(
        IServer server,
        IProgress progress,
        IConflictRowService rowService,
        IHelpPage helpPage,
        Config config
    )
    {
        this.helpPage = helpPage;
        this.config = config;
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

    public ConflictOutcome Show(
        ConflictFile file,
        ConflictSides sides,
        GitOperation operation,
        string repoPath,
        ConflictFileList files
    )
    {
        this.file = file;
        this.files = files;
        this.outcome = new(ConflictEnd.Closed);
        this.isListFocus = false;
        this.listSelected = Math.Max(0, files.IndexOfOpen);
        this.sides = sides;
        this.names = ConflictNames.Of(file, sides);
        this.combinations.Clear();
        this.status = new StatusLine();
        this.operation = operation;
        this.repoPath = repoPath;
        this.rowStartX = 0;
        this.isShowBase = false;
        this.isMovedToFirstHunk = false;
        this.resultAtCursor = (0, 0);
        this.rows = new ConflictRows();
        this.resolution = new ConflictResolution(file);

        // A file with nothing to merge as text is a whole file choice, not a three pane view: a
        // three pane view with nothing in it reads as gmd being broken. The workspace asks it.
        if (file.IsBinary || file.Hunks.Count == 0)
            return outcome;

        Toplevel view = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        top = view;
        typingView = null;

        header = new UILabel(0, 0);
        var border = new HorizontalLine()
        {
            X = 0,
            Y = 1,
            ColorScheme = ColorSchemes.Border,
        };

        // The keys worth knowing here on the bottom row, below the result pane, unless turned off
        var hintsHeight = HintsHeight;
        resultHeight = ResultHeightFor(Application.Driver.Rows);

        // Never focused, so the keys stay with the file, which hands them on while the list has them
        listWidth = ListWidth();
        listView = new ContentView(OnGetListContent)
        {
            X = 0,
            Y = 2,
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
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(resultHeight + hintsHeight),
            IsShowCursor = false,
            IsScrollMode = false,
        };

        // Its title row is the border above it
        resultView = new ContentView(OnGetResultContent)
        {
            X = listWidth,
            Y = Pos.AnchorEnd(resultHeight + hintsHeight),
            Width = Dim.Fill(),
            Height = resultHeight,
            TopRow = ResultTitle,
            IsShowCursor = false,
            IsScrollMode = true,
            IsFocus = false,
            // Shows the result only. Were it focusable, Tab (the toplevel's next-view key) or a
            // click would move the focus here, off the file, and every key after it would go nowhere
            CanFocus = false,
        };

        view.Add(header, border, listView, contentView, resultView);
        // The bar is there with the hints off too, for what a key did, as in the diff
        var hintBar = new KeyHintBar(
            () => typingView != null ? ConflictHints.Typing : ConflictHints.For(files.Files.Count > 1, IsListHidden),
            () => status.Current
        );
        view.Add(hintBar);
        hintBar.FollowStatus(status, config.ShowKeyHints, view);
        RegisterShortcuts(contentView);

        // The result pane follows the cursor, so walking the file walks through the decisions
        contentView.CurrentIndexChange += OnCurrentIndexChange;

        SetRows();
        UI.RunDialog(view);

        // What closes the view waits for git first, so what it changed is on disk by now
        return outcome;
    }

    // Ends the view, with how, for the workspace to go on from
    void End(ConflictOutcome how)
    {
        outcome = how;
        Application.RequestStop();
    }

    void RegisterShortcuts(ContentView view)
    {
        // Esc leaves the list of files for the file, and closes the resolver
        view.RegisterKeyHandler(
            Key.Esc,
            () =>
            {
                if (isListFocus)
                    ToggleListFocus();
                else
                    Close();
            }
        );

        // The next and the previous file still conflicted, as the diff's } and { go to a file, and the
        // list of them, which Tab moves the keys to and back; there up and down select a file, and
        // Enter goes to it
        view.RegisterKeyHandler((Key)125, () => GoToFile(files.NextOpen(1))); // '}'
        view.RegisterKeyHandler((Key)123, () => GoToFile(files.NextOpen(-1))); // '{'
        view.RegisterKeyHandler(Key.Tab, ToggleListFocus);
        view.RegisterKeyHandler(Key.CursorUp, () => MoveInList(-1));
        view.RegisterKeyHandler(Key.CursorDown, () => MoveInList(1));
        view.RegisterKeyHandler(
            Key.Enter,
            () =>
            {
                if (!isListFocus)
                    return false;
                GoToFile(files.Files[listSelected].Path);
                return true;
            }
        );

        // Terminal.Gui 1.x has no Key value for these, so use the ascii code, as the diff view's
        // '+' and '-' do
        view.RegisterKeyHandler((Key)49, () => Choose(HunkChoice.Ours)); // '1'
        view.RegisterKeyHandler((Key)50, () => Choose(HunkChoice.Theirs)); // '2'
        view.RegisterKeyHandler((Key)51, () => Choose(HunkChoice.OursThenTheirs)); // '3'
        view.RegisterKeyHandler((Key)52, () => Choose(HunkChoice.TheirsThenOurs)); // '4'
        view.RegisterKeyHandler((Key)48, ChooseBase); // '0'

        // Every letter in both cases: the menu and the help name these keys the way shortcuts are
        // always written, in upper case, so those are the ones a user presses. The view is modal (see
        // UI.RunDialog), so a key not registered here does nothing. It used to fall through to the
        // views below, where 'U' pulls every branch, 'P' pushes every branch and the diff's 'c'
        // closed the resolver without asking about the decisions made in it.
        view.RegisterLetterHandler(Key.q, Close);
        // The help, as in every view, at the part about this one
        view.RegisterKeyHandler((Key)'?', () => helpPage.ShowAsync(HelpPage.ConflictSection).RunInBackground());
        view.RegisterKeyHandler(Key.F1, () => helpPage.ShowAsync(HelpPage.ConflictSection).RunInBackground());
        view.RegisterLetterHandler(Key.u, () => Choose(HunkChoice.None));
        view.RegisterLetterHandler(Key.n, () => GotoHunk(1));
        view.RegisterLetterHandler(Key.p, () => GotoHunk(-1));
        view.RegisterLetterHandler(Key.e, EditCurrentHunk);
        view.RegisterLetterHandler(Key.c, Combine);
        view.RegisterLetterHandler(Key.b, ToggleBase);
        view.RegisterLetterHandler(Key.l, ToggleList);
        view.RegisterLetterHandler(Key.s, () => Save());
        view.RegisterLetterHandler(Key.a, ShowWholeFileMenu);
        view.RegisterLetterHandler(Key.m, () => ShowMainMenu());

        view.RegisterKeyHandler((Key)93, () => GotoHunk(1)); // ']'
        view.RegisterKeyHandler((Key)91, () => GotoHunk(-1)); // '['

        view.RegisterKeyHandler(Key.CursorLeft, () => Scroll(-1));
        view.RegisterKeyHandler(Key.CursorRight, () => Scroll(1));

        // The result pane never has the focus (see where it is made), so it is scrolled from here,
        // and by the mouse wheel over it: a result longer than the pane showed its first rows only
        view.RegisterKeyHandler(Key.CursorDown | Key.ShiftMask, () => resultView.Scroll(1));
        view.RegisterKeyHandler(Key.CursorUp | Key.ShiftMask, () => resultView.Scroll(-1));

        view.RegisterMouseHandler(MouseFlags.Button1Pressed, (x, y) => OnMouseClick(y));
        // At the screen row above the click, so the first item is beside it: the view is two rows down
        view.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => ShowMainMenu(x - 1, y + 1));
    }

    // The cursor stays on the line it was on: with the common ancestor shown or hidden, a conflict is
    // as tall as its longest side, so the row it was at could be in another conflict
    void SetRows()
    {
        var anchor = rows.Count > 0 ? rows.AnchorAt(contentView.CurrentIndex) : null;
        rows = rowService.ToRows(file, resolution, isShowBase, names);
        result = rowService.ToResult(file, resolution, names, h => CombinationOf(h) != null);
        // Counted now rather than as the views draw, for the cursor and the result to be put in the
        // rows that are, e.g. after b shows the ancestor
        contentView.SetTotalCount(rows.Count);
        resultView.SetTotalCount(result.Rows.Count);
        if (anchor != null && rows.IndexOf(anchor) is var index and not -1 && index != contentView.CurrentIndex)
            contentView.ShowCurrentIndex(index);

        SetHeader();
        FollowCursorInResult(); // What was decided, or how it was shown
        contentView.SetNeedsDisplay();
        resultView.SetNeedsDisplay();
        listView.SetNeedsDisplay(); // How far the file is
    }

    void SetHeader()
    {
        var text = rowService.ToHeader(file, resolution, CurrentHunk, operation).ToTextBuilder();
        if (files.Files.Count > 1)
            text.Dark($"   file {files.IndexOfOpen + 1} of {files.Files.Count}");
        header.Text = text;
        // The Text setter sizes the label from the text it replaces, so a longer header is clipped
        header.Width = Dim.Fill();
    }

    void OnCurrentIndexChange()
    {
        SetHeader();
        FollowCursorInResult();
    }

    // The result pane goes along with the cursor: at the same place in the file as it will be saved,
    // the line the cursor is on marked, or the lines of the conflict it is in, a third of the way down
    // the pane. It used to stay on the conflict the cursor was last in, and looked stuck while the
    // cursor went on through the text after it. Scrolled on its own, by Shift-↑↓ or the wheel, it is
    // back with the cursor at the next move.
    void FollowCursorInResult()
    {
        if (rows.Count == 0)
            return;

        resultAtCursor = result.RangeOf(rows.AnchorAt(contentView.CurrentIndex));
        var first = Math.Max(0, resultAtCursor.Start - resultView.ContentHeight / 3);
        resultView.Scroll(first - resultView.FirstIndex);
        resultView.SetNeedsDisplay();
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstRow, int rowCount, int currentIndex, int contentWidth)
    {
        MoveToFirstHunkOnce();
        FollowSize();

        // The cursor on the line number, as the diff draws it, while the keys are with the file
        var columns = ConflictColumns.Calculate(contentWidth, isShowBase);
        var texts = rows
            .Rows.Skip(firstRow)
            .Take(rowCount)
            .Select(
                (r, i) =>
                    firstRow + i == currentIndex && contentView.HasFocus
                        ? ConflictRowService.WithCursor(
                            rowService.ToRowText(r, columns, rowStartX),
                            r.GutterWidth,
                            ConflictRowService.CursorColor
                        )
                        : rowService.ToRowText(r, columns, rowStartX)
            );

        return (texts, rows.Count);
    }

    // The file as it will be saved, which is the whole point of the pane: the decisions are visible
    // as text, in the file around them, rather than only as words in the conflicts' headers, and an
    // undecided conflict says which keys decide it, naming the sides as the column titles do
    //
    // While a conflict is typed, the pane is the file around it, the text view laid over its rows
    (IEnumerable<Text> rows, int total) OnGetResultContent(int firstRow, int rowCount, int _, int width)
    {
        if (typingView == null)
        {
            var shown = result
                .Rows.Skip(firstRow)
                .Take(rowCount)
                .Select(
                    (r, i) =>
                        IsAtCursor(firstRow + i)
                            ? ConflictRowService.WithCursor(
                                r,
                                ConflictRowService.GutterWidth,
                                ConflictRowService.ResultCursorColor
                            )
                            : r
                );
            return (shown, result.Rows.Count);
        }

        var rows = typing.Rows(typingResult, typingHunk, typingView.TopRow);
        return (rows.Take(rowCount), rows.Count);
    }

    // A row of the result at the cursor, which is marked as the cursor's row above is
    bool IsAtCursor(int row) => row >= resultAtCursor.Start && row < resultAtCursor.End;

    // Goes to another file of the workspace, asking first about decisions not saved, as closing does
    void GoToFile(string path)
    {
        // A click on the list while typing is done typing, as a click on the sides is: the list takes
        // no focus, so the text view was never left, and the file was gone from with what was typed
        // thrown away unasked
        EndTyping();

        if (path == "")
        {
            status.Notice("No other file has conflicts left");
            return;
        }
        if (files.Resolved.Contains(path))
        {
            status.Notice($"{path} is resolved");
            return;
        }
        if (path == file.Path)
        {
            if (isListFocus)
                ToggleListFocus();
            return;
        }
        if (!resolution.IsChanged)
        {
            End(new(ConflictEnd.GoTo, path));
            return;
        }

        var choice = UI.InfoMessage(
            "Unsaved Decisions",
            $"{resolution.HunkCount - resolution.UnresolvedCount} of {resolution.HunkCount} conflicts of "
                + $"{file.Path} have been decided but not saved.\n\nNothing has been written to it yet.",
            0,
            ["Stay", "Save and Go", "Discard and Go"]
        );
        // Esc (-1) backs out of the question, i.e. stays
        if (choice is 0 or -1)
            return;
        if (choice == 1)
        {
            Save(new(ConflictEnd.GoTo, path));
            return;
        }
        End(new(ConflictEnd.GoTo, path));
    }

    // Shows or hides the list of files, l. Remembered for the files and the resolvers after, as the
    // diff's list is, since it is hidden for the width it takes
    void ToggleList()
    {
        if (files.WidthFor(Application.Driver.Cols) == 0)
        {
            status.Notice(WhyNoList());
            return;
        }

        config.Set(c => c.ShowConflictFileList = !c.ShowConflictFileList);
        SetLayout();
        status.Info(listWidth > 0 ? "File list shown: Tab moves into it, l hides it" : "File list hidden: l shows it");
    }

    // The keys' row at the bottom, there unless turned off
    int HintsHeight => config.ShowKeyHints ? 1 : 0;

    // How tall the result pane is at a terminal's height, about two fifths of what is below the header
    int ResultHeightFor(int terminalRows) => Math.Max(MinResultHeight, (terminalRows - 2 - HintsHeight) * 2 / 5);

    // How wide the list of files is: as wide as it can be at the terminal's width, unless hidden by l
    int ListWidth() => config.ShowConflictFileList ? files.WidthFor(Application.Driver.Cols) : 0;

    // Hidden by l, where it would be shown, which the hints then say l shows
    bool IsListHidden => listWidth == 0 && files.WidthFor(Application.Driver.Cols) > 0;

    string WhyNoList() =>
        IsListHidden ? "The file list is hidden: l shows it"
        : files.Files.Count == 0 ? "There are no files with conflicts to list"
        : $"The file list is beside the resolver on a terminal {ConflictFileList.MinTerminalWidth} columns wide or more";

    // The size the terminal calls for, once the view is drawn at a size other than it was laid out
    // for, e.g. the terminal resized: the result pane was as tall as when the view opened, and the
    // list as wide. Posted, since the views cannot be laid out again while they are drawn.
    void FollowSize()
    {
        if (isLayoutPosted || (ResultHeightFor(Application.Driver.Rows) == resultHeight && ListWidth() == listWidth))
            return;

        isLayoutPosted = true;
        UI.Post(() =>
        {
            isLayoutPosted = false;
            SetLayout();
        });
    }

    // Lays the panes out for the terminal's size and the list shown or not: the list at the left,
    // the file beside it, and the result pane below, the text being typed in it with it
    void SetLayout()
    {
        resultHeight = ResultHeightFor(Application.Driver.Rows);
        listWidth = ListWidth();
        if (listWidth == 0 && isListFocus)
            ToggleListFocus();

        listView.Width = listWidth;
        contentView.X = listWidth;
        contentView.Height = Dim.Fill(resultHeight + HintsHeight);
        resultView.X = listWidth;
        resultView.Y = Pos.AnchorEnd(resultHeight + HintsHeight);
        resultView.Height = resultHeight;
        top.LayoutSubviews();

        LayOutTyping();
        FollowCursorInResult();
        top.SetNeedsDisplay();
    }

    // Moves the keys to the list of files and back
    void ToggleListFocus()
    {
        if (listWidth == 0 && !isListFocus)
        {
            status.Notice(WhyNoList());
            return;
        }

        isListFocus = !isListFocus;
        listSelected = Math.Max(0, files.IndexOfOpen);
        listView.IsFocus = isListFocus;
        contentView.IsFocus = !isListFocus;
        listView.SetNeedsDisplay();
        contentView.SetNeedsDisplay();
    }

    // Selects the file above or below in the list, while it has the keys, true then; with the file
    // having them the key is left to it
    bool MoveInList(int step)
    {
        if (!isListFocus)
            return false;

        listSelected = Math.Clamp(listSelected + step, 0, files.Files.Count - 1);
        listView.ScrollToShowIndex(listSelected + 1, 1);
        listView.SetNeedsDisplay();
        return true;
    }

    // The list's title is its first row, the files after it
    void OnListClick(int y)
    {
        var index = listView.FirstIndex + y - 1;
        if (index >= 0 && index < files.Files.Count)
            GoToFile(files.Files[index].Path);
    }

    (IEnumerable<Text> rows, int total) OnGetListContent(int firstRow, int rowCount, int _, int contentWidth)
    {
        var progress = $"{resolution.HunkCount - resolution.UnresolvedCount} of {resolution.HunkCount}";
        var rows = files.Rows(contentWidth, listSelected, isListFocus, progress);
        return (rows.Skip(firstRow).Take(rowCount), rows.Count);
    }

    // The pane's title, its border, in the color of the conflicts' own: the file as it will be saved,
    // by its name, its path being on the header above, or what is typed in it, and the colors of
    // where its lines came from
    Text ResultTitle(int firstRow, int width)
    {
        var title =
            typingView != null
                ? Text.BrightMagenta("─── Typing ")
                    .Dark($"conflict {typingHunk + 1} by hand, {typingFrom}, Esc when done ")
                : Text.BrightMagenta("─── Result: ").White(Path.GetFileName(file.Path)).Dark(" as it will be saved ");
        var legend = Text.Dark(" ")
            .Yellow("▌")
            .Dark($"{names.Ours.Max(20)} ")
            .Cyan("▌")
            .Dark($"{names.Theirs.Max(20)} ")
            .Magenta("▌")
            .Dark("both or by hand ")
            .BrightMagenta("───");
        var fill = Math.Max(0, width - title.Length - legend.Length);
        return title.BrightMagenta(new string('─', fill)).Add(legend);
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

        contentView.ShowCurrentIndex(index);
    }

    // Types the result of one conflict by hand, for the merge that is neither side but something of
    // both, and for dropping the region altogether, by emptying it. Typed in the result pane, in the
    // file around it, the sides staying in sight above, which a box over them hid. It starts from what
    // the conflict resolves to now, or from both sides' changes combined, or from both sides, which the
    // pane's title says. Esc, Tab or a click on the sides is done, and what was typed is the
    // conflict's decision.
    void EditCurrentHunk()
    {
        var hunk = CurrentHunkOrNull();
        if (hunk == null || typingView != null)
            return;

        typingHunk = hunk.Index;
        typingSeed = SeedText(hunk);
        typingFrom = TypedFrom(hunk);
        typingTopRow = 0;
        typingView = new UITextView()
        {
            X = listWidth + ConflictRowService.GutterWidth,
            Width = Dim.Fill(),
            Text = typingSeed,
            ColorScheme = ColorSchemes.TextField,
        };
        typingView.ContentsChanged += _ => LayOutTyping();
        typingView.Redrawn += OnTypingRedrawn;
        typingView.KeyPress += e =>
        {
            if (e.KeyEvent.Key != Key.Esc)
                return;
            e.Handled = true;
            contentView.SetFocus(); // Done, as leaving it is
        };
        // Done however the keys leave it, Esc, Tab or a click on the sides
        typingView.Leave += _ => EndTyping();

        LayOutTyping();
        top.Add(typingView);
        typingView.SetFocus();
        typingView.CursorPosition = new Point(0, 0);
    }

    // Where the text goes in the pane, and the result with it typed, again with every change to it:
    // the lines after it are numbered as they will be, and the text grows as lines are added
    void LayOutTyping()
    {
        if (typingView == null)
            return;

        var withTyped = resolution.WithTyped(typingHunk, typingView.RawText);
        typingResult = rowService.ToResult(file, withTyped, names, h => CombinationOf(h) != null);
        typing = ConflictTyping.Of(
            typingResult.Starts[typingHunk],
            typingResult.Ends[typingHunk],
            typingResult.Rows.Count,
            typingView.Lines,
            resultView.ContentHeight
        );

        // The pane's rows are below its title
        typingView.X = listWidth + ConflictRowService.GutterWidth;
        typingView.Y = Pos.AnchorEnd(resultHeight + HintsHeight - 1 - typing.Above);
        typingView.Height = typing.TextHeight;
        top.LayoutSubviews();
        resultView.SetNeedsDisplay();
        typingView.SetNeedsDisplay();
    }

    // The caret scrolls the text, and the gutter numbers its rows from the row it is scrolled to
    void OnTypingRedrawn()
    {
        if (typingView == null || typingView.TopRow == typingTopRow)
            return;
        typingTopRow = typingView.TopRow;
        resultView.SetNeedsDisplay();
        typingView.SetNeedsDisplay();
    }

    // What was typed is the conflict's decision, unless nothing was, when it is left as it was
    void EndTyping()
    {
        if (typingView is not UITextView view)
            return;

        typingView = null;
        top.Remove(view);
        // RawText and not Text: the latter trims, and indentation is part of code
        var text = view.RawText;
        if (text != typingSeed)
        {
            resolution.Set(typingHunk, HunkChoice.Manual, text);
            status.Info($"Conflict {typingHunk + 1} is written by hand: u takes it back");
        }
        else
        {
            status.Info($"Conflict {typingHunk + 1} is as it was, nothing was typed");
        }

        contentView.SetFocus();
        SetRows();
        top.SetNeedsDisplay();
    }

    // Where the text typed from came from, which the pane's title says, so it is never a surprise
    string TypedFrom(ConflictHunk hunk) =>
        resolution.ChoiceOf(hunk.Index) != HunkChoice.None ? "from what it resolves to now"
        : CombinationOf(hunk) != null ? "from both sides' changes combined"
        : $"from both sides, {names.Ours.Max(20)} first, to cut down";

    string SeedText(ConflictHunk hunk)
    {
        // Something already chosen is what to start from, so '1' then 'e' means "ours, but tweaked"
        if (resolution.ChoiceOf(hunk.Index) != HunkChoice.None)
            return ConflictResolution.ToText(resolution.ResultOf(hunk).Select(l => l.Text));

        // Otherwise both sides' changes combined, where they can be, or both sides, which is the
        // usual starting point for a merge written by hand
        if (CombinationOf(hunk) is IReadOnlyList<CombinedLine> combined)
            return ConflictResolution.ToText(combined.Select(l => l.Text));
        return ConflictResolution.ToText(hunk.Ours.Concat(hunk.Theirs).Select(l => l.Text));
    }

    // Takes both sides' changes in one, c, where they touch no word in common (ConflictCombine): what
    // git could not merge for their being in lines that touch, e.g. a line added above a line the
    // other changed. Never taken by itself, only offered: the result shows it, and u takes it back.
    void Combine()
    {
        var hunk = CurrentHunkOrNull();
        if (hunk == null)
            return;

        if (CombinationOf(hunk) is not IReadOnlyList<CombinedLine> combined)
        {
            status.Notice(WhyNotCombine(hunk));
            return;
        }

        resolution.SetCombined(hunk.Index, combined);
        SetRows();
        status.Info($"Conflict {hunk.Index + 1} is both changes combined: u takes it back");
    }

    IReadOnlyList<CombinedLine>? CombinationOf(ConflictHunk hunk)
    {
        if (!combinations.TryGetValue(hunk.Index, out var combined))
        {
            // What each side changed is only known against the ancestor
            combined = file.HasBase || hunk.HasBase ? ConflictCombine.Of(hunk.Base, hunk.Ours, hunk.Theirs) : null;
            combinations[hunk.Index] = combined;
        }
        return combined;
    }

    string WhyNotCombine(ConflictHunk hunk) =>
        !file.HasBase && !hunk.HasBase
            ? "The common ancestor is not known, so neither is what each side changed: e edits it by hand"
            : "The two sides change the same words here, or write lines in one place: e edits it by hand";

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
                if (result is not ConflictFile read)
                {
                    UI.ErrorMessage($"Failed to get the common ancestor\n{result.Error.AllMessages()}");
                    return;
                }
                withBase = read;
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
            combinations.Clear();
            onBase();
        });
    }

    // Up to where the longest line ends at the right of a pane: with no bound, → went on into
    // blank panes for as long as it was held
    void Scroll(int step)
    {
        var pane = ConflictColumns.Calculate(contentView.ContentWidth, isShowBase).Pane;
        rowStartX = Math.Clamp(rowStartX + step, 0, Math.Max(0, rows.MaxLength - pane));
        contentView.SetNeedsDisplay();
    }

    // The y is the row in the view, which is below the header, so it put the cursor two rows above
    // the click when it took the header off again
    bool OnMouseClick(int y)
    {
        contentView.SetIndexAtViewY(y);
        contentView.SetNeedsDisplay();
        return false;
    }

    // Writes the decisions, and marks the file resolved once none is left undecided. Nothing is
    // written until this is pressed, so a resolver that is closed without saving has changed nothing
    // on disk.
    //
    // A save that leaves conflicts undecided writes their markers back, so the file on disk no
    // longer holds the conflicts this view was built from: the decided ones are stable text now,
    // and the rest have moved up to fill the numbers. The decisions are held by conflict number, so
    // the view is re-read rather than kept — without that a second save was refused with "the file
    // has changed on disk" and the only way on was to close the resolver and open it again.
    //
    // after is how the view ends once saved, e.g. gone to another file; with none, a file with
    // conflicts left goes on being resolved, and one with none is done.
    void Save(ConflictOutcome? after = null) =>
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
                if (result is Error e)
                {
                    UI.ErrorMessage($"Failed to resolve {file.Path}\n{e.AllMessages()}");
                    return;
                }
            }

            var left = resolution.UnresolvedCount;
            if (left == 0)
            {
                End(after ?? new(ConflictEnd.Done));
                return;
            }

            UI.InfoMessage(
                "Saved",
                $"{file.Path} was saved, with {left} of its conflicts still to resolve.\n"
                    + "It is marked resolved once none is left."
            );

            if (after != null)
            {
                End(after);
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
            var result = await server.GetConflictFileAsync(file.Path, file.Kind, true, repoPath);
            if (result is Error)
                result = await server.GetConflictFileAsync(file.Path, file.Kind, false, repoPath);
            if (result is not ConflictFile read)
            {
                // There is nothing left to show the file as, so staying open would show the one it
                // no longer is
                UI.ErrorMessage($"Failed to re-read {file.Path}\n{result.Error.AllMessages()}");
                End(new(ConflictEnd.Closed));
                return;
            }
            reread = read;
        }

        if (reread.Hunks.Count == 0)
        {
            End(new(ConflictEnd.Done));
            return;
        }

        file = reread;
        names = ConflictNames.Of(file, sides);
        combinations.Clear();
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

        End(new(ConflictEnd.Closed));
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
        // Esc (-1) backs out of the question, i.e. stays, rather than falling to Discard
        if (choice is 0 or -1)
            return;
        if (choice == 1)
        {
            Save(new(ConflictEnd.Closed));
            return;
        }

        End(new(ConflictEnd.Closed));
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
    public Func<Task<Result>>? AskWholeFile(ConflictFile file, string repoPath)
    {
        this.file = file;
        this.repoPath = repoPath;
        if (!file.HasOurs && !file.HasTheirs)
            return AskGone();
        else if (file.HasOurs != file.HasTheirs)
            return AskKeepOrDelete();
        else
            return AskSide();
    }

    // Both sides exist, so the question is which one to take. A binary file is the usual case.
    Func<Task<Result>>? AskSide()
    {
        if (!file.IsBinary)
            return AskHandResolved();

        var choice = UI.InfoMessage(
            $"Resolve {file.Path}",
            $"{file.Path} is binary, so it is resolved as a whole file.",
            2,
            ["Use Ours", "Use Theirs", "Cancel"]
        );

        return choice switch
        {
            0 => () => server.UseWholeFileAsync(file.Path, true, repoPath),
            1 => () => server.UseWholeFileAsync(file.Path, false, repoPath),
            _ => null,
        };
    }

    // A text file both sides changed with no markers left in it has been resolved by hand, outside
    // gmd, but not marked so. It is marked resolved as it is, or one side is taken after all: the two
    // sides were all that was offered, and either threw the hand merge away.
    Func<Task<Result>>? AskHandResolved()
    {
        var choice = UI.InfoMessage(
            $"Resolve {file.Path}",
            $"{file.Path} has no conflict markers left in it, as if resolved by hand.\n\n"
                + "Mark it resolved as it is, or take one side for the whole file?",
            3,
            ["Mark Resolved", "Use Ours", "Use Theirs", "Cancel"]
        );

        return choice switch
        {
            0 => () => server.KeepConflictedFileAsync(file.Path, repoPath),
            1 => () => server.UseWholeFileAsync(file.Path, true, repoPath),
            2 => () => server.UseWholeFileAsync(file.Path, false, repoPath),
            _ => null,
        };
    }

    // Only one side has the file, so it is kept as that side left it, or the deletion is accepted
    Func<Task<Result>>? AskKeepOrDelete()
    {
        var choice = UI.InfoMessage(
            $"Resolve {file.Path}",
            DeleteQuestion(),
            2,
            ["Keep the File", "Delete It", "Cancel"]
        );

        return choice switch
        {
            0 => () => server.KeepConflictedFileAsync(file.Path, repoPath),
            1 => () => server.DeleteConflictedAsync(file.Path, repoPath),
            _ => null,
        };
    }

    // Neither side has it. There is no version to keep, so 'keep' is not offered — it would run the
    // command that records the deletion, i.e. do the opposite of what it says.
    Func<Task<Result>>? AskGone()
    {
        var choice = UI.InfoMessage(
            $"Resolve {file.Path}",
            $"{file.Path} is gone on both sides, but they did not remove it the same way\n"
                + "— it was renamed on one side, or renamed differently on each.\n\n"
                + "There is no version of it left to keep.",
            1,
            ["Accept the Deletion", "Cancel"]
        );

        return choice == 0 ? () => server.DeleteConflictedAsync(file.Path, repoPath) : null;
    }

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

    // The main menu's Whole File items, which offer only the sides the index holds
    void ShowWholeFileMenu() => Menu.Show($"Whole File", Menu.Center, 2, WholeFileItems());

    void UseWholeFile(bool isOurs)
    {
        Run(() => server.UseWholeFileAsync(file.Path, isOurs, repoPath), () => End(new(ConflictEnd.Done)));
    }

    // Puts the conflict back as git left it, discarding whatever was resolved
    void Unresolve()
    {
        if (UI.InfoMessage("Unresolve", $"Put the conflicts back into {file.Path}?", 1, ["Yes", "No"]) != 0)
            return;

        // The same file again, as git left it
        Run(() => server.UnresolveAsync(file.Path, repoPath), () => End(new(ConflictEnd.GoTo, file.Path)));
    }

    // Every git call goes through here rather than being awaited on the main loop, which a
    // Terminal.Gui SynchronizationContext would deadlock
    void Run(Func<Task<Result>> action, Action? onDone = null) =>
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (await action() is Error e)
                {
                    UI.ErrorMessage($"Failed\n{e.AllMessages()}");
                    return;
                }
            }

            onDone?.Invoke();
        });

    void ShowMainMenu(int x = Menu.Center, int y = 0)
    {
        var hunk = CurrentHunkOrNull();
        var (ours, theirs) = (names.Ours, names.Theirs);
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
                .Item(
                    "Combine Both Changes",
                    "c",
                    Combine,
                    () => hunk != null && CombinationOf(hunk) != null,
                    () => hunk != null ? WhyNotCombine(hunk) : "No conflict here"
                )
                .Item("Use the Common Ancestor", "0", ChooseBase, () => hasHunk)
                .Item("Edit by Hand", "e", EditCurrentHunk, () => hasHunk)
                .Item("Clear Decision", "u", () => Choose(HunkChoice.None), () => hasHunk)
                .Separator()
                .Item(
                    "Next Conflict",
                    "], n",
                    () => GotoHunk(1),
                    () => rows.NextHunkRow(contentView.CurrentIndex, 1) != -1
                )
                .Item(
                    "Previous Conflict",
                    "[, p",
                    () => GotoHunk(-1),
                    () => rows.NextHunkRow(contentView.CurrentIndex, -1) != -1
                )
                .Item(isShowBase ? "Hide Common Ancestor" : "Show Common Ancestor", "b", ToggleBase)
                .Item(
                    listWidth > 0 ? "Hide File List" : "Show File List",
                    "l",
                    ToggleList,
                    () => files.WidthFor(Application.Driver.Cols) > 0,
                    WhyNoList
                )
                .Separator()
                .Item("Save and Mark Resolved", "s", () => Save())
                .SubMenu("Whole File", "a", WholeFileItems())
                .Item("Close", "Esc", Close)
        );
    }

    IEnumerable<Common.MenuItem> WholeFileItems() =>
        Menu
            .Items.Item(file.HasOurs, $"Use {names.Ours} for the Whole File", "", () => UseWholeFile(true))
            .Item(file.HasTheirs, $"Use {names.Theirs} for the Whole File", "", () => UseWholeFile(false))
            .Separator()
            .Item("Unresolve This File", "", Unresolve);
}
