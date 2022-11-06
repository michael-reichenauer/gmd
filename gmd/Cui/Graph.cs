using gmd.ViewRepos;
using Color = Terminal.Gui.Attribute;


namespace gmd.Cui;

class Graph
{
    GraphRow[] rows;
    private readonly IReadOnlyList<GraphBranch> branches;

    internal int Width { get; private set; }

    internal Graph(int columnCount, int height, IReadOnlyList<GraphBranch> branches)
    {
        Width = columnCount * 2;
        rows = new GraphRow[height];
        for (int y = 0; y < height; y++)
        {
            rows[y] = new GraphRow(columnCount);
        }
        this.branches = branches;
    }

    internal GraphRow GetRow(int index) => rows[index];

    internal GraphBranch BranchByName(string name) => branches.First(b => b.B.Name == name);


    internal void DrawHorizontalLine(int x1, int x2, int y, Color color)
    {
        for (int x = x1; x < x2; x++)
        {
            SetGraphBranchPass(x, y, Sign.Pass, color);
            SetGraphPass(x, y, Sign.Pass, color);
        }
    }

    internal void DrawVerticalLine(int x, int y1, int y2, Color color)
    {
        for (int y = y1; y < y2; y++)
        {
            SetGraphConnect(x, y, Sign.ConnectLine, color);
        }
    }


    internal void SetGraphConnect(int x, int y, Sign sign, Color color) =>
        rows[y].SetConnect(x, sign, color);



    internal void SetGraphBranch(int x, int y, Sign sign, Color color) =>
        rows[y].SetBranch(x, sign, color);

    void SetGraphBranchPass(int x, int y, Sign sign, Color color) =>
        rows[y].SetGraphBranchPass(x, y, sign, color);

    void SetGraphPass(int x, int y, Sign sign, Color color) =>
        rows[y].SetGraphPass(x, y, sign, color);
}


class GraphColumn
{
    internal Sign Connect { get; private set; } = Sign.Blank;
    internal Sign Branch { get; private set; } = Sign.Blank;
    internal Color BranchColor { get; private set; } = Colors.None;
    internal Color ConnectColor { get; private set; } = Colors.None;
    internal Color PassColor { get; private set; } = Colors.None;

    internal void SetConnect(Sign sign, Color color)
    {
        Connect |= sign;
        ConnectColor = color;
    }

    internal void SetBranch(Sign sign, Color color)
    {
        Branch |= sign;
        BranchColor = color;
    }

    internal void SetGraphBranchPass(Sign sign, Color color)
    {
        Branch |= sign;

        if (BranchColor == Colors.None)
        {
            BranchColor = color;
        }
    }

    internal void SetGraphPass(Sign sign, Color color)
    {
        Connect |= sign;

        if (PassColor == Colors.None)
        {
            PassColor = color;
        }
        else if (PassColor != color)
        {
            PassColor = Colors.Ambiguous;
        }
    }
}


class GraphRow
{
    GraphColumn[] columns;

    internal int Width => columns.Length;

    internal GraphRow(int width)
    {
        columns = new GraphColumn[width];
        for (int x = 0; x < width; x++)
        {
            columns[x] = new GraphColumn();
        }
    }

    internal GraphColumn this[int i]
    {
        get { return columns[i]; }
    }

    internal void SetConnect(int x, Sign sign, Color color)
    {
        columns[x].SetConnect(sign, color);
    }

    internal void SetBranch(int x, Sign sign, Color color)
    {
        columns[x].SetBranch(sign, color);
    }

    internal void SetGraphBranchPass(int x, int y, Sign sign, Color color)
    {
        columns[x].SetGraphBranchPass(sign, color);
    }

    internal void SetGraphPass(int x, int y, Sign sign, Color color)
    {
        columns[x].SetGraphPass(sign, color);
    }
}

class GraphBranch
{
    internal Branch B { get; }

    internal int Index { get; set; } = 0;
    internal int X { get; set; } = 0;
    internal int TipIndex { get; set; }
    internal int BottomIndex { get; set; }
    internal GraphBranch? ParentBranch { get; set; }
    internal Color Color { get; set; }

    internal GraphBranch(Branch branch, int index)
    {
        B = branch;
        Index = index;
    }

    public override string ToString() => $"{B}";
}