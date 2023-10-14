
namespace gmd.Cui.Common;

record Color
{
    // Predefined colors
    public static readonly Color Blue = Make(Terminal.Gui.Color.Blue);
    public static readonly Color Green = Make(Terminal.Gui.Color.Green);
    public static readonly Color Cyan = Make(Terminal.Gui.Color.Cyan);
    public static readonly Color Red = Make(Terminal.Gui.Color.Red);
    public static readonly Color Magenta = Make(Terminal.Gui.Color.Magenta);
    public static readonly Color Yellow = Make(Terminal.Gui.Color.Brown);
    public static readonly Color Dark = Make(Terminal.Gui.Color.DarkGray);
    public static readonly Color White = Make(Terminal.Gui.Color.White);
    public static readonly Color Black = Make(Terminal.Gui.Color.Black);
    public static readonly Color BrightBlue = Make(Terminal.Gui.Color.BrightBlue);
    public static readonly Color BrightGreen = Make(Terminal.Gui.Color.BrightGreen);
    public static readonly Color BrightCyan = Make(Terminal.Gui.Color.BrightCyan);
    public static readonly Color BrightRed = Make(Terminal.Gui.Color.BrightRed);
    public static readonly Color BrightMagenta = Make(Terminal.Gui.Color.BrightMagenta);
    public static readonly Color BrightYellow = Make(Terminal.Gui.Color.BrightYellow);

    public static readonly Color RedBg = Make(Terminal.Gui.Color.White, Terminal.Gui.Color.Red);
    public static readonly Color GreenBg = Make(Terminal.Gui.Color.Black, Terminal.Gui.Color.BrightGreen);

    public Color(Color fg, Color bg) : this(fg.Foreground, bg.Foreground) { }

    public Color(Terminal.Gui.Color fg, Terminal.Gui.Color bg)
    {
        Foreground = fg;
        Background = bg;
    }

    // Foreground and background Terminal.Gui colors
    public Terminal.Gui.Color Foreground { get; init; }
    public Terminal.Gui.Color Background { get; init; }

    // Converters to Color from Gui.Attribute and Gui.Color
    public static implicit operator Color(Terminal.Gui.Attribute a) =>
        new Color(a.Foreground, a.Background);

    // Converters to Gui.Attribute and Gui.Color from Color
    public static implicit operator Terminal.Gui.Attribute(Color color) =>
        new Terminal.Gui.Attribute(color.Foreground, color.Background);
    public static implicit operator Terminal.Gui.Color(Color color) => color.Foreground;

    static Color Make(Terminal.Gui.Color color) => Make(color, Terminal.Gui.Color.Black);
    static Color Make(Terminal.Gui.Color fg, Terminal.Gui.Color bg) => new Color(fg, bg);
}

