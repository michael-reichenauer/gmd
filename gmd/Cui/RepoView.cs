using gmd.Cui.TerminalGui;
using gmd.Utils.Git.Private;
using Terminal.Gui;


namespace gmd.Cui;

internal class RepoView
{
    TextViewEx textView;
    private ColorText text;


    public View View => textView;

    internal RepoView() : base()
    {
        text = new ColorText();
        textView = new TextViewEx(text)
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
                text.Append(" ┃ ┃", Colors.Magenta);
                text.Append($" {c.Sid}", Colors.Blue);
                text.Append($" {c.Subject.Max(50),-50}", Colors.White);
                text.Append($" {c.Author.Max(10),-10}", Colors.Green);
                text.Append($" {c.AuthorTime.ToString().Max(10),-10}", Colors.DarkGray);
                text.EoL();
            }

            textView.Text = text.ToString();
        }
    }
}
