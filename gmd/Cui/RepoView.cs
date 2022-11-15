using gmd.Common;
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
    private readonly IState state;
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
        Func<IRepo, IRepoViewMenus> newMenuService,
        IState state) : base()
    {
        this.viewRepoService = viewRepoService;
        this.newDiffView = newDiffView;
        this.newViewRepo = newViewRepo;
        this.newMenuService = newMenuService;
        this.state = state;

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
    }


    public View View => contentView;
    public int ContentWidth => contentView.ContentWidth;

    public int CurrentIndex => contentView.CurrentIndex;
    public Point CurrentPoint => contentView.CurrentPoint;

    public async Task<R> ShowRepoAsync(string path)
    {
        var branches = state.GetRepo(path).Branches;
        if (!Try(out var e, await ShowNewRepoAsync(path, branches))) return e;

        RegisterShortcuts();
        return R.Ok;
    }


    public void UpdateRepoTo(Repo repo) => ShowRepo(repo);

    public void Refresh() => ShowRefreshedRepoAsync().RunInBackground();


    void RegisterShortcuts()
    {
        contentView.RegisterKeyHandler(Key.m, menuService!.ShowMainMenu);
        contentView.RegisterKeyHandler(Key.M, menuService!.ShowMainMenu);
        contentView.RegisterKeyHandler(Key.CursorRight, menuService!.ShowShowBranchesMenu);
        contentView.RegisterKeyHandler(Key.CursorLeft, menuService!.ShowHideBranchesMenu);
        contentView.RegisterKeyHandler(Key.r, Refresh);
        contentView.RegisterKeyHandler(Key.R, Refresh);
        contentView.RegisterKeyHandler(Key.c, repo!.Commit);
        contentView.RegisterKeyHandler(Key.C, repo!.Commit);
        contentView.RegisterKeyHandler(Key.b, repo!.CreateBranch);
        contentView.RegisterKeyHandler(Key.B, repo!.CreateBranch);
        contentView.RegisterKeyHandler(Key.d, ShowDiff);
        contentView.RegisterKeyHandler(Key.D, ShowDiff);
        contentView.RegisterKeyHandler(Key.D | Key.CtrlMask, ShowDiff);
        contentView.RegisterKeyHandler(Key.p, repo!.PushCurrentBranch);
        contentView.RegisterKeyHandler(Key.P, repo!.PushCurrentBranch);
    }

    private void ShowDiff()
    {
        var diffView = newDiffView(repo!);
        diffView.ShowCurrentRow();
    }


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

    async Task<R> ShowNewRepoAsync(string path, IReadOnlyList<string> showBranches)
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
        var branchNames = repo!.Repo.Branches.Select(b => b.Name).ToList();

        if (!Try(out var viewRepo, out var e,
            await viewRepoService.GetRepoAsync(repo!.Repo.Path, branchNames)))
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

        var names = repo.Branches.Select(b => b.Name).ToList();
        state.SetRepo(repo.Path, s => s.Branches = names);
    }
}
