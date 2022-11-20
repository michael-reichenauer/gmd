using gmd.Common;
using gmd.Cui.Common;
using Terminal.Gui;


namespace gmd.Cui;

interface IRepoView
{
    View View { get; }
    View DetailsView { get; }
    int CurrentIndex { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }

    Task<R> ShowRepoAsync(string path);
    void UpdateRepoTo(Server.Repo repo, string branchName = "");
    void Refresh(string addName = "", string commitId = "");
    void ToggleDetails();
}

class RepoView : IRepoView
{
    static readonly TimeSpan minRepoUpdateInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan minStatusUpdateInterval = TimeSpan.FromMilliseconds(100);

    readonly Server.IServer server;
    readonly Func<IRepoView, Server.Repo, IRepo> newViewRepo;
    private readonly Func<IRepo, IRepoViewMenus> newMenuService;
    private readonly IState state;
    private readonly IProgress progress;
    private readonly ICommitDetailsView commitDetailsView;
    readonly Func<IRepo, IDiffView> newDiffView;
    private readonly Func<View, int, IRepoWriter> newRepoWriter;
    readonly ContentView contentView;
    readonly IRepoWriter repoWriter;

    // State data
    IRepo? repo; // Is set once the repo has been retrieved the first time in ShowRepo().
    IRepoViewMenus? menuService;
    bool isStatusUpdateInProgress = false;
    bool isRepoUpdateInProgress = false;
    bool isShowDetails = false;
    bool isRegistered = false;


    internal RepoView(
        Server.IServer server,
        Func<IRepo, IDiffView> newDiffView,
        Func<View, int, IRepoWriter> newRepoWriter,
        Func<IRepoView, Server.Repo, IRepo> newViewRepo,
        Func<IRepo, IRepoViewMenus> newMenuService,
        IState state,
        IProgress progress,
        ICommitDetailsView commitDetailsView) : base()
    {
        this.server = server;
        this.newDiffView = newDiffView;
        this.newRepoWriter = newRepoWriter;
        this.newViewRepo = newViewRepo;
        this.newMenuService = newMenuService;
        this.state = state;
        this.progress = progress;
        this.commitDetailsView = commitDetailsView;
        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            IsFocus = true,
        };
        contentView.CurrentIndexChange += () => OnCurrentIndexChange();

        repoWriter = newRepoWriter(contentView, contentView.ContentX);

