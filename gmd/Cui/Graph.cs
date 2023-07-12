using gmd.Cui.Common;
using System.Linq;
using Color = Terminal.Gui.Attribute;


namespace gmd.Cui;

class Graph
{
    GraphRow[] rows;
    readonly IReadOnlyList<GraphBranch> branches;

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

    public IReadOnlyList<GraphBranch> GetOverlappingBranches(string branchName)
    {
        var branch = BranchByName(branchName);
        return branches.Where(b => IsOverlapping(b, branch)).ToList();
    }

    bool IsOverlapping(GraphBranch b1, GraphBranch b2)
    {
        int margin = 0;

        if (b2.B.Name == b1.B.Name)       // Same branch    
        {
            return true;
        }

        int top1 = b1.TipIndex;
        int bottom1 = b1.BottomIndex;
        int top2 = b2.TipIndex - margin;
        int bottom2 = b2.BottomIndex + margin;

        return (top2 >= top1 && top2 <= bottom1) ||
            (bottom2 >= top1 && bottom2 <= bottom1) ||
            (top2 <= top1 && bottom2 >= bottom1);
    }


    internal IReadOnlyList<GraphBranch> GetRowBranches(int rowIndex) =>
        GetRow(rowIndex).columns
        .Where(c => c.Branch != null)
        .Select(c => c.Branch!).ToList();


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



    internal void SetGraphBranch(int x, int y, Sign sign, Color color, GraphBranch branch) =>
        rows[y].SetBranch(x, sign, color, branch);

    void SetGraphBranchPass(int x, int y, Sign sign, Color color) =>
        rows[y].SetGraphBranchPass(x, y, sign, color);

    void SetGraphPass(int x, int y, Sign sign, Color color) =>
        rows[y].SetGraphPass(x, y, sign, color);
}


class GraphRow
{
    internal GraphColumn[] columns;

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

    internal void SetBranch(int x, Sign sign, Color color, GraphBranch branch)
    {
        columns[x].SetBranch(sign, color, branch);
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


class GraphColumn
{
    internal Sign ConnectSign { get; private set; } = Sign.Blank;
    internal Sign BranchSign { get; private set; } = Sign.Blank;
    internal Color BranchColor { get; private set; } = TextColor.None;
    internal Color ConnectColor { get; private set; } = TextColor.None;
    internal Color PassColor { get; private set; } = TextColor.None;
    internal GraphBranch? Branch { get; private set; }

    internal void SetConnect(Sign sign, Color color)
    {
        ConnectSign |= sign;
        if (ConnectColor == TextColor.None)
        {
            ConnectColor = color;
        }
        else if (ConnectColor != color)
        {
            ConnectColor = TextColor.Ambiguous;
        }
    }

    internal void SetBranch(Sign sign, Color color, GraphBranch branch)
    {
        BranchSign |= sign;
        BranchColor = color;
        Branch = branch;
    }

    internal void SetGraphBranchPass(Sign sign, Color color)
    {
        BranchSign |= sign;

        if (BranchColor == TextColor.None)
        {
            BranchColor = color;
        }
    }

    internal void SetGraphPass(Sign sign, Color color)
    {
        ConnectSign |= sign;

        if (PassColor == TextColor.None)
        {
            PassColor = color;
        }
        else if (PassColor != color)
        {
            PassColor = TextColor.Ambiguous;
        }
    }
}


class GraphBranch
{
    internal Server.Branch B { get; }

    internal int Index { get; set; } = 0;
    internal int X { get; set; } = 0;
    internal int TipIndex { get; set; }
    internal int BottomIndex { get; set; }
    internal GraphBranch? ParentBranch { get; set; }
    internal Color Color { get; set; }

    internal GraphBranch(Server.Branch branch, int index)
    {
        B = branch;
        Index = index;
    }

    public override string ToString() => $"{B}";
}