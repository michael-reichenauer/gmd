using System.Drawing;
using gmd.Common.Spelling;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace gmd.Cui.Common;

// A multi line text input view, where tab moves focus to the next control instead of being
// inserted into the text. With a SpellChecker set, misspelled words are drawn in red and F7 or
// Ctrl+G opens the suggestions for the misspelled word at or after the caret. A right click or
// Shift+F10 opens the context menu, with those suggestions on top when on a misspelled word.
class UITextView : TextView
{
    // The spelling of the line being drawn, by reference: OnDrawNormalColor is called once per cell
    // and the line is drawn left to right, so the scan is done once per line per redraw. An edit
    // clears it, since an edited line keeps its identity.
    List<Cell>? spellLine;
    int spellIndex = -1;
    IReadOnlyList<WordSpan> spellSpans = [];

    readonly Typing typing;

    // Where the caret was when the view was last drawn, see RedrawIfCaretMoved
    Point drawnCaret;

    internal UITextView()
    {
        TabKeyAddsTab = false;
        this.WithScheme(() => ColorSchemes.TextField);
        typing = new Typing(SetNeedsDraw);
        ContentsChanged += (_, _) =>
        {
            spellLine = null;
            if (IsSpellCheck)
                typing.Edited();
        };
    }

    internal ISpellChecker? SpellChecker { get; set; }

    // Raised after each redraw, for what is drawn from this view's state elsewhere, e.g. the hint
    // under it that counts the red words
    internal event Action? Redrawn;

    // Raised for Esc, which the view then takes, where the dialog it is in would otherwise close on
    // it; with no handler the key goes on as any other
    internal event Action? Escaped;

    bool IsSpellCheck => SpellChecker?.IsEnabled == true;

    // The first column and the first row shown, of a view scrolled sideways or down
    internal int LeftColumn => Viewport.X;
    internal int TopRow => Viewport.Y;

    // Where the caret is, as the column and the row of the text
    public Point CursorPosition
    {
        get => InsertionPoint;
        set => InsertionPoint = value;
    }

    // The misspelled words drawn red, i.e. all but the one being typed
    internal int MisspelledCount =>
        MisspelledLines().Select((spans, row) => spans.Count(s => !(row == CurrentRow && IsBeingTyped(s)))).Sum();

    protected override bool OnKeyDown(Key key)
    {
        AfterInput();
        if (key == Key.Esc && Escaped != null)
        {
            Escaped();
            return true;
        }
        if (IsSpellCheck && SpellSpans.IsSpellKey(key))
        {
            ShowSpellingSuggestions();
            return true;
        }
        if (TextContextMenu.IsMenuKey(key))
        {
            ShowContextMenu(CurrentColumn - LeftColumn, CurrentRow - TopRow);
            return true;
        }
        return base.OnKeyDown(key);
    }

    // A right click opens the context menu in place of the one Terminal.Gui would open on it, and
    // the press and release it is made of are swallowed too, so the base view sees none of it
    protected override bool OnMouseEvent(Mouse ev)
    {
        var at = ev.Position ?? Point.Empty;
        if (ev.Flags.HasFlag(MouseFlags.RightButtonClicked))
        {
            if (!HasFocus)
                SetFocus();
            CursorPosition = new Point(LeftColumn + at.X, TopRow + at.Y); // The caret goes to the click, as with a left click
            ShowContextMenu(at.X, at.Y);
            return true;
        }
        if (ev.Flags.HasFlag(MouseFlags.RightButtonPressed) || ev.Flags.HasFlag(MouseFlags.RightButtonReleased))
            return true;
        AfterInput();
        return base.OnMouseEvent(ev);
    }

    // A word is colored as misspelled depending on where the caret is (one being typed is not), and
    // Terminal.Gui 2.x moves the caret without drawing the text anew, so once a key or a click is
    // done, the text is drawn anew if the caret moved
    void AfterInput()
    {
        if (IsSpellCheck && UI.HasApp)
            UI.Post(RedrawIfCaretMoved);
    }

