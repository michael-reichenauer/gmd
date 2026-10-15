namespace gmd.Cui.Common;

// A search of the rows of the diff or the blame: the text searched for and the rows it is in, for n
// and Shift-N to step through. They step from the cursor rather than from the last match shown, as
// the log's do (SearchMatches), so that after scrolling down a page n goes to the match below it.
// Index math with no view, so it is unit testable.
//
// The text is looked for as typed, as a part of a line with case ignored, not word by word as the
// log matches a commit: a line of code is looked for by a piece of it, e.g. 'Count(' or '-> b'.
class RowSearch
{
    readonly List<int> indexes;

    RowSearch(string text, List<int> indexes)
    {
        Text = text;
        this.indexes = indexes;
    }

    public static readonly RowSearch None = new("", []);

    // What was searched for
    public string Text { get; }

    public int Count => indexes.Count;

    // A search with matches to step through; one that found none leaves nothing to go on with
    public bool IsActive => indexes.Count > 0;

    // The rows the text is in, rowText being a row's text as it is searched, e.g. a diff line without
    // its line number
    public static RowSearch Find(string text, int rowCount, Func<int, string> rowText) =>
        text == ""
            ? None
            : new(text, Enumerable.Range(0, rowCount).Where(i => IndexOf(rowText(i), text) != -1).ToList());

    // Where the text is in a line, from char index 'from' on, ignoring case; -1 where it is not
    public static int IndexOf(string line, string text, int from = 0) =>
        text == "" || from > line.Length ? -1 : line.IndexOf(text, from, StringComparison.OrdinalIgnoreCase);

    // Each place the text is in a line, from char index 'from' on, as char indexes, the end not
    // included. One place ends before the next starts, so 'aa' is found twice in 'aaaa', not three times.
    public static IReadOnlyList<(int Start, int End)> MatchRanges(string line, string text, int from = 0)
    {
        List<(int, int)> ranges = [];
        for (var i = IndexOf(line, text, from); i != -1; i = IndexOf(line, text, i + text.Length))
            ranges.Add((i, i + text.Length));
        return ranges;
    }

    // The match a new search goes to: the first one at the cursor or below it, else the last one above
    // it, so a search finds what there is wherever the cursor is; -1 when there is none
    public int First(int index)
    {
        var below = indexes.FindIndex(i => i >= index);
        return below != -1 ? indexes[below]
            : indexes.Count > 0 ? indexes[^1]
            : -1;
    }

    // The match below the index (direction 1) or above it (-1); -1 past the last one that way, where
    // the cursor stays, as the log's n stops at its last match rather than going round
    public int Next(int index, int direction)
    {
        var next = direction > 0 ? indexes.FindIndex(i => i > index) : indexes.FindLastIndex(i => i < index);
        return next == -1 ? -1 : indexes[next];
    }

    // The status line's words for a match shown and for a step past the last one, as the log has them
    // for its own (BranchCommands.ShowSearchMatch)
    public string MatchText(int index) => $"Match {indexes.IndexOf(index) + 1} of {Count} for '{Text}'";

    public string NoMoreText(int direction) =>
        direction > 0
            ? $"No more matches for '{Text}' below: ⇧n goes back up"
            : $"No more matches for '{Text}' above: n goes down";

    // How far to scroll a line sideways to show the columns from start to end (not included) in a
    // window 'width' columns wide, which marks either end with a '…' where it cuts the line, as the
    // diff and the blame cut theirs: 0 when the match is in the window as it is unscrolled, else just
    // far enough for its end to show, or its start where it is wider than the window
    public static int ScrollToShow(int start, int end, int width)
    {
        // Unscrolled, the window shows the line up to the '…' of its cut on the right
        if (end <= width - 1)
            return 0;

        // Scrolled, the line shows from after the '…' on the left to before the '…' on the right
        var x = end - (width - 1);
        return x + 1 <= start ? x : Math.Max(0, start - 1);
    }
}
