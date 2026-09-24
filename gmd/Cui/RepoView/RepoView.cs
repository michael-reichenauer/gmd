using gmd.Common;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Installation;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.RepoView;

interface IRepoView
{
    View View { get; }
    View DetailsView { get; }
    View ApplicationBarView { get; }
    int CurrentIndex { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    Selection Selection { get; }

    // The shown repo, which is an immutable snapshot replaced on every refresh, so a command that
    // refreshes and then needs the repo (or its command classes) has to read this again after.
    IViewRepo ViewRepo { get; }

    Task<Result> ShowInitialRepoAsync(string path);
    Task<Result> ShowRepoAsync(string path);
    void UpdateRepoTo(Repo repo, string branchName = "");
    void UpdateRepoToAtCommit(Repo repo, string commitId);
    void Refresh(string addName = "", string commitId = "");
    Task RefreshAsync(string addName = "", string commitId = "");
    void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Server.Commit>? commits = null);
    void RefreshAndFetch(string addName = "", string commitId = "");
    void ToggleDetails();
    void ShowFilter();
    void ClearSelection();
}

// The main log view: the list of commits with the branch graph, the commit details below it and
// the application bar above it. It owns the shown repo, i.e. reading it, refreshing it when git or
// the working folder changes, and drawing the page the user is looking at.
//
// What the user does to it is in RepoViewInput (the keys and mouse buttons) and in the command
// classes it dispatches to; where the hoovered branch is, is in Hoover.
class RepoView : IRepoView, IRepoViewInputHost
{
    static readonly TimeSpan minRepoUpdateInterval = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan minStatusUpdateInterval = TimeSpan.FromMilliseconds(100);
    static readonly TimeSpan fetchInterval = TimeSpan.FromMinutes(5);

    // How often the other worktrees' changes are re-read. Their folders are not watched — a
    // worktree nested in this one is even excluded from the watcher, so a build there cannot storm
    // this gmd — so this is what turns the top bar marker yellow while someone edits in one. They
    // are also read once right after a repo is shown: the repo is read without them, so that a
    // status run in a large worktree never delays showing it, and this fills them in moments later.
    static readonly TimeSpan worktreeStatusInterval = TimeSpan.FromSeconds(30);

    readonly IServer server;
    readonly Func<IRepoView, Repo, IViewRepo> newViewRepo;
    readonly Func<IViewRepo, IRepoViewMenus> newMenuService;
    readonly Config config;
    readonly IUpdater updater;
    readonly IRepoConfig repoConfig;
    readonly IProgress progress;
    readonly IGit git;
    readonly ICommitDetailsView commitDetailsView;
    readonly IApplicationBar applicationBarView;
    readonly IFilterDlg filterDlg;
    readonly ContentView commitsView;
    readonly IRepoWriter repoWriter;
    readonly Hoover hoover = new Hoover();
    readonly RepoViewInput input;

    // State data
    IViewRepo repo; // Is set once the repo has been retrieved the first time in ShowRepo().
    ICommitCommands CommitCmds => repo.CommitCmds;
    IRepoViewMenus menuService = null!;
    bool isStatusUpdateInProgress = false;
    bool isRepoUpdateInProgress = false;
    bool isShowDetails = false;
    bool isShowFilter;

    internal RepoView(
        IServer server,
        Func<View, int, IRepoWriter> newRepoWriter,
        Func<IRepoView, Repo, IViewRepo> newViewRepo,
        Func<IViewRepo, IRepoViewMenus> newMenuService,
        Config config,
        IUpdater updater,
        IRepoConfig repoConfig,
        IProgress progress,
        IGit git,
        ICommitDetailsView commitDetailsView,
        IApplicationBar applicationBarView,
        IFilterDlg filterDlg,
        IUnicodeSetsDlg charDlg,
        IClipboardService clipboard
    )
        : base()
    {
        this.server = server;
        this.newViewRepo = newViewRepo;
        this.newMenuService = newMenuService;
        this.config = config;
        this.updater = updater;
        this.repoConfig = repoConfig;
        this.progress = progress;
        this.git = git;
        this.commitDetailsView = commitDetailsView;
        this.applicationBarView = applicationBarView;
        this.filterDlg = filterDlg;
        commitsView = new ContentView(OnGetContent)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            IsFocus = true,
            IsShowCursor = false,
            IsCursorMargin = false,
            IsScrollMode = false,
            IsHighlightCurrentIndex = false,
            IsCustomShowSelection = true,
        };
        commitsView.CurrentIndexChange += () => OnCurrentIndexChange();

