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
    static readonly TimeSpan minRepoUpdateInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan minStatusUpdateInterval = TimeSpan.FromMilliseconds(100);

    readonly IViewRepoService viewRepoService;
    private readonly IDiffView diffView;
    readonly IGraphService graphService;
    readonly IMenuService menuService;
    private readonly IRepoCommands repoCommands;
    readonly ContentView contentView;
    readonly IRepoWriter repoWriter;

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
        IDiffView diffView,
        IGraphService graphService,
        IMenuService menuService,
        IRepoCommands repoCommands) : base()
    {
        this.viewRepoService = viewRepoService;
        this.diffView = diffView;
        this.graphService = graphService;
        this.menuService = menuService;
        this.repoCommands = repoCommands;
        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            // WantMousePositionReports = false,
        };

        repoWriter = new RepoWriter(contentView, contentView.ContentX);

        viewRepoService.RepoChange += (s, e) => OnRefresh(e);
        viewRepoService.StatusChange += (s, e) => OnRefreshStatus(e);

        RegisterKeyHandlers();
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
        contentView.RegisterKeyHandler(Key.C, CommitAll);

        contentView.RegisterKeyHandler(Key.d, ShowDiff);
        contentView.RegisterKeyHandler(Key.D, ShowDiff);
    }

    private void ShowDiff()
    {
        diffView.Show(Repo, Repo.UncommittedId);
    }

    void CommitAll() => repoCommands.CommitAsync(this).RunInBackground();
    void OnMenuKey() => menuService.ShowMainMenu(this);
    void OnRightArrow() => menuService.ShowShowBranchesMenu(this);
    void OnLeftArrow() => menuService.ShowHideBranchesMenu(this);


    public Task<R> ShowRepoAsync(string path) =>
        ShowRepoAsync(path, new string[0]);


    public void Refresh() => ShowRefreshedRepoAsync().RunInBackground();

    void OnRefresh(ChangeEventArgs e)
    {
        Log.Info($"Current: {Repo.TimeStamp.Iso()}");
        Log.Info($"New    : {e.TimeStamp.Iso()}");

        if (e.TimeStamp - Repo.TimeStamp < minRepoUpdateInterval)
        {
            Log.Warn("New repo event to soon, skipping update");
            return;
        }
        ShowRefreshedRepoAsync().RunInBackground();
    }

    void OnRefreshStatus(ChangeEventArgs e)
    {
        Log.Info($"Current: {Repo.TimeStamp.Iso()}");
        Log.Info($"New    : {e.TimeStamp.Iso()}");
        if (e.TimeStamp - Repo.TimeStamp < minStatusUpdateInterval)
        {
            Log.Warn("New status event to soon, skipping update");
            return;
        }
        ShowUpdatedStatusRepoAsync().RunInBackground();
    }


    public async Task<R> ShowRepoAsync(string path, string[] showBranches)
    {
        var t = Timing.Start();
        if (!Try(out repo, out var e, await viewRepoService.GetRepoAsync(path, showBranches)))
        {
            return e;
        }

        ShowRepo(repo);
        Log.Info($"{t}");
        return R.Ok;
    }

    public void ShowRepo(Repo repo)
    {
        graph = graphService.CreateGraph(repo);

        contentView.TriggerUpdateContent(TotalRows);
    }

    void onDrawRepoContent(Rect bounds, int firstIndex, int currentIndex)
    {
        if (repo == null || graph == null)
        {
            return;
        }

        int firstCommit = Math.Min(firstIndex, TotalRows);
        int commitCount = Math.Min(bounds.Height, TotalRows - firstCommit);

        repoWriter.WriteRepoPage(graph, repo, bounds.Width, firstCommit, commitCount, currentIndex);
    }

    async Task ShowRefreshedRepoAsync()
    {
        Log.Info("show refresh");
        var t = Timing.Start();

        if (!Try(out repo, out var e, await viewRepoService.GetFreshRepoAsync(repo!)))
        {
            UI.ErrorMessage($"Failed to refresh:\n{e}");
            return;
        }

        ShowRepo(repo);
        Log.Info($"{t}");
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        var t = Timing.Start();
        if (!Try(out repo, out var e, await viewRepoService.GetUpdateStatusRepoAsync(repo!)))
        {
            UI.ErrorMessage($"Failed to update status:\n{e}");
            return;
        }

        ShowRepo(repo);
        Log.Info($"{t}");
    }
}
