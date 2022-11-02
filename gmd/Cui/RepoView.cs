using gmd.Utils.Git;
using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;

interface IRepoView
{
    View View { get; }
    void SetRepo(ViewRepo repo);
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

    public void SetRepo(ViewRepo repo)
    {
        var commits = repo.Commits;

        contentView.ShowCommits(commits);
    }
}
