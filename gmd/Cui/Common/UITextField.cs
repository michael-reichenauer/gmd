using gmd.Common.Spelling;
using Terminal.Gui;

namespace gmd.Cui.Common;

// A one line text input field, which unlike Terminal.Gui's TextField returns its text as a
// trimmed string rather than as a ustring. With a SpellChecker set, misspelled words are drawn in
// red and F7 or Ctrl+G opens the suggestions for the misspelled word at or after the caret. A
// right click or Shift+F10 opens the context menu, with those suggestions on top when on a
// misspelled word.
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
        if (TextContextMenu.IsMenuKey(keyEvent.Key))
        {
            ShowContextMenu(CursorPosition - ScrollOffset);
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
            CursorPosition = ScrollOffset + ev.X; // The caret goes to the click, as with a left click
            ShowContextMenu(ev.X);
            return true;
        }
        if (ev.Flags.HasFlag(MouseFlags.Button3Pressed) || ev.Flags.HasFlag(MouseFlags.Button3Released))
            return true;
        return base.MouseEvent(ev);
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
        var next = SpellSpans.NextFrom([Misspelled()], 0, CursorPosition);
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

    // The context menu, placed just under the view column x, the click or the caret, or under the
    // misspelled word when on one, where the F7 menu for it goes
    void ShowContextMenu(int x)
    {
        var spans = Misspelled();
        var at = SpellSpans.At(spans, CursorPosition);
        if (at != null)
            x = at.Start - ScrollOffset;
        var spelling = TextContextMenu.SpellingItems(
            SpellChecker,
            at,
            spans.Count > 0,
            with => Replace(at!, with),
            ShowSpellingSuggestions,
            SetNeedsDisplay
        );

        var origin = ScreenToView(0, 0);
        var items = TextContextMenu.Items(spelling, this, () => SelectedLength > 0);
        Menu.Show(TextContextMenu.Title(at), x - origin.X, 1 - origin.Y, items);
    }

    // The misspelled words of the text, or nothing at all when not spell checking
    IReadOnlyList<WordSpan> Misspelled() =>
        !IsSpellCheck ? [] : SpellScanner.Misspelled(SpellSpans.LineText(RawText), SpellChecker!.IsMisspelled);

    void Replace(WordSpan span, string with)
    {
        (string text, int caret) = SpellSpans.Replace(RawText, span, with);
        Text = text;
        CursorPosition = caret;
        SetNeedsDisplay();
    }
}
