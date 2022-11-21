using Terminal.Gui;


namespace gmd.Cui.Common;

static class TextColor
{
    public static readonly Terminal.Gui.Attribute Blue = Make(Terminal.Gui.Color.Blue);
    public static readonly Terminal.Gui.Attribute Green = Make(Terminal.Gui.Color.Green);
    public static readonly Terminal.Gui.Attribute Cyan = Make(Terminal.Gui.Color.Cyan);
    public static readonly Terminal.Gui.Attribute Red = Make(Terminal.Gui.Color.Red);
    public static readonly Terminal.Gui.Attribute Magenta = Make(Terminal.Gui.Color.Magenta);
    public static readonly Terminal.Gui.Attribute Yellow = Make(Terminal.Gui.Color.Brown);
    public static readonly Terminal.Gui.Attribute Dark = Make(Terminal.Gui.Color.DarkGray);
    public static readonly Terminal.Gui.Attribute BrightBlue = Make(Terminal.Gui.Color.BrightBlue);
    public static readonly Terminal.Gui.Attribute BrightGreen = Make(Terminal.Gui.Color.BrightGreen);
    public static readonly Terminal.Gui.Attribute BrightCyan = Make(Terminal.Gui.Color.BrightCyan);
    public static readonly Terminal.Gui.Attribute BrightRed = Make(Terminal.Gui.Color.BrightRed);
    public static readonly Terminal.Gui.Attribute BrightMagenta = Make(Terminal.Gui.Color.BrightMagenta);
    public static readonly Terminal.Gui.Attribute BrightYellow = Make(Terminal.Gui.Color.BrightYellow);
    public static readonly Terminal.Gui.Attribute White = Make(Terminal.Gui.Color.White);
    public static readonly Terminal.Gui.Attribute Black = Make(Terminal.Gui.Color.Black);

    public static readonly Terminal.Gui.Attribute None = Make(Terminal.Gui.Color.Black);
    public static readonly Terminal.Gui.Attribute Ambiguous = Make(Terminal.Gui.Color.White);

    internal static readonly Terminal.Gui.Attribute[] BranchColors = { Blue, Green, Cyan, Red, Yellow };


    static Terminal.Gui.Attribute Make(Terminal.Gui.Color fg)
    {
        return Make(fg, Terminal.Gui.Color.Black);
    }

    static Terminal.Gui.Attribute HighMake(Terminal.Gui.Color fg)
    {
        return Make(fg, Terminal.Gui.Color.DarkGray);
    }


    public static Terminal.Gui.Attribute Make(Terminal.Gui.Color fg, Terminal.Gui.Color bg)
    {
        return View.Driver.MakeAttribute(fg, bg);
    }
}