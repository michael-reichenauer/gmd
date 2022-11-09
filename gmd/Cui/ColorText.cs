using Terminal.Gui;
using Color = Terminal.Gui.Attribute;


namespace gmd.Cui;

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

    public void Red(string text) => Add(text, Colors.Red);
    public void Blue(string text) => Add(text, Colors.Blue);
    public void White(string text) => Add(text, Colors.White);
    public void Magenta(string text) => Add(text, Colors.Magenta);
    public void BrightBlue(string text) => Add(text, Colors.BrightBlue);
    public void BrightCyan(string text) => Add(text, Colors.BrightCyan);
    public void BrightGreen(string text) => Add(text, Colors.BrightGreen);
    public void BrightMagenta(string text) => Add(text, Colors.BrightMagenta);
    public void BrightRed(string text) => Add(text, Colors.BrightRed);
    public void BrightYellow(string text) => Add(text, Colors.BrightYellow);
    public void Cyan(string text) => Add(text, Colors.Cyan);
    public void DarkGray(string text) => Add(text, Colors.DarkGray);
    public void Gray(string text) => Add(text, Colors.Gray);
    public void Green(string text) => Add(text, Colors.Green);
    public void Yellow(string text) => Add(text, Colors.Yellow);
    public void Black(string text) => Add(text, Colors.Black);


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
