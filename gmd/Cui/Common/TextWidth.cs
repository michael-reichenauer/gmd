namespace gmd.Cui.Common;

// How many terminal columns text takes, as Terminal.Gui draws it, which is not its length in UTF-16
// chars: a wide character, e.g. of Chinese or Japanese, takes two, a combining mark none, and a
// character outside the basic plane, e.g. an emoji, is two chars. Measured in chars, a subject in
// Japanese moved the sid, author and time columns after it, and a cut could split an emoji in two.
//
// Terminal.Gui 1.x's curses driver cannot draw a character outside the basic plane (an emoji came out
// as '~I'), so one is drawn as '�', one column, and counted so; it stays itself in the text, which
// is what a copy takes.
static class TextWidth
{
    public const string Replacement = "�";

    // The columns of a string, quickly for the text below U+0300, all one column wide, which nearly
    // all text is
    public static int Of(string text)
    {
        if (!text.AsSpan().ContainsAnyInRange((char)0x300, char.MaxValue))
            return text.Length;

        var width = 0;
        foreach (var rune in text.EnumerateRunes())
            width += Of(rune);
        return width;
    }

    // As Terminal.Gui counts them, the same table, but a control character, which it counts as -1 and
    // still moves past, one
    public static int Of(System.Text.Rune rune)
    {
        if (!rune.IsBmp)
            return 1;
        var width = System.Rune.ColumnWidth(new System.Rune((uint)rune.Value));
        return width < 0 ? 1 : width;
    }

    // The part of a text from column start to column end, given that it starts at column x: a wide
    // character cut by either end is spaces, for the columns of it that are in. A combining mark goes
    // with the character before it.
    public static string Slice(string text, int x, long start, long end)
    {
        if (x >= start && x + text.Length <= end && !text.AsSpan().ContainsAnyInRange((char)0x300, char.MaxValue))
            return text;

        var slice = new System.Text.StringBuilder();
        long column = x;
        foreach (var rune in text.EnumerateRunes())
        {
            if (column > end)
                break;
            var width = Of(rune);
            if (width == 0)
            {
                if (column > start && column <= end)
                    slice.Append(rune.ToString());
                continue;
            }

            var right = column + width;
            if (right <= start)
            { // Before the slice
            }
            else if (column >= start && right <= end)
            {
                slice.Append(rune.ToString());
            }
            else
            { // Cut by an end, so only some of its columns are in
                var inside = Math.Min(right, end) - Math.Max(column, start);
                if (inside > 0)
                    slice.Append(' ', (int)inside);
            }

            column = right;
        }

        return slice.ToString();
    }

    // The string as drawn: a character outside the basic plane as '�', and a control character as
    // its picture, e.g. '\r' as '␍', one column as counted: a '\r' kept from a file, e.g. a lone one
    // in a line of a diff, was drawn as it is, which moves the terminal's cursor back to the start of
    // the row, over what was drawn there
    public static string Drawn(string text)
    {
        if (
            !text.AsSpan().ContainsAnyInRange((char)0xD800, (char)0xDFFF)
            && !text.AsSpan().ContainsAnyInRange('\0', (char)0x1F)
            && !text.Contains('\u007F')
        )
            return text;

        var drawn = new System.Text.StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (!rune.IsBmp)
                drawn.Append(Replacement);
            else if (rune.Value < 0x20)
                drawn.Append((char)(0x2400 + rune.Value));
            else if (rune.Value == 0x7F)
                drawn.Append('\u2421');
            else
                drawn.Append(rune.ToString());
        }
        return drawn.ToString();
    }

    // A string cut or padded with spaces to a width, as a column of a row is
    public static string Fit(string text, int width)
    {
        var slice = Slice(text, 0, 0, width);
        var sliceWidth = Of(slice);
        return sliceWidth < width ? slice + new string(' ', width - sliceWidth) : slice;
    }
}
