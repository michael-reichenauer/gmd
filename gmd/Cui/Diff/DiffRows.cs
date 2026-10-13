using gmd.Cui.Common;

namespace gmd.Cui.Diff;

class DiffRows
{
    readonly List<DiffRow> rows = [];

    internal int MaxLength { get; private set; }
    internal int Count => rows.Count;

    public IReadOnlyList<DiffRow> Rows => rows;

    internal void Add(
        Text oneRow,
        string filePath = "",
        string commitId = "",
        int leftLineNbr = 0,
        int rightLineNbr = 0
    ) => Add(oneRow, Text.Empty, DiffRowMode.SpanBoth, filePath, commitId, leftLineNbr, rightLineNbr);

    internal void Add(Text left, Text right, int leftLineNbr = 0, int rightLineNbr = 0) =>
        Add(left, right, DiffRowMode.SideBySide, "", "", leftLineNbr, rightLineNbr);

    internal void AddLine(Text line) => Add(line, Text.Empty, DiffRowMode.DividerLine, "", "", 0, 0);

    void Add(
        Text left,
        Text right,
        DiffRowMode mode,
        string filePath,
        string commitId,
        int leftLineNbr,
        int rightLineNbr
    )
    {
        if (left.Length > MaxLength)
        {
            MaxLength = left.Length;
        }
        if (right.Length > MaxLength)
        {
            MaxLength = right.Length;
        }

        rows.Add(new DiffRow(left, right, mode, filePath, commitId, leftLineNbr, rightLineNbr));
    }

    // Where a row is, as content rather than as an index, which means nothing once the rows are
    // rebuilt with another context: the commit it is in, since a file history shows one diff per
    // commit, the file and the source line
    internal DiffAnchor AnchorAt(int index)
    {
        if (index < 0 || index >= rows.Count)
            return DiffAnchor.None;

        return new DiffAnchor(Above(index, r => r.CommitId), Above(index, r => r.FilePath), rows[index].LineNbr);
    }

    // The row an anchor is at, or the nearest line of its file, or -1 when the file is not there.
    // The file is looked for from the anchor's commit on, since in a file history every commit
    // shows the same path, and taking the first of them put the cursor back on the newest commit
    // whenever + or - was pressed on an older one.
    internal int IndexOf(DiffAnchor anchor)
    {
        if (anchor.FilePath == "")
            return -1;

        var start = anchor.CommitId == "" ? 0 : Math.Max(0, rows.FindIndexBy(r => r.CommitId == anchor.CommitId));
        for (int i = start; i < rows.Count; i++)
        {
            if (rows[i].FilePath == anchor.FilePath)
                return DiffService.FindClosestLineIndex(rows, i, anchor.LineNbr);
        }

        return -1;
    }

    // The value of the closest row at or above the index that has one, "" above them all
    string Above(int index, Func<DiffRow, string> value)
    {
        for (int i = index; i >= 0; i--)
        {
            if (value(rows[i]) != "")
                return value(rows[i]);
        }

        return "";
    }

    public override string ToString() => $"Rows: {Count}";
}

// A drawn row. LeftLineNbr/RightLineNbr are the source line numbers the row shows, or 0 for a row
// that is not a source line (a divider, a header, a conflict marker) or for the side that has no
// line (the '░' filler). They are what the gutter is drawn from, so they also say how wide it is,
// which is how a copy strips it and how the cursor is put back on the same line after a reload.
record DiffRow(
    Text Left,
    Text Right,
    DiffRowMode Mode,
    string FilePath,
    string CommitId,
    int LeftLineNbr,
    int RightLineNbr
)
{
    // The line number this row is at, preferring the new file's side, or 0 if it is not a source line
    internal int LineNbr => RightLineNbr != 0 ? RightLineNbr : LeftLineNbr;
}

// Where the cursor is, as content rather than a row index, so it survives the rows being rebuilt
// with a different amount of context. LineNbr is 0 when the cursor is not on a source line, and
// CommitId "" in a diff that names no commit, e.g. of the uncommitted changes.
record DiffAnchor(string CommitId, string FilePath, int LineNbr)
{
    public static readonly DiffAnchor None = new DiffAnchor("", "", 0);
}

enum DiffRowMode
{
    SideBySide,
    SpanBoth,
    DividerLine,
}

record Line(int LineNbr, string Text, Color Color);

class Block
{
    public List<Line> Lines { get; } = [];

    public void Add(int lineNbr, string text, Color color)
    {
        Lines.Add(new Line(lineNbr, text, color));
    }
}
