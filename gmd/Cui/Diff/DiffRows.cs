using gmd.Cui.Common;

namespace gmd.Cui.Diff;

class DiffRows
{
    readonly List<DiffRow> rows = new List<DiffRow>();

    internal int MaxLength { get; private set; }
    internal int Count => rows.Count;

    public IReadOnlyList<DiffRow> Rows => rows;

    internal void Add(Text oneRow, string filePath = "", string commitId = "") =>
        Add(oneRow, Text.Empty, DiffRowMode.SpanBoth, filePath, commitId);

    internal void Add(Text left, Text right) =>
        Add(left, right, DiffRowMode.SideBySide, "", "");

    internal void AddLine(Text line) =>
       Add(line, Text.Empty, DiffRowMode.DividerLine, "", "");

    void Add(Text left, Text right, DiffRowMode mode, string filePath, string commitId)
    {
        if (left.Length > MaxLength)
        {
            MaxLength = left.Length;
        }
        if (right.Length > MaxLength)
        {
            MaxLength = right.Length;
        }

        rows.Add(new DiffRow(left, right, mode, filePath, commitId));
    }

    public override string ToString() => $"Rows: {Count}";
}


record DiffRow(Text Left, Text Right, DiffRowMode Mode, string FilePath, string CommitId);

enum DiffRowMode
{
    SideBySide,
    SpanBoth,
    DividerLine,
}

record Line(int LineNbr, string Text, Color Color);

class Block
{
    public List<Line> Lines { get; } = new List<Line>();
    public void Add(int lineNbr, string text, Color color)
    {
        Lines.Add(new Line(lineNbr, text, color));
    }
}
