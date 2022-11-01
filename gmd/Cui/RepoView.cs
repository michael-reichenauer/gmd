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

    RepoContentView contentView;

    public View View => contentView;

    RepoView(IGitService gitService, IRepoLayout repoLayout) : base()
    {
        this.gitService = gitService;
        this.repoLayout = repoLayout;

        contentView = new RepoContentView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WantMousePositionReports = false,
        };



        // textView = new TextViewEx(text)
        // {
        //     X = 0,
        //     Y = 0,
        //     Width = Dim.Fill(),
        //     Height = Dim.Fill(),
        //     ReadOnly = true,
        //     DesiredCursorVisibility = CursorVisibility.Vertical,
        //     WantMousePositionReports = false,
        // };

        // textView.DrawContent += (r) =>
        // {
        //     int row = textView.CurrentRow;
        //     int x = textView.CurrentColumn;
        //     int top = textView.TopRow;
        //     int maxX = textView.Maxlength;
        //     int lines = textView.Lines;

        //     textView.Text = text.ToString(top, textView.Frame.Height);

        //     Log.Info($"top level frame {textView.Frame}");
        //     Log.Info($"row: {row}, X: {x}, top: {top}, lines: {lines}, maxX:{maxX}");
        // };
    }

    public async Task SetDataAsync()
    {
        var git = gitService.GetRepo("");

        var commits = await git.GetLog();
        if (commits.IsFaulted)
        {
            return;
        }

        contentView.ShowCommits(commits.Value);

        //textView.Text = text.ToString();
    }

    // public Size GetContentSize()
    // {
    //     return text.GetContentSize();
    // }
}
