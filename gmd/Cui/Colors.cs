using Terminal.Gui;
using ColorFgBg = Terminal.Gui.Attribute;


namespace gmd.Cui;

class Colors
{
    public static readonly ColorFgBg Blue = Make(Terminal.Gui.Color.Blue);
    public static readonly ColorFgBg Green = Make(Terminal.Gui.Color.Green);
    public static readonly ColorFgBg Cyan = Make(Terminal.Gui.Color.Cyan);
    public static readonly ColorFgBg Red = Make(Terminal.Gui.Color.Red);
    public static readonly ColorFgBg Magenta = Make(Terminal.Gui.Color.Magenta);
    public static readonly ColorFgBg Yellow = Make(Terminal.Gui.Color.Brown);
    public static readonly ColorFgBg Gray = Make(Terminal.Gui.Color.Gray);
    public static readonly ColorFgBg Dark = Make(Terminal.Gui.Color.DarkGray);
    public static readonly ColorFgBg BrightBlue = Make(Terminal.Gui.Color.BrightBlue);
    public static readonly ColorFgBg BrightGreen = Make(Terminal.Gui.Color.BrightGreen);
    public static readonly ColorFgBg BrightCyan = Make(Terminal.Gui.Color.BrightCyan);
    public static readonly ColorFgBg BrightRed = Make(Terminal.Gui.Color.BrightRed);
    public static readonly ColorFgBg BrightMagenta = Make(Terminal.Gui.Color.BrightMagenta);
    public static readonly ColorFgBg BrightYellow = Make(Terminal.Gui.Color.BrightYellow);
    public static readonly ColorFgBg White = Make(Terminal.Gui.Color.White);
    public static readonly ColorFgBg Black = Make(Terminal.Gui.Color.Black);

    public static readonly ColorFgBg None = Make(Terminal.Gui.Color.Black);
    public static readonly ColorFgBg Ambiguous = Make(Terminal.Gui.Color.White);

    internal static readonly ColorFgBg[] BranchColors = { Blue, Green, Cyan, Red, Yellow };


    internal static readonly ColorScheme ButtonColorScheme = new ColorScheme()
    {
        Normal = Colors.Black,
        Focus = Colors.Make(Color.White, Color.DarkGray),
        HotNormal = Colors.Blue,
        HotFocus = Colors.Make(Color.White, Color.DarkGray),
        Disabled = Colors.Dark,
    };

    internal static readonly ColorScheme DialogColorScheme = new ColorScheme()
    {
        Normal = Colors.White,
        Focus = Colors.White,
        HotNormal = Colors.White,
        HotFocus = Colors.White,
        Disabled = Colors.Dark,
    };



    internal static ColorFgBg Make(Terminal.Gui.Color fg)
    {
        return Make(fg, Terminal.Gui.Color.Black);
    }

    internal static ColorFgBg Make(Terminal.Gui.Color fg, Terminal.Gui.Color bg)
    {
        return View.Driver.MakeAttribute(fg, bg);
    }
}