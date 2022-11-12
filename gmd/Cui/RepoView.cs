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
    void UpdateRepoTo(Repo repo);
    void Refresh();
}

class RepoView : IRepoView
{
    static readonly TimeSpan minRepoUpdateInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan minStatusUpdateInterval = TimeSpan.FromMilliseconds(100);
    readonly IViewRepoService viewRepoService;
    readonly Func<IRepoView, Repo, IRepo> newViewRepo;
    private readonly Func<IRepo, IRepoViewMenus> newMenuService;
    readonly Func<IRepo, IDiffView> newDiffView;
    readonly ContentView contentView;
    readonly IRepoWriter repoWriter;

    // State data
    IRepo? repo; // Is set once the repo has been retrieved the first time in ShowRepo().
    IRepoViewMenus? menuService;


    internal RepoView(
        IViewRepoService viewRepoService,
        Func<IRepo, IDiffView> newDiffView,
        Func<View, int, IRepoWriter> newRepoWriter,
        Func<IRepoView, Repo, IRepo> newViewRepo,
        Func<IRepo, IRepoViewMenus> newMenuService) : base()
    {
        this.viewRepoService = viewRepoService;
        this.newDiffView = newDiffView;
        this.newViewRepo = newViewRepo;
        this.newMenuService = newMenuService;
        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        repoWriter = newRepoWriter(contentView, contentView.ContentX);

        viewRepoService.RepoChange += OnRefresh;
        viewRepoService.StatusChange += OnRefreshStatus;

        RegisterKeyHandlers();
    }


    public View View => contentView;
    public int ContentWidth => contentView.ContentWidth;

    public int CurrentIndex => contentView.CurrentIndex;
    public Point CurrentPoint => contentView.CurrentPoint;

    void CommitAll() => repo!.Commit();
    void OnMenuKey() => menuService!.ShowMainMenu();
    void OnRightArrow() => menuService!.ShowShowBranchesMenu();
    void OnLeftArrow() => menuService!.ShowHideBranchesMenu();

    public Task<R> ShowRepoAsync(string path) =>
        ShowRepoAsync(path, new string[0]);

    public Task<R> ShowRepoAsync(string path, string[] showBranches) =>
        ShowNewRepoAsync(path, showBranches);

    public void UpdateRepoTo(Repo repo) => ShowRepo(repo);

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


    void OnRefresh(ChangeEvent e)
    {
        Log.Debug($"Current: {repo!.Repo.TimeStamp.Iso()}");
        Log.Debug($"New    : {e.TimeStamp.Iso()}");

        if (e.TimeStamp - repo!.Repo.TimeStamp < minRepoUpdateInterval)
        {
            Log.Debug("New repo event to soon, skipping update");
            return;
        }
        ShowRefreshedRepoAsync().RunInBackground();
    }

    void OnRefreshStatus(ChangeEvent e)
    {
        Log.Debug($"Current: {repo!.Repo.TimeStamp.Iso()}");
        Log.Debug($"New    : {e.TimeStamp.Iso()}");

        if (e.TimeStamp - repo!.Repo.TimeStamp < minStatusUpdateInterval)
        {
            Log.Debug("New status event to soon, skipping update");
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
        var t = Timing.Start;
        if (!Try(out var viewRepo, out var e,
            await viewRepoService.GetRepoAsync(path, showBranches))) return e;

        ShowRepo(viewRepo);
        Log.Info($"{t} {viewRepo}");
        return R.Ok;
    }

    async Task ShowRefreshedRepoAsync()
    {
        Log.Info("show refreshed repo ...");
        var t = Timing.Start;

        if (!Try(out var viewRepo, out var e,
            await viewRepoService.GetFreshRepoAsync(repo!.Repo!)))
        {
            UI.ErrorMessage($"Failed to refresh:\n{e}");
            return;
        }

        ShowRepo(viewRepo);
        Log.Info($"{t} {viewRepo}");
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        var t = Timing.Start;
        if (!Try(out var viewRepo, out var e,
            await viewRepoService.GetUpdateStatusRepoAsync(repo!.Repo)))
        {
            UI.ErrorMessage($"Failed to update status:\n{e}");
            return;
        }

        ShowRepo(viewRepo);
        Log.Info($"{t} {viewRepo}");
    }


    void ShowRepo(Repo repo)
    {
        this.repo = newViewRepo(this, repo);
        this.menuService = newMenuService(this.repo);
        contentView.TriggerUpdateContent(this.repo.TotalRows);
    }
}
