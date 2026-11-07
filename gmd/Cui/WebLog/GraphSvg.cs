using gmd.Cui.Common;

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
    // defines for its dark and light look.
    public static string Row(Text row, int dotIndex, int width)
    {
        var svg = new System.Text.StringBuilder();
        svg.Append(
            $"<svg class=\"g\" width=\"{width * CellWidth}\" height=\"{RowHeight}\" "
                + $"viewBox=\"0 0 {width * CellWidth} {RowHeight}\" aria-hidden=\"true\">"
        );

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
                if (Shapes.TryGetValue(rune, out var shape))
                    AppendShape(svg, shape, index, color);
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

    // The CSS class of a color, e.g. "c-Branch3", as gmd names its colors
    public static string ColorClass(Color color) => $"c-{color.Foreground}";

    static void AppendShape(System.Text.StringBuilder svg, Shape shape, int index, string color)
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
        AppendArms(svg, shape.Heavy, "h", color, x0, x1, xm, ym);
    }

    static void AppendArms(
        System.Text.StringBuilder svg,
        Arm arms,
        string weight,
        string color,
        double x0,
        double x1,
        double xm,
        double ym
    )
    {
        if (arms == Arm.None)
            return;
        var d = new System.Text.StringBuilder();
        // A line straight through is drawn as one, so that no join shows where the two halves meet
        if (arms.HasFlag(Arm.Up) && arms.HasFlag(Arm.Down))
            d.Append($"M{N(xm)} 0V{RowHeight}");
        else if (arms.HasFlag(Arm.Up))
            d.Append($"M{N(xm)} 0V{N(ym)}");
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
