
using System.Text;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;


namespace gmd.Cui;

internal class ColorText
{
    private readonly List<StringBuilder> lines;
    private readonly List<List<Attribute>> colors;

    private StringBuilder currentLine;
    private List<Attribute> currentColors;

    private Attribute magenta;
    private Attribute blue;
    private Attribute white;
    private Attribute red;

    internal ColorText()
    {
        InitColors();

        lines = new List<StringBuilder>();
        colors = new List<List<Attribute>>();

        currentLine = new StringBuilder();
        lines.Add(currentLine);

        currentColors = new List<Attribute>();
        colors.Add(currentColors);
    }

    public Attribute Default => white;

    public void Normal(string text) => Add(text, white);
    public void White(string text) => Add(text, white);
    public void Red(string text) => Add(text, red);
    public void Blue(string text) => Add(text, blue);
    public void Magenta(string text) => Add(text, magenta);

    public void EoL()
    {
        currentLine = new StringBuilder();
        lines.Add(currentLine);

        currentColors = new List<Attribute>();
        colors.Add(currentColors);
    }

    public Attribute GetColor(int x, int row)
    {
        return colors[row][x];
    }

    public override string ToString() => string.Join('\n', lines.Select(sb => sb.ToString()));

    private void InitColors()
    {
        magenta = View.Driver.MakeAttribute(Color.Magenta, Color.Black);
        blue = View.Driver.MakeAttribute(Color.Cyan, Color.Black);
        white = View.Driver.MakeAttribute(Color.White, Color.Black);
        red = View.Driver.MakeAttribute(Color.Red, Color.Black);
    }

    private void Add(string text, Attribute color)
    {
        currentLine.Append(text);
        currentColors.AddRange(Enumerable.Range(0, text.Length).Select(i => color));
    }
}