using System.Drawing;
using gmd.Common.Spelling;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace gmd.Cui.Common;

// A one line text input field, which returns its text trimmed. With a SpellChecker set, misspelled
// words are drawn in red and F7 or Ctrl+G opens the suggestions for the misspelled word at or after
// the caret. A right click or Shift+F10 opens the context menu, with those suggestions on top when on
// a misspelled word, in place of Terminal.Gui's own edit menu.
//
// Terminal.Gui counts the caret and the scroll offset in graphemes, the spell checking in runes,
// which are the same for text with no combining marks.
class UITextField : TextField
{
    readonly Typing typing;

    internal UITextField(int x, int y, int w, string text = "")
    {
        X = x;
        Y = y;
        Width = w;
        Height = 1;
        base.Text = text;
        this.WithScheme(() => ColorSchemes.TextField);
        typing = new Typing(SetNeedsDraw);
        TextChanged += (_, _) =>
        {
            if (IsSpellCheck)
                typing.Edited();
        };
    }

    internal ISpellChecker? SpellChecker { get; set; }

    // Raised after each redraw, for what is drawn from this view's state elsewhere, e.g. the hint
    // that counts the red words
    internal event Action? Redrawn;

    bool IsSpellCheck => SpellChecker?.IsEnabled == true;

    // The misspelled words drawn red, i.e. all but the one being typed
    internal int MisspelledCount => Misspelled().Count(s => !IsBeingTyped(s));

    public new string Text
    {
        get => base.Text?.Trim() ?? "";
        set => base.Text = value;
    }

    // The text exactly as typed, which is what the caret and the scroll offset are indexes into
    public string RawText => base.Text ?? "";

    // Where the caret is, as an index into the text
    public int CursorPosition
    {
        get => InsertionPoint;
        set => InsertionPoint = value;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (IsSpellCheck && SpellSpans.IsSpellKey(key))
        {
            ShowSpellingSuggestions();
            return true;
        }
        if (TextContextMenu.IsMenuKey(key))
        {
            ShowContextMenu(CursorPosition - ScrollOffset);
            return true;
        }
        return base.OnKeyDown(key);
    }

    // A right click opens the context menu in place of the one Terminal.Gui would open on it, and
    // the press and release it is made of are swallowed too, so the base view sees none of it
    protected override bool OnMouseEvent(Mouse ev)
    {
        var x = (ev.Position ?? Point.Empty).X;
        if (ev.Flags.HasFlag(MouseFlags.RightButtonClicked))
        {
            if (!HasFocus)
                SetFocus();
            CursorPosition = ScrollOffset + x; // The caret goes to the click, as with a left click
            ShowContextMenu(x);
            return true;
        }
        if (ev.Flags.HasFlag(MouseFlags.RightButtonPressed) || ev.Flags.HasFlag(MouseFlags.RightButtonReleased))
            return true;
        return base.OnMouseEvent(ev);
    }

    // TextField has no hook for coloring a rune as it is drawn, so the misspelled words are drawn
    // once more, in red, over what the base drew. The caret cell is left as drawn, and a selection is
    // left alone entirely.
    protected override bool OnDrawingContent(DrawContext? context)
    {
        var isDrawn = base.OnDrawingContent(context);
        DrawMisspelled();
        Redrawn?.Invoke();
        return isDrawn;
    }

    void DrawMisspelled()
    {
        if (!IsSpellCheck || SelectedLength > 0)
            return;

        var runes = RawText.EnumerateRunes().ToList();
        var spans = SpellScanner.Misspelled(SpellSpans.LineText(runes), SpellChecker!.IsMisspelled);
        if (spans.Count == 0)
            return;

        SetAttribute(SpellSpans.MisspelledColor);
        int col = 0;
        for (int idx = ScrollOffset; idx < runes.Count && col < Viewport.Width; idx++)
        {
            var span = SpellSpans.At(spans, idx);
            if (span != null && idx != CursorPosition && !IsBeingTyped(span))
            {
                Move(col, 0);
                AddRune(runes[idx]);
            }
            col += TextWidth.Of(runes[idx]);
        }
    }

    bool IsBeingTyped(WordSpan span) => SpellSpans.IsBeingTyped(span, CursorPosition, HasFocus && typing.IsTyping);

    void ShowSpellingSuggestions()
    {
        var next = SpellSpans.NextFrom([Misspelled()], 0, CursorPosition);
        if (next == null)
            return;
        var span = next.Value.Span;

        // The menu is placed in screen coordinates, just under the word
        var origin = ViewportToScreen(Point.Empty);
        int x = span.Start - ScrollOffset + origin.X;
        int y = 1 + origin.Y;
        var items = SpellSpans.MenuItems(SpellChecker!, span.Word, with => Replace(span, with), SetNeedsDraw);
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
            SetNeedsDraw
        );

        var origin = ViewportToScreen(Point.Empty);
        var items = TextContextMenu.Items(spelling, this, () => SelectedLength > 0);
        Menu.Show(TextContextMenu.Title(at), x + origin.X, 1 + origin.Y, items);
    }

    // The misspelled words of the text, or nothing at all when not spell checking
    IReadOnlyList<WordSpan> Misspelled() =>
        !IsSpellCheck ? [] : SpellScanner.Misspelled(SpellSpans.LineText(RawText), SpellChecker!.IsMisspelled);

    void Replace(WordSpan span, string with)
    {
        (string text, int caret) = SpellSpans.Replace(RawText, span, with);
        Text = text;
        CursorPosition = caret;
        SetNeedsDraw();
    }
}
