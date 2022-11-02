using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;


namespace gmd.Cui;

class Colors
{
    public static readonly Attribute Blue = MakeColor(Color.Blue);
    public static readonly Attribute Green = MakeColor(Color.Green);
    public static readonly Attribute Cyan = MakeColor(Color.Cyan);
    public static readonly Attribute Red = MakeColor(Color.Red);
    public static readonly Attribute Magenta = MakeColor(Color.Magenta);
    public static readonly Attribute Yellow = MakeColor(Color.Brown);
    public static readonly Attribute Gray = MakeColor(Color.Gray);
    public static readonly Attribute DarkGray = MakeColor(Color.DarkGray);
    public static readonly Attribute BrightBlue = MakeColor(Color.BrightBlue);
    public static readonly Attribute BrightGreen = MakeColor(Color.BrightGreen);
    public static readonly Attribute BrightCyan = MakeColor(Color.BrightCyan);
    public static readonly Attribute BrightRed = MakeColor(Color.BrightRed);
    public static readonly Attribute BrightMagenta = MakeColor(Color.BrightMagenta);
    public static readonly Attribute BrightYellow = MakeColor(Color.BrightYellow);
    public static readonly Attribute White = MakeColor(Color.White);



    static Attribute MakeColor(Color fg)
    {
        return MakeColorFgBg(fg, Color.Black);
    }

    static Attribute MakeColorFgBg(Color fg, Color bg)
    {
        return View.Driver.MakeAttribute(fg, bg);
    }
}