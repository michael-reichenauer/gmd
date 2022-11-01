using gmd.Cui.TerminalGui;
using gmd.Utils.Git;
using Terminal.Gui;


namespace gmd.Cui;

interface IRepoView
{
    View View { get; }
    Task SetDataAsync();
}

class RepoView : IRepoView
{
    private readonly IGitService gitService;

    private TextViewEx textView;
    private ColorText text;

    public View View => textView;

    RepoView(IGitService gitService) : base()
    {
        this.gitService = gitService;

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

    public async Task SetDataAsync()
    {
        var git = gitService.GetRepo("");

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
