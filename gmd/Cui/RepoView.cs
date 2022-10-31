using System.Collections;
using System.Text;
using gmd.Utils;
using gmd.Utils.Git.Private;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace gmd.Cui;



internal class RepoView : TextViewX
{


    private ColorText colorText;

    internal RepoView() : base()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        ReadOnly = true;
        DesiredCursorVisibility = CursorVisibility.Vertical;
        WantMousePositionReports = false;

        colorText = new ColorText(Driver);
    }

    protected override void SetNormalColor()
    {
        Driver.SetAttribute(colorText.Default);
    }


    protected override void SetReadOnlyColor(List<System.Rune> line, int idx, int row)
    {
        Driver.SetAttribute(colorText.GetColor(idx, row));

        // string strLine = new string(line.Select(r => (char)r).ToArray());

        // // Log.Info($"idx:{idx}, x:{x}, ld:{ld}");
        // Log.Info($"Line: {row,2}{strLine}");




        // //Log.Info($"set {this.TopRow}, {idx} {x}");
        // // Log.Info($"set {this.TopRow + ld} {strLine}");
        // if (idx % 3 == 0)
        // {
        //     Driver.SetAttribute(magenta);
        // }
        // else if (idx % 3 == 1)
        // {
        //     Driver.SetAttribute(blue);
        // }
        // else
        // {
        //     Driver.SetAttribute(white);
        // }


        // Driver.SetAttribute(magenta);

        // if (IsInStringLiteral(line, idx))
        // {
        //     Driver.SetAttribute(magenta);
        // }
        // else
        // if (IsKeyword(line, idx))
        // {
        //     Driver.SetAttribute(blue);
        // }
        // else
        // {
        //     Driver.SetAttribute(white);
        // }
    }


    internal async Task SetData()
    {
        var git = new GitRepo("");

        var commits = await git.GetLog();
        if (commits.IsFaulted)
        {
            return;
        }

        // var lines = commits.Value.Select(c => $"┃ ┃ {c.Sid} {c.Subject.Max(50),-50} {c.Author.Max(10),-10}");

        for (int i = 0; i < 10; i++)
        {
            foreach (var c in commits.Value)
            {
                colorText.Magenta(" ┃ ┃");
                colorText.Blue($" {c.Sid}");
                colorText.White($" {c.Subject.Max(50),-50}");
                colorText.Red($" {c.Author.Max(10),-10}");
                colorText.EoL();
            }

            Text = colorText.ToString();
        }
    }
}

internal class ColorText
{
    private readonly ConsoleDriver driver;
    private readonly List<StringBuilder> lines;
    private readonly List<List<Attribute>> colors;

    private StringBuilder currentLine;
    private List<Attribute> currentColors;

    private Attribute magenta;
    private Attribute blue;
    private Attribute white;
    private Attribute red;

    internal ColorText(ConsoleDriver driver)
    {
        this.driver = driver;
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
        magenta = driver.MakeAttribute(Color.Magenta, Color.Black);
        blue = driver.MakeAttribute(Color.Cyan, Color.Black);
        white = driver.MakeAttribute(Color.White, Color.Black);
        red = driver.MakeAttribute(Color.Red, Color.Black);
    }

    private void Add(string text, Attribute color)
    {
        currentLine.Append(text);
        currentColors.AddRange(Enumerable.Range(0, text.Length).Select(i => color));
    }
}