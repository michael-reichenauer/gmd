using gmd.Cui.Common;
using Attribute = Terminal.Gui.Attribute;

namespace gmd.Cui.Diff;

class DiffRows
{
    readonly List<DiffRow> rows = new List<DiffRow>();

    internal int MaxLength { get; private set; }
    internal int Count => rows.Count;

    public IReadOnlyList<DiffRow> Rows => rows;

    internal void Add(Text oneRow) =>
        Add(oneRow, Text.None, DiffRowMode.SpanBoth);

    internal void Add(Text left, Text right) =>
        Add(left, right, DiffRowMode.LeftRight);

    internal void AddToBoth(Text text) =>
        Add(text, text, DiffRowMode.LeftRight);

    internal void AddLine(Text line) =>
       Add(line, Text.None, DiffRowMode.Line);

    internal void SetHighlighted(int index, bool isHighlighted)
    {
        var row = rows[index];
        rows[index] = row with { IsHighlighted = isHighlighted };
    }

    private void Add(Text left, Text right, DiffRowMode mode)
    {
        if (left.Length > MaxLength)
        {
            MaxLength = left.Length;
        }
        if (right.Length > MaxLength)
        {
            MaxLength = right.Length;
        }

        rows.Add(new DiffRow(left, right, mode, false));
    }

    public override string ToString() => $"Rows: {Count}";
}


record DiffRow(Text Left, Text Right, DiffRowMode Mode, bool IsHighlighted);

enum DiffRowMode
{
    LeftRight,
    SpanBoth,
    Line,
}

record Line(int lineNbr, string text, Attribute color);

class Block
{
    public List<Line> Lines { get; } = new List<Line>();
    public void Add(int lineNbr, string text, Attribute color)
    {
        Lines.Add(new Line(lineNbr, text, color));
    }
}
