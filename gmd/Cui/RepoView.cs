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
    private readonly IRepoLayout repoLayout;
    private TextViewEx textView;
    private ColorText text;

    public View View => textView;

    RepoView(IGitService gitService, IRepoLayout repoLayout) : base()
    {
        this.gitService = gitService;
        this.repoLayout = repoLayout;

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

        repoLayout.SetText(commits.Value, text);

        textView.Text = text.ToString();
    }
}