        server.RepoChange += OnRefreshRepo;
        server.StatusChange += OnRefreshStatus;
    }


    public View View => contentView;
    public View DetailsView => commitDetailsView.View;
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


    public void UpdateRepoTo(Server.Repo serverRepo, string branchName = "")
    {
        ShowRepo(serverRepo);

        ScrollToBranch(branchName);
    }


    public void Refresh(string addName = "", string commitId = "") => ShowRefreshedRepoAsync(addName, commitId).RunInBackground();

    public void ToggleDetails()
    {
        isShowDetails = !isShowDetails;

        if (!isShowDetails)
        {
            contentView.Height = Dim.Fill();
            commitDetailsView.View.Height = 0;
            contentView.IsFocus = true;
            commitDetailsView.View.IsFocus = false;
        }
        else
        {
            contentView.Height = Dim.Fill(CommitDetailsView.ContentHeight);
            commitDetailsView.View.Height = CommitDetailsView.ContentHeight;
            OnCurrentIndexChange();
        }

        contentView.SetNeedsDisplay();
        commitDetailsView.View.SetNeedsDisplay();
    }

    void OnRefreshRepo(Server.ChangeEvent e)
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
        ShowRefreshedRepoAsync("", "").RunInBackground();
    }

    void OnRefreshStatus(Server.ChangeEvent e)
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
        if (isRegistered)
        {
            return;
        }
        isRegistered = true;

        // Keys on repo view contents
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
        contentView.RegisterKeyHandler(Key.d, () => repo!.ShowCurrentRowDiff());
        contentView.RegisterKeyHandler(Key.D, () => repo!.ShowCurrentRowDiff());
        contentView.RegisterKeyHandler(Key.D | Key.CtrlMask, () => repo!.ShowCurrentRowDiff());
        contentView.RegisterKeyHandler(Key.p, () => repo!.PushCurrentBranch());
        contentView.RegisterKeyHandler(Key.P, () => repo!.PushCurrentBranch());
        contentView.RegisterKeyHandler(Key.f, () => repo!.Filter());
        contentView.RegisterKeyHandler(Key.Enter, () => ToggleDetails());
        contentView.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocous());

        // Keys on commit details view
        commitDetailsView.View.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocous());
        commitDetailsView.View.RegisterKeyHandler(Key.d, () => repo!.ShowCurrentRowDiff());
    }


    void onDrawRepoContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (repo == null)
        {
            return;
        }

        repoWriter.WriteRepoPage(repo, firstIndex, count);
    }


    async Task<R> ShowNewRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        using (progress.Show())
        {
            isStatusUpdateInProgress = true;
            isRepoUpdateInProgress = true;
            var t = Timing.Start;
            if (!Try(out var viewRepo, out var e,
                await server.GetRepoAsync(path, showBranches)))
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
    }

    async Task ShowRefreshedRepoAsync(string addBranchName, string commitId)
    {
        using (progress.Show())
        {
            Log.Info($"show refreshed repo with {addBranchName} ...");

            isStatusUpdateInProgress = true;
            isRepoUpdateInProgress = true;
            var t = Timing.Start;

            var branchNames = repo!.GetShownBranches().Select(b => b.Name).ToList();
            if (addBranchName != "")
            {
                branchNames.Add(addBranchName);
            }

            if (!Try(out var viewRepo, out var e,
                await server.GetRepoAsync(repo!.Repo.Path, branchNames)))
            {
                isStatusUpdateInProgress = false;
                isRepoUpdateInProgress = false;
                UI.ErrorMessage($"Failed to refresh:\n{e}");
                return;
            }

            isStatusUpdateInProgress = false;
            isRepoUpdateInProgress = false;
            ShowRepo(viewRepo);

            if (commitId != "")
            {
                ScrollToCommit(commitId);
            }
            else if (addBranchName != "")
            {
                ScrollToBranch(addBranchName);
            }

            Log.Info($"{t} {viewRepo}");
        }
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        using (progress.Show())
        {
            isStatusUpdateInProgress = true;
            var t = Timing.Start;
            if (!Try(out var viewRepo, out var e,
                await server.GetUpdateStatusRepoAsync(repo!.Repo)))
            {
                isStatusUpdateInProgress = false;
                UI.ErrorMessage($"Failed to update status:\n{e}");
                return;
            }

            isStatusUpdateInProgress = false;
            ShowRepo(viewRepo);
            Log.Info($"{t} {viewRepo}");
        }
    }


    void ShowRepo(Server.Repo serverRepo)
    {
        repo = newViewRepo(this, serverRepo);
        menuService = newMenuService(repo);
        contentView.TriggerUpdateContent(repo.TotalRows);
        OnCurrentIndexChange();

        // Remember shown branch for next restart of program
        var names = repo.GetShownBranches().Select(b => b.Name).ToList();
        state.SetRepo(serverRepo.Path, s => s.Branches = names);
    }

    void ScrollToBranch(string branchName)
    {
        if (branchName != "")
        {
            var branch = repo!.Repo.Branches.FirstOrDefault(b => b.Name == branchName);
            if (branch != null)
            {
                var tip = repo.Repo.CommitById[branch.TipId];
                contentView.ScrollToShowIndex(tip.Index);
            }
        }
    }

    void ScrollToCommit(string commitId)
    {
        var commit = repo!.Repo.Commits.FirstOrDefault(c => c.Id == commitId);
        if (commit != null)
        {
            contentView.ScrollToShowIndex(commit.Index);
        }
    }



    private void ToggleDetailsFocous()
    {
        if (!isShowDetails)
        {
            return;
        }

        // Shift focus (unfortionatley SetFocus() does not seem to work)
        contentView.IsFocus = !contentView.IsFocus;
        commitDetailsView.View.IsFocus = !commitDetailsView.View.IsFocus;

        commitDetailsView.View.SetNeedsDisplay();
        contentView.SetNeedsDisplay();
    }

    void OnCurrentIndexChange()
    {
        var i = contentView.CurrentIndex;
        if (i >= repo!.Repo.Commits.Count)
        {
            return;
        }

        if (isShowDetails)
        {
            commitDetailsView.Set(repo!.Repo.Commits[i]);
        }
    }
}
