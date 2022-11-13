using Terminal.Gui;
using TextColor = Terminal.Gui.Attribute;


namespace gmd.Cui;

static class Colors
{
    public static readonly TextColor Blue = Make(Terminal.Gui.Color.Blue);
    public static readonly TextColor Green = Make(Terminal.Gui.Color.Green);
    public static readonly TextColor Cyan = Make(Terminal.Gui.Color.Cyan);
    public static readonly TextColor Red = Make(Terminal.Gui.Color.Red);
    public static readonly TextColor Magenta = Make(Terminal.Gui.Color.Magenta);
    public static readonly TextColor Yellow = Make(Terminal.Gui.Color.Brown);
    public static readonly TextColor Gray = Make(Terminal.Gui.Color.Gray);
    public static readonly TextColor Dark = Make(Terminal.Gui.Color.DarkGray);
    public static readonly TextColor BrightBlue = Make(Terminal.Gui.Color.BrightBlue);
    public static readonly TextColor BrightGreen = Make(Terminal.Gui.Color.BrightGreen);
    public static readonly TextColor BrightCyan = Make(Terminal.Gui.Color.BrightCyan);
    public static readonly TextColor BrightRed = Make(Terminal.Gui.Color.BrightRed);
    public static readonly TextColor BrightMagenta = Make(Terminal.Gui.Color.BrightMagenta);
    public static readonly TextColor BrightYellow = Make(Terminal.Gui.Color.BrightYellow);
    public static readonly TextColor White = Make(Terminal.Gui.Color.White);
    public static readonly TextColor Black = Make(Terminal.Gui.Color.Black);

    public static readonly TextColor None = Make(Terminal.Gui.Color.Black);
    public static readonly TextColor Ambiguous = Make(Terminal.Gui.Color.White);

    internal static readonly TextColor[] BranchColors = { Blue, Green, Cyan, Red, Yellow };


    static TextColor Make(Terminal.Gui.Color fg)
    {
        return Make(fg, Terminal.Gui.Color.Black);
    }

    public static TextColor Make(Terminal.Gui.Color fg, Terminal.Gui.Color bg)
    {
        return View.Driver.MakeAttribute(fg, bg);
    }
}