using Terminal.Gui;
using Color = Terminal.Gui.Attribute;


namespace gmd.Cui.Common;

class ColorText
{
    View view;
    private readonly int startX;
    int row = 0;

    internal ColorText(View view, int startX)
    {
        this.view = view;
        this.startX = startX;
    }

    public void Reset()
    {
        row = 0;
        view.Move(startX, 0);
    }

    public void EoL() => view.Move(startX, ++row);

    public void Red(string text) => Add(text, TextColor.Red);
    public void Blue(string text) => Add(text, TextColor.Blue);
    public void White(string text) => Add(text, TextColor.White);
    public void Magenta(string text) => Add(text, TextColor.Magenta);
    public void BrightBlue(string text) => Add(text, TextColor.BrightBlue);
    public void BrightCyan(string text) => Add(text, TextColor.BrightCyan);
    public void BrightGreen(string text) => Add(text, TextColor.BrightGreen);
    public void BrightMagenta(string text) => Add(text, TextColor.BrightMagenta);
    public void BrightRed(string text) => Add(text, TextColor.BrightRed);
    public void BrightYellow(string text) => Add(text, TextColor.BrightYellow);
    public void Cyan(string text) => Add(text, TextColor.Cyan);
    public void Dark(string text) => Add(text, TextColor.Dark);
    public void Green(string text) => Add(text, TextColor.Green);
    public void Yellow(string text) => Add(text, TextColor.Yellow);
    public void Black(string text) => Add(text, TextColor.Black);


    public void Add(string text, Color color)
    {
        View.Driver.SetAttribute(color);
        View.Driver.AddStr(text);
    }

    public void Add(System.Rune rune, Color color)
    {
        View.Driver.SetAttribute(color);
        View.Driver.AddRune(rune);
    }
}
