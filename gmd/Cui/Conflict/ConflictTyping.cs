using gmd.Cui.Common;

namespace gmd.Cui.Conflict;

// Where the text of a conflict being typed by hand goes in the result pane, below the sides, which
// stay in sight above it (ConflictView): a few of the file's lines above the conflict, the rows of
// the text, which a text view is laid over and whose gutter numbers its lines as the file will, and
// a line or two of the file after it. First is the row of the result the pane starts at. No view, so
// it is unit testable.
record ConflictTyping(int First, int Above, int TextHeight, int Below)
{
    // As many of the file's lines as the pane shows above a conflict when not typing
    const int MaxAbove = 3;
    const int MaxBelow = 2;

    // The fewest rows the text has, which the lines around it give way to in a low pane
    const int MinText = 3;

    // start and end are the rows of the conflict's lines in the result, the result being made with
    // the text typed so far, total its rows, lines how many lines the text has as typed, and height
    // the rows of the pane below its title
    public static ConflictTyping Of(int start, int end, int total, int lines, int height)
    {
        var above = Math.Min(MaxAbove, start);
        var below = Math.Min(MaxBelow, total - end);

        // A row to spare below the text, so that a line added at its end is typed in sight rather
        // than the text scrolling up a line first and the rows growing after
        var text = lines + 1;
        while (above + below + Math.Min(text, MinText) > height && above + below > 0)
        {
            if (below > 0)
                below--;
            else
                above--;
        }

        text = Math.Max(1, Math.Min(text, height - above - below));
        return new(start - above, above, text, below);
    }

    // The rows of the pane: the file above, for each row of the text its gutter, numbered where the
    // row is a line of the result, counted from the row the text is scrolled to, and the file after.
    // rowAt is a row of the result as drawn, its code colored (ConflictSyntax).
    public IReadOnlyList<Text> Rows(ConflictResult result, int hunk, int topRow, Func<int, Text>? rowAt = null)
    {
        rowAt ??= i => result.Rows[i];
        var (start, end) = (result.Starts[hunk], result.Ends[hunk]);
        List<Text> rows = [.. Enumerable.Range(First, Above).Select(rowAt)];
        for (int i = 0; i < TextHeight; i++)
        {
            var row = start + topRow + i;
            rows.Add(row < end ? result.Rows[row].Subtext(0, ConflictRowService.GutterWidth) : Text.Magenta("▌"));
        }
        rows.AddRange(Enumerable.Range(end, Math.Min(Below, result.Rows.Count - end)).Select(rowAt));
        return rows;
    }
}
