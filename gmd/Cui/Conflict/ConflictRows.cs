using gmd.Cui.Common;

namespace gmd.Cui.Conflict;

// How a row is laid out across the source panes
enum ConflictRowMode
{
    Panes, // One cell per pane, e.g. ours │ theirs
    SpanAll, // One text across the whole width, e.g. a file header
}

// Which version of the file a line of a pane is a line of, each side being a version of the whole
// file, the text between the conflicts and its lines of each
enum CodeVersion
{
    None,
    Ours,
    Theirs,
}

// A line of a pane as a line of a version of the file, by its number there, as its gutter numbers it,
// which its code is colored as (ConflictSyntax)
readonly record struct PaneLine(CodeVersion Version, int LineNbr)
{
    public static readonly PaneLine None = new(CodeVersion.None, 0);
}

// A drawn row of the resolver.
//
// HunkIndex is which conflict the row belongs to, or -1 for the stable text between them, which is
// what the cursor is turned into a conflict number with and what next/previous conflict move by.
// GutterWidth is how much of the start of each pane is its gutter, a mark and a line number, which
// stays where it is while the rest scrolls sideways, 0 for a row of no lines. Lines is the line of
// the file each pane is, for a row of lines. IsHunkEnd is the rule a conflict ends with.
record ConflictRow(
    IReadOnlyList<Text> Panes,
    ConflictRowMode Mode,
    int HunkIndex,
    bool IsHunkHeader = false,
    int GutterWidth = 0,
    IReadOnlyList<PaneLine>? Lines = null,
    bool IsHunkEnd = false
)
{
    public static ConflictRow Span(Text text, int hunkIndex = -1, int gutterWidth = 0, PaneLine? line = null) =>
        new ConflictRow(
            [text],
            ConflictRowMode.SpanAll,
            hunkIndex,
            GutterWidth: gutterWidth,
            Lines: line is PaneLine l ? [l] : null
        );

    public static ConflictRow Header(Text text, int hunkIndex) =>
        new ConflictRow([text], ConflictRowMode.SpanAll, hunkIndex, IsHunkHeader: true);

    // Drawn as wide as the panes are when it is drawn (ConflictRowService.ToRowText)
    public static ConflictRow End(int hunkIndex) =>
        new ConflictRow([], ConflictRowMode.Panes, hunkIndex, IsHunkEnd: true);

    public override string ToString() => string.Join(" │ ", Panes.Select(p => p.ToString()));
}

// Where the cursor is as content rather than as a row index, which survives the rows being laid
// out again, e.g. with the common ancestor shown, which makes a conflict as tall as its longest side.
// A row of a conflict is held by the conflict and the row in it, and a row of the text between
// conflicts by the conflict before it (-1 for none) and the row after that conflict.
record ConflictAnchor(int HunkIndex, int Offset, bool IsInHunk);

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

    public ConflictAnchor AnchorAt(int index)
    {
        index = Math.Clamp(index, 0, rows.Count - 1);
        var row = rows[index];
        if (row.HunkIndex >= 0)
            return new(row.HunkIndex, index - IndexOfHunk(row.HunkIndex), true);

        var last = index - 1;
        while (last >= 0 && rows[last].HunkIndex == -1)
            last--;

        return last == -1 ? new(-1, index, false) : new(rows[last].HunkIndex, index - last - 1, false);
    }

    // The row the anchor points at, the last row of its conflict if the conflict is shorter now, or
    // -1 if the conflict is not there
    public int IndexOf(ConflictAnchor anchor)
    {
        if (anchor.HunkIndex == -1)
            return Math.Min(anchor.Offset, rows.Count - 1);

        var header = IndexOfHunk(anchor.HunkIndex);
        if (header == -1)
            return -1;
        var last = header;
        while (last + 1 < rows.Count && rows[last + 1].HunkIndex == anchor.HunkIndex)
            last++;

        return anchor.IsInHunk
            ? Math.Min(header + anchor.Offset, last)
            : Math.Min(last + 1 + anchor.Offset, rows.Count - 1);
    }

    public override string ToString() => $"Rows: {Count}";
}
