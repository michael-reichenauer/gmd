using gmd.Cui.Common;
using gmd.Cui.Graphs;

namespace gmd.Cui.WebLog;

// One row of the drawn branch graph as SVG, for the web log page. The graph is the log view's own,
// as GraphWriter writes it as runes, and each rune is drawn here as the lines it is made of: its
// arms up, down, left and right, heavy for a branch's line and light for a merge's or a branch
// out's, with the four corners as arcs. Text in a browser cannot be relied on to join the runes of
// one row to the next, as a terminal does, since the height of a line is the font's and the page's;
// lines drawn to the edges of a cell always join. The commit is drawn as a dot on its branch's line.
//
// This is string work with no view, so it is tested with no driver.
static class GraphSvg
{
    // The size of one rune's cell in pixels; a row's cells are as wide as its runes are many
    public const int CellWidth = 9;
    public const int RowHeight = 40;

    const double HeavyWidth = 2.5;
    const double LightWidth = 1.5;
    const double DotRadius = 4.5;

    // Where the line above an active branch's tip starts, half way up to the top of the row
    const double ActiveTipTop = RowHeight / 4.0;

    [Flags]
    enum Arm
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8,
    }

    // The arms of each rune GraphWriter writes, heavy and light, and whether its two arms are drawn
    // as an arc rather than as a corner. The heavy arm to the right of a branch rune, which in the
    // terminal points at the commit's subject, is left out, since the dot says it.
    record Shape(Arm Heavy, Arm Light, bool IsArc = false);

    static readonly IReadOnlyDictionary<char, Shape> Shapes = new Dictionary<char, Shape>
    {
        // Branch runes
        ['┃'] = new(Arm.Up | Arm.Down, Arm.None),
        ['┣'] = new(Arm.Up | Arm.Down, Arm.None),
        ['┏'] = new(Arm.Down, Arm.None),
        ['┗'] = new(Arm.Up, Arm.None),
        ['┺'] = new(Arm.Up, Arm.Left),
        ['┲'] = new(Arm.Down, Arm.Left),
        ['╊'] = new(Arm.Up | Arm.Down, Arm.Left),
        ['╂'] = new(Arm.Up | Arm.Down, Arm.Left | Arm.Right),
        ['╼'] = new(Arm.None, Arm.Left),
        ['Φ'] = new(Arm.Up | Arm.Down, Arm.None), // A commit whose branch the user set
        // Connect runes
        ['─'] = new(Arm.None, Arm.Left | Arm.Right),
        ['│'] = new(Arm.None, Arm.Up | Arm.Down),
        ['╮'] = new(Arm.None, Arm.Left | Arm.Down, IsArc: true),
        ['╭'] = new(Arm.None, Arm.Right | Arm.Down, IsArc: true),
        ['╯'] = new(Arm.None, Arm.Left | Arm.Up, IsArc: true),
        ['╰'] = new(Arm.None, Arm.Right | Arm.Up, IsArc: true),
        ['┬'] = new(Arm.None, Arm.Left | Arm.Right | Arm.Down),
        ['┴'] = new(Arm.None, Arm.Left | Arm.Right | Arm.Up),
        ['┤'] = new(Arm.None, Arm.Up | Arm.Down | Arm.Left),
        ['├'] = new(Arm.None, Arm.Up | Arm.Down | Arm.Right),
        ['┼'] = new(Arm.None, Arm.Up | Arm.Down | Arm.Left | Arm.Right),
    };

    // The SVG of a graph row, as GraphWriter wrote it, with a dot at the rune index of the row's
    // commit (-1 for none). The colors are CSS classes named by gmd's color names, which the page
    // defines for its dark and light look. With the graph's row, the cells where lines of several
    // colors meet are drawn from its parts instead (see Parts), each line in its own color.
    public static string Row(Text row, int dotIndex, int width, GraphRow? graphRow = null)
    {
        var svg = new System.Text.StringBuilder();
        svg.Append(
            $"<svg class=\"g\" width=\"{width * CellWidth}\" height=\"{RowHeight}\" "
                + $"viewBox=\"0 0 {width * CellWidth} {RowHeight}\" aria-hidden=\"true\">"
        );

        var parts = graphRow != null ? new Parts(graphRow) : null;
        int index = 0;
        string dotColor = "";
        foreach (var fragment in row.Fragments)
        {
            var color = ColorClass(fragment.Color);
            foreach (var rune in fragment.Text)
            {
                // The commit's own color, white when its branch is ambiguous or set by the user ('Φ')
                if (index == dotIndex)
                    dotColor = color;
                // The line above the latest commit of a branch that may get more, which the terminal
                // has as the upper half of its rune (┣), is drawn half as long, as a mark of it
                var top = graphRow != null && IsActiveTip(graphRow, index) ? ActiveTipTop : 0;
                if (parts != null && parts.IsDrawn(svg, index, rune, top))
                {
                    index++;
                    continue;
                }
                if (Shapes.TryGetValue(rune, out var shape))
                    AppendShape(svg, shape, index, color, top);
                index++;
            }
        }

        if (dotIndex >= 0 && dotColor != "")
        {
            double cx = dotIndex * CellWidth + CellWidth / 2.0;
            double cy = RowHeight / 2.0;
            svg.Append($"<circle class=\"d {dotColor}\" cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(DotRadius)}\"/>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    // The lines of a row where the terminal shares one white rune between lines of several colors,
    // e.g. where a branch is merged in from below and another branches out above, at the same
    // commit, along one horizontal line. Each is drawn in its own color here, and the horizontal
    // lines that run side by side are drawn a little apart, the one turning up above the one
    // turning down, so that each can be followed from the commit to its branch.
    sealed class Parts
    {
        const double Gap = 4;

        readonly GraphRow row;

        // The vertical offset of each color's horizontal line, for the colors that share one
        readonly Dictionary<Color, double> offsets = [];

        public Parts(GraphRow row)
        {
            this.row = row;

            // The colors that share a horizontal line somewhere in the row, passing or turning in one cell
            var shared = new List<Color>();
            for (int x = 0; x < row.Width; x++)
            {
                foreach (var colors in new[] { LineColors(row[x]), row[x].BranchPassColors.Distinct().ToList() })
                {
                    if (colors.Count > 1)
                        shared.AddRange(colors.Where(c => !shared.Contains(c)));
                }
            }
            if (shared.Count < 2)
                return;

            // The ones turning up above, those turning down below, each a gap apart around the middle
            var ordered = shared.OrderBy(Direction).ToList();
            for (int i = 0; i < ordered.Count; i++)
                offsets[ordered[i]] = (i - (ordered.Count - 1) / 2.0) * Gap;
        }

        // -1 for a color whose line turns up in the row, 1 for one turning down, else 0
        int Direction(Color color)
        {
            var signs = Enumerable
                .Range(0, row.Width)
                .SelectMany(x => row[x].ConnectParts)
                .Where(p => p.Color == color)
                .Aggregate(Sign.Blank, (all, p) => all | p.Sign);
            if (signs.HasFlag(Sign.BranchToLeft) || signs.HasFlag(Sign.BranchToRight))
                return -1;
            if (signs.HasFlag(Sign.MergeFromLeft) || signs.HasFlag(Sign.MergeFromRight))
                return 1;
            return 0;
        }

        // The colors of the horizontal lines of a connect cell, passing or turning, not vertical
        static List<Color> LineColors(GraphColumn column) =>
            DrawnParts(column).Where(p => p.Sign != Sign.ConnectLine).Select(p => p.Color).Distinct().ToList();

        // The parts of a connect cell as drawn: a horizontal line that starts with a turn to the
        // right of the cell (╰ ╭) is set as passing that cell too, which the terminal leaves out
        static List<(Sign Sign, Color Color)> DrawnParts(GraphColumn column) =>
            column
                .ConnectParts.Distinct()
                .Where(p =>
                    p.Sign != Sign.Pass
                    || !column.ConnectParts.Any(t =>
                        t.Color == p.Color && (t.Sign == Sign.BranchToLeft || t.Sign == Sign.MergeFromLeft)
                    )
                )
                .ToList();

        // Whether the cell at the rune index is drawn from its parts, which it then is
        public bool IsDrawn(System.Text.StringBuilder svg, int index, char rune, double top)
        {
            // A column is a connect rune (but the first) followed by its branch rune
            var column = row[(index + 1) / 2];
            bool isConnect = index % 2 == 1;
            double x0 = index * CellWidth;
            double x1 = x0 + CellWidth;
            double xm = x0 + CellWidth / 2.0;
            double ym = RowHeight / 2.0;

            if (isConnect)
            {
                var cellParts = DrawnParts(column);
                var colors = cellParts.Select(p => p.Color).Distinct().ToList();
                if (colors.Count < 2 && !colors.Any(offsets.ContainsKey))
                    return false;

                // A turn into a line of another color that runs through the cell joins it straight,
                // since its arc up or down would run along that line
                bool hasLine = cellParts.Any(p => p.Sign == Sign.ConnectLine);
                foreach (var (sign, color) in cellParts)
                {
                    var c = ColorClass(color);
                    var y = ym + offsets.GetValueOrDefault(color);
                    if (hasLine && (sign == Sign.MergeFromRight || sign == Sign.BranchToRight))
                        Line(svg, c, $"M{N(x0)} {N(y)}H{N(xm)}");
                    else if (hasLine && (sign == Sign.MergeFromLeft || sign == Sign.BranchToLeft))
                        Line(svg, c, $"M{N(x1)} {N(y)}H{N(xm)}");
                    else if (sign == Sign.Pass)
                        Line(svg, c, $"M{N(x0)} {N(y)}H{N(x1)}");
                    else if (sign == Sign.ConnectLine)
                        Line(svg, c, $"M{N(xm)} 0V{RowHeight}");
                    else if (sign == Sign.MergeFromRight)
                        Line(svg, c, $"M{N(x0)} {N(y)}Q{N(xm)} {N(y)} {N(xm)} {RowHeight}");
                    else if (sign == Sign.BranchToRight)
                        Line(svg, c, $"M{N(x0)} {N(y)}Q{N(xm)} {N(y)} {N(xm)} 0");
                    else if (sign == Sign.MergeFromLeft)
                        Line(svg, c, $"M{N(x1)} {N(y)}Q{N(xm)} {N(y)} {N(xm)} {RowHeight}");
                    else if (sign == Sign.BranchToLeft)
                        Line(svg, c, $"M{N(x1)} {N(y)}Q{N(xm)} {N(y)} {N(xm)} 0");
                }
                return true;
            }

            // A branch rune with a horizontal line through it: the lines, and the branch's own
            // line of the rune, e.g. the one a horizontal line crosses
            var passColors = column.BranchPassColors.Distinct().ToList();
            if (!column.BranchSign.HasFlag(Sign.Pass) || passColors.Count == 0)
                return false;
            if (passColors.Count < 2 && !passColors.Any(offsets.ContainsKey))
                return false;

            // A line that ends in the cell, at a commit or a branch's line, ends in its middle; the
            // connect cell to its left is the column's own, the one to its right the next column's
            var right = (index + 2) / 2 < row.Width ? row[(index + 2) / 2] : null;
            foreach (var color in passColors)
            {
                var from = LineColors(column).Contains(color) ? x0 : xm;
                var to = right != null && LineColors(right).Contains(color) ? x1 : xm;
                Line(svg, ColorClass(color), $"M{N(from)} {N(ym + offsets.GetValueOrDefault(color))}H{N(to)}");
            }
            if (Shapes.TryGetValue(rune, out var shape))
                AppendArms(svg, shape.Heavy, "h", ColorClass(column.BranchColor), x0, x1, xm, ym, top);
            return true;
        }

        static void Line(System.Text.StringBuilder svg, string color, string d) =>
            svg.Append($"<path class=\"l {color}\" d=\"{d}\"/>");
    }

    // The CSS class of a color, e.g. "c-Branch3", as gmd names its colors
    public static string ColorClass(Color color) => $"c-{color.Foreground}";

    // Whether the rune at the index is of the tip of a branch that may get more commits
    static bool IsActiveTip(GraphRow row, int index) =>
        index % 2 == 0
        && index / 2 < row.Width
        && row[index / 2].BranchSign.HasFlag(Sign.Tip)
        && row[index / 2].BranchSign.HasFlag(Sign.ActiveTip);

    static void AppendShape(System.Text.StringBuilder svg, Shape shape, int index, string color, double top = 0)
    {
        double x0 = index * CellWidth;
        double x1 = x0 + CellWidth;
        double xm = x0 + CellWidth / 2.0;
        double ym = RowHeight / 2.0;

        if (shape.IsArc)
        {
            // An arc between the middles of the two edges its arms reach
            var (fromX, fromY) = shape.Light.HasFlag(Arm.Left) ? (x0, ym) : (x1, ym);
            var (toX, toY) = shape.Light.HasFlag(Arm.Up) ? (xm, 0.0) : (xm, (double)RowHeight);
            svg.Append($"<path class=\"l {color}\" d=\"M{N(fromX)} {N(fromY)}Q{N(xm)} {N(ym)} {N(toX)} {N(toY)}\"/>");
            return;
        }

        AppendArms(svg, shape.Light, "l", color, x0, x1, xm, ym);
        AppendArms(svg, shape.Heavy, "h", color, x0, x1, xm, ym, top);
    }

    static void AppendArms(
        System.Text.StringBuilder svg,
        Arm arms,
        string weight,
        string color,
        double x0,
        double x1,
        double xm,
        double ym,
        double top = 0
    )
    {
        if (arms == Arm.None)
            return;
        var d = new System.Text.StringBuilder();
        // A line straight through is drawn as one, so that no join shows where the two halves meet
        if (arms.HasFlag(Arm.Up) && arms.HasFlag(Arm.Down))
            d.Append($"M{N(xm)} {N(top)}V{RowHeight}");
        else if (arms.HasFlag(Arm.Up))
            d.Append($"M{N(xm)} {N(top)}V{N(ym)}");
        else if (arms.HasFlag(Arm.Down))
            d.Append($"M{N(xm)} {N(ym)}V{RowHeight}");

        if (arms.HasFlag(Arm.Left) && arms.HasFlag(Arm.Right))
            d.Append($"M{N(x0)} {N(ym)}H{N(x1)}");
        else if (arms.HasFlag(Arm.Left))
            d.Append($"M{N(x0)} {N(ym)}H{N(xm)}");
        else if (arms.HasFlag(Arm.Right))
            d.Append($"M{N(xm)} {N(ym)}H{N(x1)}");

        svg.Append($"<path class=\"{weight} {color}\" d=\"{d}\"/>");
    }

    // A number as SVG writes it, whatever the culture
    static string N(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // The stroke widths, for the page's style
    public static string Style =>
        $".g .h{{stroke-width:{N(HeavyWidth)}}}.g .l{{stroke-width:{N(LightWidth)}}}"
        + ".g path{fill:none;stroke:currentColor;stroke-linecap:butt}"
        + ".g .d{fill:currentColor;stroke:var(--bg);stroke-width:1.5}";
}
