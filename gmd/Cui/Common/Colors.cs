
namespace gmd.Cui.Common;

class Color
{
    // Predefined colors
    public static readonly Color Blue = Make(Terminal.Gui.Color.Blue);
    public static readonly Color Green = Make(Terminal.Gui.Color.Green);
    public static readonly Color Cyan = Make(Terminal.Gui.Color.Cyan);
    public static readonly Color Red = Make(Terminal.Gui.Color.Red);
    public static readonly Color Magenta = Make(Terminal.Gui.Color.Magenta);
    public static readonly Color Yellow = Make(Terminal.Gui.Color.Brown);
    public static readonly Color Dark = Make(Terminal.Gui.Color.DarkGray);
    public static readonly Color BrightBlue = Make(Terminal.Gui.Color.BrightBlue);
    public static readonly Color BrightGreen = Make(Terminal.Gui.Color.BrightGreen);
    public static readonly Color BrightCyan = Make(Terminal.Gui.Color.BrightCyan);
    public static readonly Color BrightRed = Make(Terminal.Gui.Color.BrightRed);
    public static readonly Color BrightMagenta = Make(Terminal.Gui.Color.BrightMagenta);
    public static readonly Color BrightYellow = Make(Terminal.Gui.Color.BrightYellow);
    public static readonly Color White = Make(Terminal.Gui.Color.White);
    public static readonly Color Black = Make(Terminal.Gui.Color.Black);

    static readonly Color[] Colors = { Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Magenta,
        Color.Yellow, Color.BrightBlue, Color.BrightGreen, Color.BrightCyan, Color.BrightRed, Color.BrightMagenta,
        Color.BrightYellow , Color.White, Color.Black};


    readonly Terminal.Gui.Color fg;
    readonly Terminal.Gui.Color bg;

    public static readonly Color[] BranchColors = { Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Yellow };

    public static Color BranchColorById(int colorId)
    {
        var index = Math.Min(colorId, BranchColors.Length - 1);
        return BranchColors[index];
    }

    public static int GetBranchColorId(Color color) =>
        Array.FindIndex(Color.BranchColors, c => c == color);


    public Color(Color fg, Color bg) : this(fg.fg, bg.fg) { }

    public Color(Terminal.Gui.Color fg, Terminal.Gui.Color bg)
    {
        this.fg = fg;
        this.bg = bg;
    }

    public Terminal.Gui.Color Foreground => fg;

    public Terminal.Gui.Color Background => bg;

    public static implicit operator Color(Terminal.Gui.Attribute c) =>
        Colors.FirstOrDefault(c => c.fg == c.Foreground && c.bg == c.Background) ??
        new Color(c.Foreground, c.Background);

    public static implicit operator Terminal.Gui.Attribute(Color color) =>
        new Terminal.Gui.Attribute(color.fg, color.bg);

    public static implicit operator Terminal.Gui.Color(Color color) =>
       color.fg;

    static Color Make(Terminal.Gui.Color color) => new Color(color, Terminal.Gui.Color.Black);
}

