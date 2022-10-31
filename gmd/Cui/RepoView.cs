using gmd.Utils.Git.Private;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace gmd.Cui;


internal class RepoView : TextView
{
    private Attribute blue;
    private Attribute white;
    private Attribute magenta;

    internal RepoView() : base()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        ReadOnly = true;
        DesiredCursorVisibility = CursorVisibility.Vertical;

        magenta = Driver.MakeAttribute(Color.Magenta, Color.Black);
        blue = Driver.MakeAttribute(Color.Cyan, Color.Black);
        white = Driver.MakeAttribute(Color.White, Color.Black);
    }

    protected override void SetNormalColor()
    {
        Driver.SetAttribute(white);
    }

    protected override void SetReadOnlyColor(List<System.Rune> line, int idx)
    {
        if (idx % 3 == 0)
        {
            Driver.SetAttribute(magenta);
        }
        else if (idx % 3 == 1)
        {
            Driver.SetAttribute(blue);
        }
        else
        {
            Driver.SetAttribute(white);
        }


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

        var lines = commits.Value.Select(c => $"┃ ┃ {c.Sid} {c.Subject.Max(50),-50} {c.Author.Max(10),-10}");

        var t = string.Join('\n', lines) + "\n";
        Text = t + t + t + t;
    }



    // public static class Colors
    // {
    //     public static readonly string colorEnd = "\033[0m";
    //     public static readonly string colorWhite = "\033[37;2m";
    //     public static readonly string colorGray = "\033[37;3m";
    //     public static readonly string colorDark = "\033[30;1m";
    //     public static readonly string colorRed = "\033[31;1m";
    //     public static readonly string colorRedDk = "\033[31;3m";
    //     public static readonly string colorGreen = "\033[32;1m";
    //     public static readonly string colorGreenDk = "\033[32;3m";
    //     public static readonly string colorYellow = "\033[33;1m";
    //     public static readonly string colorYellowDk = "\033[33;3m";
    //     public static readonly string colorBlue = "\033[34;1m";
    //     public static readonly string colorBlueDk = "\033[34;3m";
    //     public static readonly string colorMagenta = "\033[35;1m";
    //     public static readonly string colorMagentaDk = "\033[35;3m";
    //     public static readonly string colorCyan = "\033[36;1m";
    //     public static readonly string colorCyanDk = "\033[36;3m";
    // }



    // private string Red(string text)
    // {
    //     return Colors.colorRed + text + Colors.colorEnd;
    // }
}