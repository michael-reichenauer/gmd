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

    Task<R> ShowInitialRepoAsync(string path);
    Task<R> ShowRepoAsync(string path);
    void UpdateRepoTo(Repo repo, string branchName = "");
    void UpdateRepoToAtCommit(Repo repo, string commitId);
    void Refresh(string addName = "", string commitId = "");
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

    public async Task<R> ShowInitialRepoAsync(string path)
    {
        if (!Try(out var e, await ShowRepoAsync(path)))
            return e;
        UI.AddTimeout(fetchInterval, (_) => FetchFromRemote());
        updater.StartCheckUpdatesRegularly().RunInBackground();

        input.Register();
        return R.Ok;
    }

    public async Task<R> ShowRepoAsync(string path)
    {
        if (!Try(out var rootDir, out var e, git.RootPath(path)))
            return e;
        Log.Info($"Show repo for '{path}' ({rootDir})");

        var branches = repoConfig.Get(rootDir).Branches;
        if (!Try(out e, await ShowNewRepoAsync(rootDir, branches)))
            return e;
        FetchFromRemote();

        RememberRepoPaths(rootDir);

        return R.Ok;
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

    public void Refresh(string addName = "", string commitId = "") =>
        ShowRefreshedRepoAsync(addName, commitId, false).RunInBackground();

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
        Try(out var commit, out var e, filterDlg.Show(repo.Repo, r => ShowFilteredRepo(r), commitsView));

        // Show Commits view normal again
        isShowFilter = false;
        commitsView.IsFocus = true;
        commitsView.SetFocus();
        commitsView.SetNeedsDisplay();
        Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

        if (commit != null)
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

    async Task<R> ShowNewRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        using (progress.Show())
        {
            var t = Timing.Start();
            if (!Try(out var viewRepo, out var e, await GetRepoAsync(path, showBranches)))
                return e;

            ShowRepo(viewRepo);
            Log.Info($"Showed {t} {viewRepo}");
            return R.Ok;
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

            if (!Try(out var viewRepo, out var e, await GetRepoAsync(repo.Repo.Path, branchNames)))
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

            Log.Info($"Showed {t} {viewRepo}");
            if (isAwaitFetch)
            {
                await server.FetchAsync(repo.Repo.Path);
            }
        }

        if (!isAwaitFetch)
        {
            server.FetchAsync(repo.Repo.Path).RunInBackground();
        }
    }

    async Task ShowUpdatedStatusRepoAsync()
    {
        using (progress.Show())
        {
            var t = Timing.Start();
            if (!Try(out var viewRepo, out var e, await GetUpdateStatusRepoAsync(repo.Repo)))
            {
                UI.ErrorMessage($"Failed to update status:\n{e}");
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
        server.FetchAsync(repo.Repo.Path).RunInBackground();
        return true;
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

    async Task<R<Server.Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches)
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

    async Task<R<Server.Repo>> GetUpdateStatusRepoAsync(Server.Repo repo)
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
