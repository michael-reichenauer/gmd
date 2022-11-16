using gmd.Common;
using gmd.Server;
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
    void Refresh(string addName = "");
}

class RepoView : IRepoView
{
    static readonly TimeSpan minRepoUpdateInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan minStatusUpdateInterval = TimeSpan.FromMilliseconds(100);
    readonly IServer viewRepoService;
    readonly Func<IRepoView, Repo, IRepo> newViewRepo;
    private readonly Func<IRepo, IRepoViewMenus> newMenuService;
    private readonly IState state;
    private readonly IProgress progress;
    readonly Func<IRepo, IDiffView> newDiffView;
    private readonly Func<View, int, IRepoWriter> newRepoWriter;
    readonly ContentView contentView;
    readonly IRepoWriter repoWriter;

    // State data
    IRepo? repo; // Is set once the repo has been retrieved the first time in ShowRepo().
    IRepoViewMenus? menuService;
    bool isStatusUpdateInProgress = false;
    bool isRepoUpdateInProgress = false;


    internal RepoView(
        IServer viewRepoService,
        Func<IRepo, IDiffView> newDiffView,
        Func<View, int, IRepoWriter> newRepoWriter,
        Func<IRepoView, Repo, IRepo> newViewRepo,
        Func<IRepo, IRepoViewMenus> newMenuService,
        IState state,
        IProgress progress) : base()
    {
        this.viewRepoService = viewRepoService;
        this.newDiffView = newDiffView;
        this.newRepoWriter = newRepoWriter;
        this.newViewRepo = newViewRepo;
        this.newMenuService = newMenuService;
        this.state = state;
        this.progress = progress;
        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        repoWriter = newRepoWriter(contentView, contentView.ContentX);

        viewRepoService.RepoChange += OnRefreshRepo;
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

    public void Refresh(string addName = "") => ShowRefreshedRepoAsync(addName).RunInBackground();

    void OnRefreshRepo(ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (isRepoUpdateInProgress)
        {
            return;
        }

        Log.Debug($"Current: {repo!.Repo.TimeStamp.Iso()}");
        Log.Debug($"New    : {e.TimeStamp.Iso()}");

        if (e.TimeStamp - repo!.Repo.TimeStamp < minRepoUpdateInterval)
        {
            Log.Debug("New repo event to soon, skipping update");
            return;
        }
        ShowRefreshedRepoAsync("").RunInBackground();
    }

    void OnRefreshStatus(ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (isStatusUpdateInProgress || isRepoUpdateInProgress)
        {
            return;
        }
        Log.Debug($"Current: {repo!.Repo.TimeStamp.Iso()}");
        Log.Debug($"New    : {e.TimeStamp.Iso()}");

        if (e.TimeStamp - repo!.Repo.TimeStamp < minStatusUpdateInterval)
        {
            Log.Debug("New status event to soon, skipping update");
            return;
        }
        ShowUpdatedStatusRepoAsync().RunInBackground();
    }


    void RegisterShortcuts()
    {
        contentView.RegisterKeyHandler(Key.C | Key.CtrlMask, UI.Shutdown);
        contentView.RegisterKeyHandler(Key.m, () => menuService!.ShowMainMenu());
        contentView.RegisterKeyHandler(Key.M, () => menuService!.ShowMainMenu());
        contentView.RegisterKeyHandler(Key.CursorRight, () => menuService!.ShowShowBranchesMenu());
        contentView.RegisterKeyHandler(Key.CursorLeft, () => menuService!.ShowHideBranchesMenu());
        contentView.RegisterKeyHandler(Key.r, () => Refresh());
        contentView.RegisterKeyHandler(Key.R, () => Refresh());
        contentView.RegisterKeyHandler(Key.c, () => repo!.Commit());
        contentView.RegisterKeyHandler(Key.C, () => repo!.Commit());
        contentView.RegisterKeyHandler(Key.b, () => repo!.CreateBranch());
        contentView.RegisterKeyHandler(Key.B, () => repo!.CreateBranch());
        contentView.RegisterKeyHandler(Key.d, ShowDiff);
        contentView.RegisterKeyHandler(Key.D, ShowDiff);
        contentView.RegisterKeyHandler(Key.D | Key.CtrlMask, ShowDiff);
        contentView.RegisterKeyHandler(Key.p, () => repo!.PushCurrentBranch());
        contentView.RegisterKeyHandler(Key.P, () => repo!.PushCurrentBranch());
    }

    private void ShowDiff()
    {
        var diffView = newDiffView(repo!);
        diffView.ShowCurrentRow();
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
        isStatusUpdateInProgress = true;
        isRepoUpdateInProgress = true;
        var t = Timing.Start;
        if (!Try(out var viewRepo, out var e,
            await viewRepoService.GetRepoAsync(path, showBranches)))
        {
            isStatusUpdateInProgress = true;
            isRepoUpdateInProgress = true;
            return e;
        }

        isStatusUpdateInProgress = true;
        isRepoUpdateInProgress = true;
        ShowRepo(viewRepo);
        Log.Info($"{t} {viewRepo}");
        return R.Ok;
    }

    async Task ShowRefreshedRepoAsync(string addName)
    {
        Log.Info("show refreshed repo ...");
        isStatusUpdateInProgress = true;
        isRepoUpdateInProgress = true;
        var t = Timing.Start;

        var branchNames = repo!.Repo.Branches.Select(b => b.Name).ToList();
        if (addName != "")
        {
            branchNames.Add(addName);
        }

        if (!Try(out var viewRepo, out var e,
            await viewRepoService.GetRepoAsync(repo!.Repo.Path, branchNames)))
        {
            isStatusUpdateInProgress = false;
            isRepoUpdateInProgress = false;
            UI.ErrorMessage($"Failed to refresh:\n{e}");
            return;
        }

        isStatusUpdateInProgress = false;
        isRepoUpdateInProgress = false;
        ShowRepo(viewRepo);
        Log.Info($"{t} {viewRepo}");
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        isStatusUpdateInProgress = true;
        var t = Timing.Start;
        if (!Try(out var viewRepo, out var e,
            await viewRepoService.GetUpdateStatusRepoAsync(repo!.Repo)))
        {
            isStatusUpdateInProgress = false;
            UI.ErrorMessage($"Failed to update status:\n{e}");
            return;
        }

        isStatusUpdateInProgress = false;
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
