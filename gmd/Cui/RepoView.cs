using gmd.Cui.TerminalGui;
using gmd.Utils.Git.Private;
using Terminal.Gui;


namespace gmd.Cui;

internal class RepoView
{
    TextViewEx textView;
    private ColorText colorText;


    public View View => textView;

    internal RepoView() : base()
    {
        colorText = new ColorText();
        textView = new TextViewEx(colorText)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            DesiredCursorVisibility = CursorVisibility.Vertical,
            WantMousePositionReports = false,
        };
    }


    internal async Task SetDataAsync()
    {
        var git = new GitRepo("");

        var commits = await git.GetLog();
        if (commits.IsFaulted)
        {
            return;
        }

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

            textView.Text = colorText.ToString();
        }
    }
}
