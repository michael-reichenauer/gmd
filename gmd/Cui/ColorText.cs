
using System.Text;
using Attribute = Terminal.Gui.Attribute;


namespace gmd.Cui;

class ColorText
{
    readonly List<StringBuilder> lines;
    readonly List<List<Attribute>> colors;

    StringBuilder currentLine;
    List<Attribute> currentColors;

    internal ColorText()
    {
        lines = new List<StringBuilder>();
        colors = new List<List<Attribute>>();

        currentLine = new StringBuilder();
        lines.Add(currentLine);

        currentColors = new List<Attribute>();
        colors.Add(currentColors);
    }

    public void Append(string text, Attribute color)
    {
        currentLine.Append(text);
        currentColors.AddRange(Enumerable.Range(0, text.Length).Select(i => color));
    }

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
}