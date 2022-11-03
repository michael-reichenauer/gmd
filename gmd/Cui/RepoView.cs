using gmd.Utils.Git;
using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;

interface IRepoView
{
    View View { get; }
    Task<R> ShowRepoAsync(string path);
    Task<R> ShowRepoAsync(string path, string[] showBranches);
}

class RepoView : IRepoView
{
    readonly IViewRepoService viewRepoService;
    readonly ContentView contentView;
    readonly IRepoLayout repoLayout;

    Repo? repo;
    int TotalRows => repo?.Commits.Count ?? 0;

    public View View => contentView;


    internal RepoView(IViewRepoService viewRepoService) : base()
    {
        this.viewRepoService = viewRepoService;

        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WantMousePositionReports = false,
        };

        repoLayout = new RepoLayout(contentView, contentView.ContentX);
    }

    public Task<R> ShowRepoAsync(string path) =>
        ShowRepoAsync(path, new string[0]);


    public async Task<R> ShowRepoAsync(string path, string[] showBranches)
    {
        var repo = await viewRepoService.GetRepoAsync(path, showBranches);
        if (repo.IsError)
        {
            return repo.Error;
        }

        // Trigger content view to show repo
        this.repo = repo.Value;
        contentView.TriggerUpdateContent(TotalRows);
        return R.Ok;
    }

    void onDrawRepoContent(int width, int Height, int firstIndex, int currentIndex)
    {
        if (repo == null)
        {
            return;
        }

        int firstCommit = Math.Min(firstIndex, TotalRows);
        int commitCount = Math.Min(Height, TotalRows - firstCommit);

        repoLayout.WriteRepo(repo, width, firstCommit, commitCount, currentIndex);
    }


}
