using gmd.Utils.Git;
using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;


interface IRepoView
{
    View View { get; }
    Task<R> ShowRepoAsync(string path);
    Task<R> ShowRepoAsync(string path, string[] showBranches);
    void ShowRepo(Repo repo);
}

class RepoView : IRepoView
{
    readonly IViewRepoService viewRepoService;
    readonly IGraphService graphService;
    readonly IMenuService menuService;
    readonly ContentView contentView;
    readonly IRepoWriter repoLayout;

    Repo? repo = null;
    Graph? graph = null;

    int TotalRows => repo?.Commits.Count ?? 0;

    public View View => contentView;


    internal RepoView(
        IViewRepoService viewRepoService,
        IGraphService graphService,
        IMenuService menuService) : base()
    {
        this.viewRepoService = viewRepoService;
        this.graphService = graphService;
        this.menuService = menuService;
        this.menuService.SetRepoViewer(this);

        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            WantMousePositionReports = false,
        };

        repoLayout = new RepoWriter(contentView, contentView.ContentX);


    }

    // Called once the repo has been set
    void RegisterKeyHandlers(Repo _)
    {
        contentView.RegisterKeyHandler(Key.m, OnMenuKey);
        contentView.RegisterKeyHandler(Key.CursorRight, OnRightArrow);
        contentView.RegisterKeyHandler(Key.CursorLeft, OnLeftArrow);
    }

    void OnMenuKey() => menuService.ShowMainMenu(repo!, contentView.ViewWidth / 2);
    void OnRightArrow() => menuService.ShowShowBranchesMenu(repo!, contentView.CurrentPoint);
    void OnLeftArrow() => menuService.ShowHideBranchesMenu(repo!, contentView.CurrentPoint);


    public Task<R> ShowRepoAsync(string path) =>
        ShowRepoAsync(path, new string[0]);


    public async Task<R> ShowRepoAsync(string path, string[] showBranches)
    {
        var repo = await viewRepoService.GetRepoAsync(path, showBranches);
        if (repo.IsError)
        {
            return repo.Error;
        }

        ShowRepo(repo.Value);
        return R.Ok;
    }

    public void ShowRepo(Repo repo)
    {
        var graph = graphService.CreateGraph(repo);

        if (this.repo == null)
        {   // Register key handlers on first repo
            RegisterKeyHandlers(repo);
        }

        // Trigger content view to show repo
        this.repo = repo;
        this.graph = graph;
        contentView.TriggerUpdateContent(TotalRows);
    }

    void onDrawRepoContent(int width, int Height, int firstIndex, int currentIndex)
    {
        if (repo == null || graph == null)
        {
            return;
        }

        int firstCommit = Math.Min(firstIndex, TotalRows);
        int commitCount = Math.Min(Height, TotalRows - firstCommit);

        repoLayout.WriteRepo(graph, repo, width, firstCommit, commitCount, currentIndex);
    }
}
