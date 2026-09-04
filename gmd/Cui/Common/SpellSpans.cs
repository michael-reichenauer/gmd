using gmd.Common.Spelling;
using Terminal.Gui;

namespace gmd.Cui.Common;

// The index math behind spell checking in the text inputs: which keys open the suggestions, which
// misspelled word the caret is on or should go to next, whether a word is still being typed, and
// how a word is replaced. No view and no drawing, so it is unit testable; UITextView and
// UITextField are the callers.
//
// All indexes are rune indexes, which is what Terminal.Gui counts columns and caret positions in;
// LineText is how a line gets one char per rune so that SpellScanner's indexes are those too.
static class SpellSpans
{
    // Misspelled words are drawn in this color; the inputs have a black background
    public static readonly Color MisspelledColor = Color.BrightRed;

    public static bool IsSpellKey(Key key) => key == Key.F7 || key == (Key.G | Key.CtrlMask);

    // A line of runes as one char per rune. A rune outside the basic plane becomes a placeholder,
    // which is fine for a scan that skips any word with a symbol in it anyway.
    public static string LineText(IReadOnlyList<Rune> line)
    {
        var chars = new char[line.Count];
        for (int i = 0; i < line.Count; i++)
            chars[i] = ToChar(line[i].Value);
        return new string(chars);
    }

    public static string LineText(string text)
    {
        List<char> chars = [];
        foreach (var rune in text.EnumerateRunes())
            chars.Add(ToChar((uint)rune.Value));
        return new string(chars.ToArray());
    }

    // The lines of a TextView's text, which joins them with the platform's newline
    public static string[] Lines(string text) => text.Split(["\r\n", "\n"], StringSplitOptions.None);

    public static WordSpan? At(IReadOnlyList<WordSpan> spans, int index) =>
        spans.FirstOrDefault(s => s.Contains(index));

    // The word the caret sits right at the end of is still being typed, and is not flagged until
    // it is finished, so a word is not red for the whole time it is half written.
    public static bool IsBeingTyped(WordSpan span, int caretIndex) => caretIndex == span.End;

    // The misspelled word at or after the caret — a word the caret is at the end of counts as at —
    // wrapping around to the first one, or null if there is none.
    public static (int Row, WordSpan Span)? NextFrom(
        IReadOnlyList<IReadOnlyList<WordSpan>> lines,
        int caretRow,
        int caretIndex
    )
    {
        var all = lines.SelectMany((spans, row) => spans.Select(span => (Row: row, Span: span))).ToList();
        if (all.Count == 0)
            return null;

        var index = all.FindIndex(x => x.Row > caretRow || (x.Row == caretRow && x.Span.End >= caretIndex));
        return index >= 0 ? all[index] : all[0];
    }

    // Replaces a word in a line, returning the new line and where the caret goes: after the word
    public static (string Text, int Caret) Replace(string line, WordSpan span, string with)
    {
        int start = CharIndex(line, span.Start);
        int end = CharIndex(line, span.End);
        return (line[..start] + with + line[end..], span.Start + with.Length);
    }

    // The menu shown for a misspelled word: the suggestions, then adding the word to the dictionary
    // or leaving it. The actions run after the menu has closed, so each ends by redrawing.
    public static IEnumerable<MenuItem> MenuItems(
        ISpellChecker spellChecker,
        string word,
        Action<string> replace,
        Action redraw
    )
    {
        var suggestions = spellChecker.Suggest(word);
        var items = Menu.Items;
        foreach (var suggestion in suggestions)
            items.Item(suggestion, "", () => replace(suggestion));
        items.Item(suggestions.Count == 0, "(no suggestions)", "", () => { }, () => false);
        items.Separator();
        items.Item(
            $"Add '{word}' to dictionary",
            "",
            () =>
            {
                spellChecker.AddToDictionary(word);
                redraw();
            }
        );
        items.Item("Ignore", "", () => { });
        return items;
    }

    static char ToChar(uint runeValue) => runeValue <= 0xFFFF ? (char)runeValue : '�';

    // The char index of a rune index, for a line that may hold runes of two chars
    static int CharIndex(string line, int runeIndex)
    {
        int charIndex = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (runeIndex-- == 0)
                break;
            charIndex += rune.Utf16SequenceLength;
        }
        return charIndex;
    }
}
