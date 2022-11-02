
using System.Text;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;


namespace gmd.Cui;



class ColorText
{
    View view;
    int row = 0;

    internal ColorText(View view)
    {
        this.view = view;
    }

    public void Reset()
    {
        row = 0;
        view.Move(0, 0);
    }

    public void EoL() => view.Move(0, ++row);

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


    public void Add(string text, Attribute color)
    {
        View.Driver.SetAttribute(color);
        View.Driver.AddStr(text);
    }
}


// class ColorText
// {
//     readonly List<StringBuilder> lines;
//     readonly List<List<Attribute>> colors;

//     StringBuilder currentLine;
//     List<Attribute> currentColors;
//     int maxWidth = 0;

//     internal ColorText()
//     {
//         lines = new List<StringBuilder>();
//         colors = new List<List<Attribute>>();

//         currentLine = new StringBuilder();
//         lines.Add(currentLine);

//         currentColors = new List<Attribute>();
//         colors.Add(currentColors);
//     }

//     public void Append(string text, Attribute color)
//     {
//         if (text.Length > maxWidth)
//         {
//             maxWidth = text.Length;
//         }

//         currentLine.Append(text);
//         currentColors.AddRange(Enumerable.Range(0, text.Length).Select(i => color));
//     }

//     public void EoL()
//     {
//         currentLine = new StringBuilder();
//         lines.Add(currentLine);

//         currentColors = new List<Attribute>();
//         colors.Add(currentColors);
//     }

//     public Attribute GetColor(int x, int row)
//     {
//         return colors[row][x];
//     }

//     public override string ToString() => string.Join('\n', lines.Select(sb => sb.ToString()));

//     public string ToString(int first, int count)
//     {
//         first = Math.Min(lines.Count - 1, first);
//         count = Math.Min(count, lines.Count - first);

//         return string.Join('\n', lines.Skip(first).Take(count).Select(sb => sb.ToString()));
//     }

//     internal Terminal.Gui.Size GetContentSize()
//     {
//         return new Terminal.Gui.Size(maxWidth, lines.Count);
//     }
// }