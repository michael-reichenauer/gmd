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
// with a different amount of context. LineNbr is 0 when the cursor is not on a source line.
record DiffAnchor(string FilePath, int LineNbr)
{
    public static readonly DiffAnchor None = new DiffAnchor("", 0);
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
