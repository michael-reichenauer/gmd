using gmd.Common.Spelling;
using Terminal.Gui;

namespace gmd.Cui.Common;

// A multi line text input view, where tab moves focus to the next control instead of being
// inserted into the text. With a SpellChecker set, misspelled words are drawn in red and F7 or
// Ctrl+G opens the suggestions for the misspelled word at or after the caret.
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

    bool IsSpellCheck => SpellChecker?.IsEnabled == true;

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
        return base.ProcessKey(keyEvent);
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
        var lines = SpellSpans
            .Lines(RawText)
            .Select(l => SpellScanner.Misspelled(SpellSpans.LineText(l), SpellChecker!.IsMisspelled))
            .ToList();
        var next = SpellSpans.NextFrom(lines, CurrentRow, CurrentColumn);
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
