using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;

interface IRepoView
{
    int CurrentIndex { get; }
    View View { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }

    Task<R> ShowRepoAsync(string path);
    Task<R> ShowRepoAsync(string path, string[] showBranches);
    void UpdateRepo(Repo repo);
    void Refresh();
}

class RepoView : IRepoView
{
    static readonly TimeSpan minRepoUpdateInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan minStatusUpdateInterval = TimeSpan.FromMilliseconds(100);
    readonly IViewRepoService viewRepoService;
    readonly Func<IRepoView, Repo, IRepo> newViewRepo;
    readonly Func<IRepo, IDiffView> newDiffView;
    readonly IMenuService menuService;
    readonly ContentView contentView;
    readonly IRepoWriter repoWriter;

    // State data
    IRepo? repo; // Is set once the repo has been retrieved the first time in ShowRepo()


    internal RepoView(
        IViewRepoService viewRepoService,
        Func<IRepo, IDiffView> newDiffView,
        Func<View, int, IRepoWriter> newRepoWriter,
        Func<IRepoView, Repo, IRepo> newViewRepo,
        IMenuService menuService) : base()
    {
        this.viewRepoService = viewRepoService;
        this.newDiffView = newDiffView;
        this.newViewRepo = newViewRepo;
        this.menuService = menuService;
        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        repoWriter = newRepoWriter(contentView, contentView.ContentX);

        viewRepoService.RepoChange += (s, e) => OnRefresh(e);
        viewRepoService.StatusChange += (s, e) => OnRefreshStatus(e);

        RegisterKeyHandlers();
    }

    public View View => contentView;
    public int ContentWidth => contentView.ContentWidth;

    public int CurrentIndex => contentView.CurrentIndex;
    public Point CurrentPoint => contentView.CurrentPoint;

    void CommitAll() => repo!.Commit();
    void OnMenuKey() => menuService.ShowMainMenu(repo!);
    void OnRightArrow() => menuService.ShowShowBranchesMenu(repo!);
    void OnLeftArrow() => menuService.ShowHideBranchesMenu(repo!);

    public Task<R> ShowRepoAsync(string path) =>
        ShowRepoAsync(path, new string[0]);

    public Task<R> ShowRepoAsync(string path, string[] showBranches) =>
        ShowNewRepoAsync(path, showBranches);

    public void UpdateRepo(Repo repo) => ShowRepo(repo);

    public void Refresh() => ShowRefreshedRepoAsync().RunInBackground();


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
        contentView.RegisterKeyHandler(Key.D | Key.CtrlMask, ShowDiff);
        contentView.RegisterKeyHandler(Key.p, PushCurrentBranch);
    }

    private void ShowDiff()
    {
        var diffView = newDiffView(repo!);
        diffView.ShowCurrentRow();
    }

    void PushCurrentBranch() => repo!.PushCurrentBranch();


    void OnRefresh(ChangeEventArgs e)
    {
        Log.Info($"Current: {repo!.Repo.TimeStamp.Iso()}");
        Log.Info($"New    : {e.TimeStamp.Iso()}");

        if (e.TimeStamp - repo!.Repo.TimeStamp < minRepoUpdateInterval)
        {
            Log.Warn("New repo event to soon, skipping update");
            return;
        }
        ShowRefreshedRepoAsync().RunInBackground();
    }

    void OnRefreshStatus(ChangeEventArgs e)
    {
        Log.Info($"Current: {repo!.Repo.TimeStamp.Iso()}");
        Log.Info($"New    : {e.TimeStamp.Iso()}");

        if (e.TimeStamp - repo!.Repo.TimeStamp < minStatusUpdateInterval)
        {
            Log.Warn("New status event to soon, skipping update");
            return;
        }
        ShowUpdatedStatusRepoAsync().RunInBackground();
    }


    void onDrawRepoContent(Rect bounds, int firstIndex, int currentIndex)
    {
        if (repo == null)
        {
            return;
        }

        int firstCommit = Math.Min(firstIndex, repo.TotalRows);
        int commitCount = Math.Min(bounds.Height, repo.TotalRows - firstCommit);

        repoWriter.WriteRepoPage(repo, firstCommit, commitCount);
    }

    async Task<R> ShowNewRepoAsync(string path, string[] showBranches)
    {
        var t = Timing.Start();
        if (!Try(out var repo, out var e, await viewRepoService.GetRepoAsync(path, showBranches)))
        {
            return e;
        }

        ShowRepo(repo);
        Log.Info($"{t}");
        return R.Ok;
    }

    async Task ShowRefreshedRepoAsync()
    {
        Log.Info("show refreshed repo ...");
        var t = Timing.Start();

        if (!Try(out var viewRepo, out var e, await viewRepoService.GetFreshRepoAsync(repo!.Repo!)))
        {
            UI.ErrorMessage($"Failed to refresh:\n{e}");
            return;
        }

        ShowRepo(viewRepo);
        Log.Info($"{t}");
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        var t = Timing.Start();
        if (!Try(out var viewRepo, out var e, await viewRepoService.GetUpdateStatusRepoAsync(repo!.Repo)))
        {
            UI.ErrorMessage($"Failed to update status:\n{e}");
            return;
        }

        ShowRepo(viewRepo);
        Log.Info($"{t}");
    }


    void ShowRepo(Repo repo)
    {
        this.repo = newViewRepo(this, repo);
        contentView.TriggerUpdateContent(this.repo.TotalRows);
    }
}
