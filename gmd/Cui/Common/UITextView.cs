using gmd.Common.Spelling;
using Terminal.Gui;

namespace gmd.Cui.Common;

// A multi line text input view, where tab moves focus to the next control instead of being
// inserted into the text. With a SpellChecker set, misspelled words are drawn in red and F7 or
// Ctrl+G opens the suggestions for the misspelled word at or after the caret. A right click or
// Shift+F10 opens the context menu, with those suggestions on top when on a misspelled word.
class UITextView : TextView
{
    // The spelling of the line being drawn, by reference: SetNormalColor is called once per rune
    // and Redraw walks each line left to right, so the scan is done once per line per redraw. An
    // edit clears it, since an edited line keeps its identity.
    List<Rune>? spellLine;
    int spellIndex = -1;
    IReadOnlyList<WordSpan> spellSpans = [];

    internal UITextView()
    {
        ContentsChanged += _ => spellLine = null;
    }

    internal ISpellChecker? SpellChecker { get; set; }

    // Raised after each redraw, for what is drawn from this view's state elsewhere, e.g. the hint
    // under it that counts the red words
    internal event Action? Redrawn;

    bool IsSpellCheck => SpellChecker?.IsEnabled == true;

    // The misspelled words drawn red, i.e. all but the one being typed
    internal int MisspelledCount =>
        MisspelledLines()
            .Select(
                (spans, row) =>
                    spans.Count(s => !(HasFocus && row == CurrentRow && SpellSpans.IsBeingTyped(s, CurrentColumn)))
            )
            .Sum();

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        if (keyEvent.Key == Key.Tab)
        { // Ensure tab sets focus on next control and not insert tab in text
            return false;
        }
        if (IsSpellCheck && SpellSpans.IsSpellKey(keyEvent.Key))
        {
            ShowSpellingSuggestions();
            return true;
        }
        if (TextContextMenu.IsMenuKey(keyEvent.Key))
        {
            ShowContextMenu(CurrentColumn - LeftColumn, CurrentRow - TopRow);
            return true;
        }
        return base.ProcessKey(keyEvent);
    }

    // A right click opens the context menu in place of the one Terminal.Gui would open on it, and
    // the press and release it is made of are swallowed too, so the base view sees none of it
    public override bool MouseEvent(MouseEvent ev)
    {
        if (ev.Flags.HasFlag(MouseFlags.Button3Clicked))
        {
            if (!HasFocus)
                SetFocus();
            CursorPosition = new Point(LeftColumn + ev.X, TopRow + ev.Y); // The caret goes to the click, as with a left click
            ShowContextMenu(ev.X, ev.Y);
            return true;
        }
        if (ev.Flags.HasFlag(MouseFlags.Button3Pressed) || ev.Flags.HasFlag(MouseFlags.Button3Released))
            return true;
        return base.MouseEvent(ev);
    }

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }

    // The text exactly as typed. Text trims, which is what a commit message wants and what code
    // does not: leading indentation and trailing blank lines are content there.
    public string RawText => base.Text?.ToString() ?? "";

    public override Border Border
    {
        get => new Border() { };
        set => base.Border = value;
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        Redrawn?.Invoke();
    }

    // Terminal.Gui's hook for coloring a rune of a line as it is drawn
    protected override void SetNormalColor(List<Rune> line, int idx)
    {
        if (!IsSpellCheck)
        {
            base.SetNormalColor(line, idx);
            return;
        }

        if (spellLine == null || !ReferenceEquals(line, spellLine) || idx <= spellIndex)
        { // A new line, or a new pass over the same one
            spellLine = line;
            spellSpans = SpellScanner.Misspelled(SpellSpans.LineText(line), SpellChecker!.IsMisspelled);
        }
        spellIndex = idx;

        var span = SpellSpans.At(spellSpans, idx);
        if (span == null || IsBeingTyped(line, span))
        {
            base.SetNormalColor(line, idx);
            return;
        }

        Driver.SetAttribute(SpellSpans.MisspelledColor);
    }

    bool IsBeingTyped(List<Rune> line, WordSpan span) =>
        HasFocus && ReferenceEquals(line, GetCurrentLine()) && SpellSpans.IsBeingTyped(span, CurrentColumn);

    void ShowSpellingSuggestions()
    {
        var next = SpellSpans.NextFrom(MisspelledLines(), CurrentRow, CurrentColumn);
        if (next == null)
            return;
        var (row, span) = next.Value;

        // The menu is placed in screen coordinates, just under the word
        var origin = ScreenToView(0, 0);
        int x = span.Start - LeftColumn - origin.X;
        int y = row - TopRow - origin.Y + 1;
        var items = SpellSpans.MenuItems(SpellChecker!, span.Word, with => Replace(row, span, with), SetNeedsDisplay);
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
            SetNeedsDisplay
        );

        var origin = ScreenToView(0, 0);
        var items = TextContextMenu.Items(spelling, this, () => SelectedLength > 0);
        Menu.Show(TextContextMenu.Title(at), x - origin.X, y - origin.Y + 1, items);
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
        SetNeedsDisplay();
    }
}