    void RedrawIfCaretMoved()
    {
        if (CursorPosition != drawnCaret)
            SetNeedsDraw();
    }

    public new string Text
    {
        get => base.Text?.Trim() ?? "";
        set => base.Text = value;
    }

    // The text exactly as typed. Text trims, which is what a commit message wants and what code
    // does not: leading indentation and trailing blank lines are content there.
    public string RawText => base.Text ?? "";

    protected override bool OnDrawingContent(DrawContext? context)
    {
        drawnCaret = CursorPosition;
        var isDrawn = base.OnDrawingContent(context);
        Redrawn?.Invoke();
        return isDrawn;
    }

    // Terminal.Gui's hook for coloring a cell of a line as it is drawn
    protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
    {
        if (!IsSpellCheck)
        {
            base.OnDrawNormalColor(line, idxCol, idxRow);
            return;
        }

        if (spellLine == null || !ReferenceEquals(line, spellLine) || idxCol <= spellIndex)
        { // A new line, or a new pass over the same one
            spellLine = line;
            spellSpans = SpellScanner.Misspelled(SpellSpans.LineText(line), SpellChecker!.IsMisspelled);
        }
        spellIndex = idxCol;

        var span = SpellSpans.At(spellSpans, idxCol);
        if (span == null || IsBeingTyped(line, span))
        {
            base.OnDrawNormalColor(line, idxCol, idxRow);
            return;
        }

        SetAttribute(SpellSpans.MisspelledColor);
    }

    bool IsBeingTyped(List<Cell> line, WordSpan span) => ReferenceEquals(line, GetCurrentLine()) && IsBeingTyped(span);

    // Whether a word of the current line is being typed
    bool IsBeingTyped(WordSpan span) => SpellSpans.IsBeingTyped(span, CurrentColumn, HasFocus && typing.IsTyping);

    void ShowSpellingSuggestions()
    {
        var next = SpellSpans.NextFrom(MisspelledLines(), CurrentRow, CurrentColumn);
        if (next == null)
            return;
        var (row, span) = next.Value;

        // The menu is placed in screen coordinates, just under the word
        var origin = ViewportToScreen(Point.Empty);
        int x = span.Start - LeftColumn + origin.X;
        int y = row - TopRow + origin.Y + 1;
        var items = SpellSpans.MenuItems(SpellChecker!, span.Word, with => Replace(row, span, with), SetNeedsDraw);
        Menu.Show("Spelling", x, y, items);
    }

    // The context menu, placed just under the view position (x, y), the click or the caret, or
    // under the misspelled word when on one, where the F7 menu for it goes
    void ShowContextMenu(int x, int y)
    {
        var lines = MisspelledLines();
        int row = CurrentRow;
        var at = row < lines.Count ? SpellSpans.At(lines[row], CurrentColumn) : null;
        if (at != null)
            x = at.Start - LeftColumn;
        var spelling = TextContextMenu.SpellingItems(
            SpellChecker,
            at,
            lines.Any(l => l.Count > 0),
            with => Replace(row, at!, with),
            ShowSpellingSuggestions,
            SetNeedsDraw
        );

        var origin = ViewportToScreen(Point.Empty);
        var items = TextContextMenu.Items(spelling, this, () => SelectedLength > 0);
        Menu.Show(TextContextMenu.Title(at), x + origin.X, y + origin.Y + 1, items);
    }

    // The misspelled words of each line, or nothing at all when not spell checking
    List<IReadOnlyList<WordSpan>> MisspelledLines() =>
        !IsSpellCheck
            ? []
            : SpellSpans
                .Lines(RawText)
                .Select(l => SpellScanner.Misspelled(SpellSpans.LineText(l), SpellChecker!.IsMisspelled))
                .ToList();

    // Replaces the word as if retyped, which keeps the undo history and the scroll position
    void Replace(int row, WordSpan span, string with)
    {
        CursorPosition = new Point(span.End, row);
        for (int i = 0; i < span.Length; i++)
            DeleteCharLeft();
        InsertText(with);
        SetNeedsDraw();
    }
}
