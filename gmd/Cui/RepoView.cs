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
    void Refresh();
}

class RepoView : IRepoView, IRepo
{
    readonly IViewRepoService viewRepoService;
    readonly IGraphService graphService;
    readonly IMenuService menuService;
    private readonly IRepoCommands repoCommands;
    readonly ContentView contentView;
    readonly IRepoWriter repoLayout;

    Repo? repo = null;
    Graph? graph = null;
    int TotalRows => repo?.Commits.Count ?? 0;

    public View View => contentView;
    public int ViewWidth => contentView.ViewWidth;
    public Repo Repo => repo!;
    public int CurrentIndex => contentView.CurrentIndex;
    public Point CurrentPoint => contentView.CurrentPoint;


    internal RepoView(
        IViewRepoService viewRepoService,
        IGraphService graphService,
        IMenuService menuService,
        IRepoCommands repoCommands) : base()
    {
        this.viewRepoService = viewRepoService;
        this.graphService = graphService;
        this.menuService = menuService;
        this.repoCommands = repoCommands;
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
    void RegisterKeyHandlers()
    {
        contentView.RegisterKeyHandler(Key.m, OnMenuKey);
        contentView.RegisterKeyHandler(Key.M, OnMenuKey);
        contentView.RegisterKeyHandler(Key.CursorRight, OnRightArrow);
        contentView.RegisterKeyHandler(Key.CursorLeft, OnLeftArrow);
        contentView.RegisterKeyHandler(Key.r, Refresh);
        contentView.RegisterKeyHandler(Key.R, Refresh);
        contentView.RegisterKeyHandler(Key.c, CommitAll);
    }

    void CommitAll() => repoCommands.CommitAsync(this).RunInBackground();
    void OnMenuKey() => menuService.ShowMainMenu(this);
    void OnRightArrow() => menuService.ShowShowBranchesMenu(this);
    void OnLeftArrow() => menuService.ShowHideBranchesMenu(this);


    public Task<R> ShowRepoAsync(string path) =>
        ShowRepoAsync(path, new string[0]);


    public void Refresh()
    {
        ShowRefreshedRepoAsync().RunInBackground();
    }


    public async Task<R> ShowRepoAsync(string path, string[] showBranches)
    {
        var t = Timing.Start();
        var repo = await viewRepoService.GetRepoAsync(path, showBranches);
        if (repo.IsError)
        {
            return repo.Error;
        }

        ShowRepo(repo.Value);
        Log.Info($"{t}");
        return R.Ok;
    }

    public void ShowRepo(Repo repo)
    {
        var graph = graphService.CreateGraph(repo);

        if (this.repo == null)
        {   // Register key handlers on first repo
            RegisterKeyHandlers();
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

        repoLayout.WriteRepoPage(graph, repo, width, firstCommit, commitCount, currentIndex);
    }

    async Task ShowRefreshedRepoAsync()
    {
        var t = Timing.Start();
        var repo = await viewRepoService.GetFreshRepoAsync(this.repo!);
        if (repo.IsError)
        {
            UI.ErrorMessage($"Failed to refresh:\n{repo.Error.Message}");
            return;
        }

        ShowRepo(repo.Value);
        Log.Info($"{t}");
    }
}
