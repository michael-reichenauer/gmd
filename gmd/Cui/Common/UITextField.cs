using gmd.Common.Spelling;
using Terminal.Gui;

namespace gmd.Cui.Common;

// A one line text input field, which unlike Terminal.Gui's TextField returns its text as a
// trimmed string rather than as a ustring. With a SpellChecker set, misspelled words are drawn in
// red and F7 or Ctrl+G opens the suggestions for the misspelled word at or after the caret.
class UITextField : TextField
{
    internal UITextField(int x, int y, int w, string text = "")
        : base(x, y, w, text)
    {
        ColorScheme = ColorSchemes.TextField;
    }

    internal ISpellChecker? SpellChecker { get; set; }

    bool IsSpellCheck => SpellChecker?.IsEnabled == true;

    public new string Text
    {
        get => base.Text?.ToString()?.Trim() ?? "";
        set => base.Text = value;
    }

    // The text exactly as typed, which is what the caret and the scroll offset are indexes into
    public string RawText => base.Text?.ToString() ?? "";

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        if (IsSpellCheck && SpellSpans.IsSpellKey(keyEvent.Key))
        {
            ShowSpellingSuggestions();
            return true;
        }
        return base.ProcessKey(keyEvent);
    }

    // TextField has no hook for coloring a rune as it is drawn, so the misspelled words are drawn
    // once more, in red, over what the base drew: nothing of that is deferred, so what is drawn
    // last is what shows. The caret cell is left as drawn, and a selection is left alone entirely.
    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        if (!IsSpellCheck || SelectedLength > 0)
            return;

        var runes = RawText.EnumerateRunes().Select(r => new Rune((uint)r.Value)).ToList();
        var spans = SpellScanner.Misspelled(SpellSpans.LineText(runes), SpellChecker!.IsMisspelled);
        if (spans.Count == 0)
            return;

        Driver.SetAttribute(SpellSpans.MisspelledColor);
        int col = 0;
        for (int idx = ScrollOffset; idx < runes.Count && col < Frame.Width; idx++)
        {
            var span = SpellSpans.At(spans, idx);
            if (span != null && idx != CursorPosition && !(HasFocus && SpellSpans.IsBeingTyped(span, CursorPosition)))
            {
                Move(col, 0);
                Driver.AddRune(runes[idx]);
            }
            col += Rune.ColumnWidth(runes[idx]);
        }

        PositionCursor();
    }

    void ShowSpellingSuggestions()
    {
        var spans = SpellScanner.Misspelled(SpellSpans.LineText(RawText), SpellChecker!.IsMisspelled);
        var next = SpellSpans.NextFrom([spans], 0, CursorPosition);
        if (next == null)
            return;
        var span = next.Value.Span;

        // The menu is placed in screen coordinates, just under the word
        var origin = ScreenToView(0, 0);
        int x = span.Start - ScrollOffset - origin.X;
        int y = 1 - origin.Y;
        var items = SpellSpans.MenuItems(SpellChecker!, span.Word, with => Replace(span, with), SetNeedsDisplay);
        Menu.Show("Spelling", x, y, items);
    }

    void Replace(WordSpan span, string with)
    {
        (string text, int caret) = SpellSpans.Replace(RawText, span, with);
        Text = text;
        CursorPosition = caret;
        SetNeedsDisplay();
    }
}
