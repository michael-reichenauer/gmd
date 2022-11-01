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
    }
}
