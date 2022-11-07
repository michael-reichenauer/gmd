using Terminal.Gui;
using Color = Terminal.Gui.Attribute;


namespace gmd.Cui;

class Colors
{
    public static readonly Color Blue = MakeColor(Terminal.Gui.Color.Blue);
    public static readonly Color Green = MakeColor(Terminal.Gui.Color.Green);
    public static readonly Color Cyan = MakeColor(Terminal.Gui.Color.Cyan);
    public static readonly Color Red = MakeColor(Terminal.Gui.Color.Red);
    public static readonly Color Magenta = MakeColor(Terminal.Gui.Color.Magenta);
    public static readonly Color Yellow = MakeColor(Terminal.Gui.Color.Brown);
    public static readonly Color Gray = MakeColor(Terminal.Gui.Color.Gray);
    public static readonly Color DarkGray = MakeColor(Terminal.Gui.Color.DarkGray);
    public static readonly Color BrightBlue = MakeColor(Terminal.Gui.Color.BrightBlue);
    public static readonly Color BrightGreen = MakeColor(Terminal.Gui.Color.BrightGreen);
    public static readonly Color BrightCyan = MakeColor(Terminal.Gui.Color.BrightCyan);
    public static readonly Color BrightRed = MakeColor(Terminal.Gui.Color.BrightRed);
    public static readonly Color BrightMagenta = MakeColor(Terminal.Gui.Color.BrightMagenta);
    public static readonly Color BrightYellow = MakeColor(Terminal.Gui.Color.BrightYellow);
    public static readonly Color White = MakeColor(Terminal.Gui.Color.White);
    public static readonly Color Black = MakeColor(Terminal.Gui.Color.Black);

    public static readonly Color None = MakeColor(Terminal.Gui.Color.Black);
    public static readonly Color Ambiguous = MakeColor(Terminal.Gui.Color.White);

    internal static readonly Color[] BranchColors = { Blue, Green, Cyan, Red, Yellow };


    static Color MakeColor(Terminal.Gui.Color fg)
    {
        return MakeColorFgBg(fg, Terminal.Gui.Color.Black);
    }

    static Color MakeColorFgBg(Terminal.Gui.Color fg, Terminal.Gui.Color bg)
    {
        return View.Driver.MakeAttribute(fg, bg);
    }
}