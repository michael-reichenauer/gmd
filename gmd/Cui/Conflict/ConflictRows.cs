using gmd.Cui.Common;

namespace gmd.Cui.Conflict;

// How a row is laid out across the source panes
enum ConflictRowMode
{
    Panes, // One cell per pane, e.g. ours │ theirs
    SpanAll, // One text across the whole width, e.g. a file header
    Divider, // A rule drawn to the full width
}

// A drawn row of the resolver.
//
// HunkIndex is which conflict the row belongs to, or -1 for the stable text between them, which is
// what the cursor is turned into a conflict number with and what next/previous conflict move by.
record ConflictRow(IReadOnlyList<Text> Panes, ConflictRowMode Mode, int HunkIndex, bool IsHunkHeader = false)
{
    public static ConflictRow Span(Text text, int hunkIndex = -1) =>
        new ConflictRow([text], ConflictRowMode.SpanAll, hunkIndex);

    public static ConflictRow Header(Text text, int hunkIndex) =>
        new ConflictRow([text], ConflictRowMode.SpanAll, hunkIndex, IsHunkHeader: true);

    public static ConflictRow Divider(Text text) => new ConflictRow([text], ConflictRowMode.Divider, -1);

    public override string ToString() => string.Join(" │ ", Panes.Select(p => p.ToString()));
}

class ConflictRows
{
    readonly List<ConflictRow> rows = [];

    public IReadOnlyList<ConflictRow> Rows => rows;
    public int Count => rows.Count;
    public int MaxLength { get; private set; }

    public void Add(ConflictRow row)
    {
        foreach (var pane in row.Panes)
        {
            if (pane.Length > MaxLength)
                MaxLength = pane.Length;
        }
        rows.Add(row);
    }

    // The row a conflict starts at, for scrolling to it
    public int IndexOfHunk(int hunkIndex) => rows.FindIndexBy(r => r.IsHunkHeader && r.HunkIndex == hunkIndex);

    // The row the next or previous conflict starts at, from a row, or -1 if there is none that way.
    // By row and not by conflict number: the cursor spends most of its time in the text between
    // conflicts, where there is no conflict to count from — counting from the nearest one above
    // steps over the conflict the user is looking for, and above the first conflict of a file there
    // is nothing above at all, so a file with a single conflict had nowhere to go in either
    // direction.
    public int NextHunkRow(int fromRow, int step)
    {
        for (int i = fromRow + step; i >= 0 && i < rows.Count; i += step)
        {
            if (rows[i].IsHunkHeader)
                return i;
        }

        return -1;
    }

    public override string ToString() => $"Rows: {Count}";
}
