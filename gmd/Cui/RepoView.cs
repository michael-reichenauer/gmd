using gmd.Common;
using gmd.Cui.Common;
using gmd.Git;
using Terminal.Gui;


namespace gmd.Cui;

interface IRepoView
{
    View View { get; }
    View DetailsView { get; }
    int CurrentIndex { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }

    Task<R> ShowInitialRepoAsync(string path);
    Task<R> ShowRepoAsync(string path);
    void UpdateRepoTo(Server.Repo repo, string branchName = "");
    void Refresh(string addName = "", string commitId = "");
    void ToggleDetails();
}

class RepoView : IRepoView
{
    static readonly TimeSpan minRepoUpdateInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan minStatusUpdateInterval = TimeSpan.FromMilliseconds(100);
    static readonly TimeSpan fetchInterval = TimeSpan.FromMinutes(5);
    static readonly int MaxRecentFolders = 10;
    static readonly int MaxRecentParentFolders = 5;

    readonly Server.IServer server;
    readonly Func<IRepoView, Server.Repo, IRepo> newViewRepo;
    readonly Func<IRepo, IRepoViewMenus> newMenuService;
    readonly IStates states;
    readonly IProgress progress;
    readonly IGit git;
    readonly ICommitDetailsView commitDetailsView;
    readonly Func<View, int, IRepoWriter> newRepoWriter;
    readonly ContentView contentView;
    readonly IRepoWriter repoWriter;

    // State data
    IRepo? repo; // Is set once the repo has been retrieved the first time in ShowRepo().
    IRepoCommands Cmd => repo!.Cmd;
    IRepoViewMenus? menuService;
    bool isStatusUpdateInProgress = false;
    bool isRepoUpdateInProgress = false;
    bool isShowDetails = false;
    bool isRegistered = false;


    internal RepoView(
        Server.IServer server,
        Func<View, int, IRepoWriter> newRepoWriter,
        Func<IRepoView, Server.Repo, IRepo> newViewRepo,
        Func<IRepo, IRepoViewMenus> newMenuService,
        IStates states,
        IProgress progress,
        IGit git,
        ICommitDetailsView commitDetailsView) : base()
    {
        this.server = server;
        this.newRepoWriter = newRepoWriter;
        this.newViewRepo = newViewRepo;
        this.newMenuService = newMenuService;
        this.states = states;
        this.progress = progress;
        this.git = git;
        this.commitDetailsView = commitDetailsView;
        contentView = new ContentView(onGetContent)
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

    public async Task<R> ShowInitialRepoAsync(string path)
    {
        if (!Try(out var e, await ShowRepoAsync(path))) return e;
        UI.AddTimeout(fetchInterval, (_) => FetchFromRemote());

        RegisterShortcuts();
        return R.Ok;
    }

    public async Task<R> ShowRepoAsync(string path)
    {
        if (!Try(out var rootDir, out var e, git.RootPath(path))) return e;
        Log.Info($"Show '{rootDir} ({path})'");

        var branches = states.GetRepo(rootDir).Branches;
        if (!Try(out e, await ShowNewRepoAsync(rootDir, branches))) return e;
        FetchFromRemote();

        RememberRepoPaths(rootDir);

        return R.Ok;
    }


    public void UpdateRepoTo(Server.Repo serverRepo, string branchName = "")
    {
        ShowRepo(serverRepo);

        ScrollToBranch(branchName);
    }


    public void Refresh(string addName = "", string commitId = "") =>
        ShowRefreshedRepoAsync(addName, commitId).RunInBackground();

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
        contentView.RegisterKeyHandler(Key.F5, () => Refresh());
        contentView.RegisterKeyHandler(Key.c, () => Cmd.Commit());
        contentView.RegisterKeyHandler(Key.C, () => Cmd.Commit());
        contentView.RegisterKeyHandler(Key.b, () => Cmd.CreateBranch());
        contentView.RegisterKeyHandler(Key.B, () => Cmd.CreateBranch());
        contentView.RegisterKeyHandler(Key.d, () => Cmd.ShowCurrentRowDiff());
        contentView.RegisterKeyHandler(Key.D, () => Cmd.ShowCurrentRowDiff());
        contentView.RegisterKeyHandler(Key.D | Key.CtrlMask, () => Cmd.ShowCurrentRowDiff());
        contentView.RegisterKeyHandler(Key.p, () => Cmd.PushAllBranches());
        contentView.RegisterKeyHandler(Key.P, () => Cmd.PushAllBranches());
        contentView.RegisterKeyHandler(Key.u, () => Cmd.PullAllBranches());
        contentView.RegisterKeyHandler(Key.U, () => Cmd.PullAllBranches());
        contentView.RegisterKeyHandler(Key.a, () => Cmd.ShowAbout());
        contentView.RegisterKeyHandler(Key.h, () => Cmd.ShowHelp());
        contentView.RegisterKeyHandler(Key.F1, () => Cmd.ShowHelp());
        contentView.RegisterKeyHandler(Key.f, () => Cmd.Filter());
        contentView.RegisterKeyHandler(Key.Enter, () => ToggleDetails());
        contentView.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocus());