        repoWriter = newRepoWriter(commitsView, commitsView.ContentX);
        repo = newViewRepo(this, Server.Repo.Empty);
        input = new RepoViewInput(this, commitsView, commitDetailsView, applicationBarView, charDlg, clipboard, hoover);

        server.RepoChange += OnRefreshRepo;
        server.StatusChange += OnRefreshStatus;
    }

    public View View => commitsView;
    public View DetailsView => commitDetailsView.View;
    public View ApplicationBarView => applicationBarView.View;

    public int ContentWidth => commitsView.ContentWidth;

    public int CurrentIndex => commitsView.CurrentIndex;
    public Point CurrentPoint => commitsView.CurrentPoint;

    public Selection Selection => commitsView.Selection;

    // What RepoViewInput needs from the view, see IRepoViewInputHost. Both are replaced every time
    // a repo is shown.
    public IViewRepo ViewRepo => repo;
    public IRepoViewMenus Menus => menuService;

    public void ClearSelection() => commitsView.ClearSelection();

    public async Task<Result> ShowInitialRepoAsync(string path)
    {
        if (await ShowRepoAsync(path) is Error e)
            return e;
        UI.AddTimeout(fetchInterval, (_) => FetchFromRemote());
        UI.AddTimeout(
            worktreeStatusInterval,
            (_) =>
            {
                UpdateWorktreesStatus();
                return true;
            }
        );
        updater.StartCheckUpdatesRegularly().RunInBackground();

        input.Register();
        return Result.Ok;
    }

    public async Task<Result> ShowRepoAsync(string path)
    {
        var rootDirResult = git.RootPath(path);
        if (rootDirResult is not string rootDir)
            return rootDirResult.Error;
        Log.Info($"Show repo for '{path}' ({rootDir})");

        var branches = repoConfig.Get(rootDir).Branches;
        if (await ShowNewRepoAsync(rootDir, branches) is Error e)
            return e;
        FetchFromRemote();

        RememberRepoPaths(rootDir);

        return Result.Ok;
    }

    public void UpdateRepoTo(Repo serverRepo, string branchName = "")
    {
        var t = Timing.Start();
        ShowRepo(serverRepo);

        ScrollToBranch(branchName);
        Log.Info($"Showed {t} {serverRepo} with '{branchName}'");
    }

    public void UpdateRepoToAtCommit(Server.Repo repo, string commitId)
    {
        var t = Timing.Start();
        ShowRepo(repo);

        ScrollToCommit(commitId);
        Log.Info($"Showed {t} {repo} with '{commitId}'");
    }

    public void Refresh(string addName = "", string commitId = "") => RefreshAsync(addName, commitId).RunInBackground();

    public Task RefreshAsync(string addName = "", string commitId = "") =>
        ShowRefreshedRepoAsync(addName, commitId, false);

    public void RefreshAndCommit(
        string addName = "",
        string commitId = "",
        IReadOnlyList<Server.Commit>? commits = null
    )
    {
        UI.Post(async () =>
        {
            await ShowRefreshedRepoAsync(addName, commitId, false);
            CommitCmds.Commit(false, commits);
        });
    }

    public void RefreshAndFetch(string addName = "", string commitId = "") =>
        ShowRefreshedRepoAsync(addName, commitId, true).RunInBackground();

    public void ShowFilter()
    {
        isShowFilter = true;
        // Make room for filter dialog
        commitsView.IsFocus = false;
        commitsView.SetNeedsDisplay();

        var orgRepo = repo.Repo;
        var orgCommit = repo.RowCommit;
        var selected = filterDlg.Show(repo.Repo, r => ShowFilteredRepo(r), commitsView);

        // Show Commits view normal again
        isShowFilter = false;
        commitsView.IsFocus = true;
        commitsView.SetFocus();
        commitsView.SetNeedsDisplay();
        Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

        if (selected is Server.Commit commit)
        { // User selected a commit, show it
            ShowRepo(orgRepo);
            Refresh(commit.BranchName, commit.Id);
        }
        else
        {
            var t = Timing.Start();
            ShowRepo(orgRepo);
            ScrollToCommit(orgCommit.Id);
            Log.Info($"Showed {t} {orgRepo}");
        }
    }

    void ShowFilteredRepo(Server.Repo serverRepo)
    {
        var t = Timing.Start();
        ShowRepo(serverRepo);
        Log.Info($"Showed {t} {serverRepo}");
    }

    public void ToggleDetails()
    {
        isShowDetails = !isShowDetails;

        if (isShowDetails)
        {
            commitsView.Height = Dim.Fill(CommitDetailsView.ContentHeight);
            commitDetailsView.View.Height = CommitDetailsView.ContentHeight;
            OnCurrentIndexChange();
        }
        else
        {
            commitsView.Height = Dim.Fill();
            commitDetailsView.View.Height = 0;
            commitsView.IsFocus = true;
            commitDetailsView.View.IsFocus = false;
        }

        commitsView.SetNeedsDisplay();
        commitDetailsView.View.SetNeedsDisplay();
    }

    public void ToggleDetailsFocus()
    {
        if (!isShowDetails)
            return;

        // Shift focus (unfortunately SetFocus() does not seem to work)
        commitsView.IsFocus = !commitsView.IsFocus;
        commitDetailsView.View.IsFocus = !commitDetailsView.View.IsFocus;

        commitDetailsView.View.SetNeedsDisplay();
        commitsView.SetNeedsDisplay();
    }

    void OnRefreshRepo(Server.ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (isRepoUpdateInProgress)
            return;
        if (e.TimeStamp - repo.Repo.RepoTimeStamp < minRepoUpdateInterval)
            return;

        ShowRefreshedRepoAsync("", "").RunInBackground();
    }

    void OnRefreshStatus(Server.ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (isStatusUpdateInProgress || isRepoUpdateInProgress)
            return;
        if (e.TimeStamp - repo.Repo.RepoTimeStamp < minStatusUpdateInterval)
            return;

        ShowUpdatedStatusRepoAsync().RunInBackground();
    }

    // The timer tick, and the read after a repo is shown: only while there are other worktrees,
    // and never over an update in progress
    void UpdateWorktreesStatus()
    {
        UI.AssertOnUIThread();
        if (isStatusUpdateInProgress || isRepoUpdateInProgress)
            return;
        if (!repo.Repo.OtherWorktrees().Any())
            return;

        ShowUpdatedWorktreesRepoAsync().RunInBackground();
    }

    // No progress shown, nothing is waited for; and the repo is only replaced when a worktree
    // changed, so an idle gmd is not redrawn every tick
    async Task ShowUpdatedWorktreesRepoAsync()
    {
        var shown = repo.Repo;
        var viewRepoResult = await server.GetUpdatedWorktreesRepoAsync(shown);
        if (viewRepoResult is not Server.Repo viewRepo)
        {
            Log.Warn($"Failed to update worktrees, {viewRepoResult.Error}");
            return;
        }
        if (repo.Repo != shown || viewRepo.Worktrees.SequenceEqual(shown.Worktrees))
            return;

        ShowRepo(viewRepo);
        Log.Info($"Showed updated worktrees {viewRepo}");
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        // The current row can have moved without the hoover following it, e.g. when a command
        // scrolled the view, so let the hoover follow the new row or be given up.
        if (hoover.FollowCurrentIndex(currentIndex, repo.Graph.GetRowBranches(repo.CurrentIndex)))
        {
            commitsView.SetNeedsDisplay();
        }

        var page = repoWriter.ToPage(
            repo,
            firstIndex,
            count,
            currentIndex,
            hoover.BranchPrimaryName,
            hoover.RowIndex,
            width,
            Selection
        );
        return (page, repo.Repo.ViewCommits.Count);
    }

    async Task<Result> ShowNewRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        using (progress.Show())
        {
            var t = Timing.Start();
            var viewRepoResult = await GetRepoAsync(path, showBranches);
            if (viewRepoResult is not Server.Repo viewRepo)
                return viewRepoResult.Error;

            ShowRepo(viewRepo);
            Log.Info($"Showed {t} {viewRepo}");
            UpdateWorktreesStatus();
            return Result.Ok;
        }
    }

    async Task ShowRefreshedRepoAsync(string addBranchName, string commitId, bool isAwaitFetch = false)
    {
        using (progress.Show(isAwaitFetch))
        {
            Log.Info($"show refreshed repo with {addBranchName} ...");

            var t = Timing.Start();

            var branchNames = repo!.Repo.ViewBranches.Select(b => b.Name).ToList();
            if (addBranchName != "")
            {
                branchNames.Add(addBranchName);
            }

            var viewRepoResult = await GetRepoAsync(repo.Repo.Path, branchNames);
            if (viewRepoResult is not Server.Repo viewRepo)
            {
                UI.ErrorMessage($"Failed to refresh:\n{viewRepoResult.Error}");
                return;
            }

            ShowRepo(viewRepo);
            UpdateWorktreesStatus();

            if (commitId != "")
            {
                ScrollToCommit(commitId);
            }
            else if (addBranchName != "")
            {
                ScrollToBranch(addBranchName);
            }

            Log.Info($"Showed {t} {viewRepo}");
            if (isAwaitFetch)
            {
                await FetchBestEffortAsync();
            }
        }

        if (!isAwaitFetch)
        {
            FetchBestEffortAsync().RunInBackground();
        }
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        using (progress.Show())
        {
            var t = Timing.Start();
            var viewRepoResult = await GetUpdateStatusRepoAsync(repo.Repo);
            if (viewRepoResult is not Server.Repo viewRepo)
            {
                UI.ErrorMessage($"Failed to update status:\n{viewRepoResult.Error}");
                return;
            }

            ShowRepo(viewRepo);
            Log.Info($"Showed {t} {viewRepo}");
        }
    }

    void ShowRepo(Server.Repo serverRepo)
    {
        repo = newViewRepo(this, serverRepo);
        menuService = newMenuService(repo);

        Console.Title = $"{Path.GetFileName(serverRepo.Path).TrimSuffix(".git")} - gmd";
        applicationBarView.SetRepo(serverRepo);

        commitsView.SetNeedsDisplay();
        OnCurrentIndexChange();

        // Remember shown branch for next restart of program
        if (serverRepo.Filter != "")
            return;

        var names = repo.Repo.ViewBranches.Select(b => b.PrimaryBaseName).Distinct().Take(30).ToList();
        repoConfig.Set(serverRepo.Path, s => s.Branches = names);
    }

    void ScrollToBranch(string branchName)
    {
        if (branchName != "")
        {
            var branch = repo.Repo.ViewBranches.FirstOrDefault(b => b.Name == branchName);
            if (branch != null)
            {
                var tip = repo.Repo.CommitById[branch.TipId];
                commitsView.ScrollToShowIndex(tip.ViewIndex);
                commitsView.SetCurrentIndex(tip.ViewIndex);
            }
        }
    }

    void ScrollToCommit(string commitId)
    {
        var commit = repo.Repo.ViewCommits.FirstOrDefault(c => c.Id == commitId);
        if (commit != null)
        {
            commitsView.ScrollToShowIndex(commit.ViewIndex);
            commitsView.SetCurrentIndex(commit.ViewIndex);
        }
    }

    void OnCurrentIndexChange()
    {
        if (repo.CurrentIndex < 0)
            return;

        var commit = repo.RowCommit;
        var branch = repo.Graph.BranchByName(commit.BranchName);
        applicationBarView.SetBranch(branch);

        if (isShowDetails)
        {
            commitDetailsView.Set(repo.Repo, commit, branch.B);
        }
    }

    bool FetchFromRemote()
    {
        FetchBestEffortAsync().RunInBackground();
        return true;
    }

    // The fetch runs after every refresh and on a timer, and fails whenever the machine is offline
    // or the repo has no 'origin', so a failure is expected and noted at Debug, not warned about
    async Task FetchBestEffortAsync()
    {
        if (await server.FetchAsync(repo.Repo.Path) is Error e)
            Log.Debug($"Fetch failed: {e.AllMessages()}");
    }

    void RememberRepoPaths(string path)
    {
        // Remember recent repo paths
        config.Set(s =>
            s.RecentFolders = s
                .RecentFolders.Prepend(path)
                .Distinct()
                .Where(Directory.Exists)
                .Take(Config.MaxRecentFolders)
                .ToList()
        );
    }

    async Task<Result<Server.Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        if (isShowFilter)
            return repo.Repo;

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

    async Task<Result<Server.Repo>> GetUpdateStatusRepoAsync(Server.Repo repo)
    {
        if (isShowFilter)
            return repo!;

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