        contentView.RegisterMouseHandler(MouseFlags.Button1Clicked, (x, y) => Clicked(x, y));
        contentView.RegisterMouseHandler(MouseFlags.Button1DoubleClicked, (x, y) => DoubleClicked(x, y));

        // Keys on commit details view.
        commitDetailsView.View.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocus());
        commitDetailsView.View.RegisterKeyHandler(Key.d, () => Cmd.ShowCurrentRowDiff());
    }

    void DoubleClicked(int x, int y)
    {
        contentView.SetIndex(y);
        ToggleDetails();
    }

    void Clicked(int x, int y)
    {
        contentView.SetIndex(y);
    }

    IEnumerable<Text> onGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (repo == null)
        {
            return Enumerable.Empty<Text>();
        }

        return repoWriter.ToPage(repo, firstIndex, count, currentIndex, width);
    }


    async Task<R> ShowNewRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        using (progress.Show())
        {
            var t = Timing.Start();
            if (!Try(out var viewRepo, out var e, await GetRepoAsync(path, showBranches))) return e;

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

            var t = Timing.Start();

            var branchNames = repo!.Branches.Select(b => b.Name).ToList();
            if (addBranchName != "")
            {
                branchNames.Add(addBranchName);
            }

            if (!Try(out var viewRepo, out var e, await GetRepoAsync(repo!.RepoPath, branchNames)))
            {
                UI.ErrorMessage($"Failed to refresh:\n{e}");
                return;
            }

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

        server.FetchAsync(repo.RepoPath).RunInBackground();
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        using (progress.Show())
        {
            var t = Timing.Start();
            if (!Try(out var viewRepo, out var e, await GetUpdateStatusRepoAsync(repo!.Repo)))
            {
                UI.ErrorMessage($"Failed to update status:\n{e}");
                return;
            }

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
        var names = repo.Branches.Select(b => b.Name).ToList();
        states.SetRepo(serverRepo.Path, s => s.Branches = names);
    }


    void ScrollToBranch(string branchName)
    {
        if (branchName != "")
        {
            var branch = repo!.Branches.FirstOrDefault(b => b.Name == branchName);
            if (branch != null)
            {
                var tip = repo.Commit(branch.TipId);
                contentView.ScrollToShowIndex(tip.Index);
            }
        }
    }


    void ScrollToCommit(string commitId)
    {
        var commit = repo!.Commits.FirstOrDefault(c => c.Id == commitId);
        if (commit != null)
        {
            contentView.ScrollToShowIndex(commit.Index);
        }
    }


    private void ToggleDetailsFocus()
    {
        if (!isShowDetails)
        {
            return;
        }

        // Shift focus (unfortunately SetFocus() does not seem to work)
        contentView.IsFocus = !contentView.IsFocus;
        commitDetailsView.View.IsFocus = !commitDetailsView.View.IsFocus;

        commitDetailsView.View.SetNeedsDisplay();
        contentView.SetNeedsDisplay();
    }


    void OnCurrentIndexChange()
    {
        if (isShowDetails)
        {
            var commit = repo!.RowCommit;
            var branch = repo.Branch(commit.BranchName);
            commitDetailsView.Set(repo.Repo, commit, branch);
        }
    }


    bool FetchFromRemote()
    {
        server.FetchAsync(repo!.RepoPath).RunInBackground();
        return true;
    }


    void RememberRepoPaths(string path)
    {
        // Remember recent repo paths
        states.Set(s => s.RecentFolders = s.RecentFolders
            .Prepend(path).Distinct().Where(Files.DirExists).Take(MaxRecentFolders).ToList());

        // Remember parent folder to paths to be used when browsing
        var parent = Path.GetDirectoryName(path);
        if (parent != null)
        {
            states.Set(s => s.RecentParentFolders = s.RecentParentFolders
               .Prepend(parent).Distinct().Where(Files.DirExists).Take(MaxRecentParentFolders).ToList());
        }
    }


    async Task<R<Server.Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        try
        {
            isStatusUpdateInProgress = true;
            isRepoUpdateInProgress = true;
            return await server.GetRepoAsync(path, showBranches);
        }
        finally
        {
            isStatusUpdateInProgress = false;
            isRepoUpdateInProgress = false;
        }
    }

    async Task<R<Server.Repo>> GetUpdateStatusRepoAsync(Server.Repo repo)
    {
        try
        {
            isStatusUpdateInProgress = true;
            return await server.GetUpdateStatusRepoAsync(repo);
        }
        finally
        {
            isStatusUpdateInProgress = false;
        }
    }
}
