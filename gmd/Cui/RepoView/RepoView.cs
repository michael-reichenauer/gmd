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
    View KeyHintView { get; }
    int CurrentIndex { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    Selection Selection { get; }

    // The shown repo, which is an immutable snapshot replaced on every refresh, so a command that
    // refreshes and then needs the repo (or its command classes) has to read this again after.
    IViewRepo ViewRepo { get; }

    // The branches shown before each show and hide, for Backspace to go back to. Kept here since the
    // view repo and its command classes are replaced on every refresh, and this has to outlive them.
    ShownHistory ShownHistory { get; }

    // What the last search found, for n and Shift-N, kept here for the same reason
    SearchMatches SearchMatches { get; }

    // The hidden remote branches with commits not yet seen, see HiddenNews
    IReadOnlyList<HiddenBranchNews> HiddenNews { get; }

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

    // Sizes the log, the details pane and the key-hint line to what is shown, e.g. after the key
    // hints were turned on or off in the config
    void UpdateLayout();
}

// The main log view: the list of commits with the branch graph, the commit details below it, the
// application bar above it and the key-hint line at the bottom. It owns the shown repo, i.e. reading it, refreshing it when git or
// the working folder changes, and drawing the page the user is looking at.
//
// What the user does to it is in RepoViewInput (the keys and mouse buttons) and in the command
// classes it dispatches to; where the hoovered branch is, is in Hoover.
class RepoView : IRepoView, IRepoViewInputHost
{
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
    readonly KeyHintBar keyHintBar;
    readonly IStatusLine status;
    readonly IRepoWriter repoWriter;
    readonly Hoover hoover = new Hoover();
    readonly ShownHistory shownHistory = new();
    readonly SearchMatches searchMatches = new();
    IReadOnlyList<HiddenBranchNews> hiddenNews = [];
    readonly RepoViewInput input;

    // State data
    IViewRepo repo; // Is set once the repo has been retrieved the first time in ShowRepo().
    ICommitCommands CommitCmds => repo.CommitCmds;
    IRepoViewMenus menuService = null!;
    bool isStatusUpdateInProgress = false;
    bool isRepoUpdateInProgress = false;

    // A change that came while the repo was being read, or while the search was up, which is looked
    // at again once a repo is shown, see OnRefreshRepo
    Server.ChangeEvent? pendingRepoChange;
    Server.ChangeEvent? pendingStatusChange;
    bool isShowDetails = false;
    bool isShowFilter;
    bool isFetchFailing = false;

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
        IClipboardService clipboard,
        IStatusLine status
    )
        : base()
    {
        this.status = status;
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
        keyHintBar = new KeyHintBar(GetKeyHints, () => status.Current);
        status.Changed += OnStatusChanged;

        repoWriter = newRepoWriter(commitsView, commitsView.ContentX);
        repo = newViewRepo(this, Server.Repo.Empty);
        input = new RepoViewInput(
            this,
            commitsView,
            commitDetailsView,
            applicationBarView,
            charDlg,
            clipboard,
            hoover,
            status
        );

        server.RepoChange += OnRefreshRepo;
        server.StatusChange += OnRefreshStatus;
    }

    public View View => commitsView;
    public View DetailsView => commitDetailsView.View;
    public View ApplicationBarView => applicationBarView.View;
    public View KeyHintView => keyHintBar;

    public int ContentWidth => commitsView.ContentWidth;

    public int CurrentIndex => commitsView.CurrentIndex;
    public Point CurrentPoint => commitsView.CurrentPoint;

    public Selection Selection => commitsView.Selection;

    // What RepoViewInput needs from the view, see IRepoViewInputHost. Both are replaced every time
    // a repo is shown.
    public IViewRepo ViewRepo => repo;
    public ShownHistory ShownHistory => shownHistory;
    public SearchMatches SearchMatches => searchMatches;
    public IReadOnlyList<HiddenBranchNews> HiddenNews => hiddenNews;
    public IRepoViewMenus Menus => menuService;

    public void ClearSelection() => commitsView.ClearSelection();

    public async Task<Result> ShowInitialRepoAsync(string path)
    {
        // Here rather than in the constructor, which runs before the config is read
        UpdateLayout();
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
        shownHistory.Clear(); // What another repo showed is nothing to go back to here
        searchMatches.Clear();
        hoover.Clear(); // A branch of the same name in another repo is another branch
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

            // After what may have left changes to commit, e.g. a merge or a cherry pick, for the user
            // to commit. What committed them itself, a squash, leaves nothing, and 'Nothing to
            // commit' would then be said about a commit nobody asked for.
            if (!repo.Repo.Status.IsOk)
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

        if (selected is SearchPick pick)
        { // A commit was picked. It is shown with its branch as any branch is shown, so that
            // Backspace undoes that, and what the search found is kept for n and Shift-N.
            ShowRepo(orgRepo);
            if (pick.MatchIds.Count > 0)
                searchMatches.Set(pick.Filter, pick.MatchIds, pick.Commit.Id);
            else
                searchMatches.Clear();
            repo.BranchCmds.ShowBranch(pick.Commit.BranchName, pick.Commit.Id);
        }
        else
        { // A search given up on is not one to go on with
            searchMatches.Clear();
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
        UpdateLayout();

        if (isShowDetails)
        {
            OnCurrentIndexChange();
        }
        else
        {
            commitsView.IsFocus = true;
            commitDetailsView.View.IsFocus = false;
        }
    }

    // From the bottom up: the key-hint line when it is turned on, the details pane when it is
    // shown, and the log filling the rest below the application bar
    public void UpdateLayout()
    {
        var hintsHeight = config.ShowKeyHints ? 1 : 0;
        var detailsHeight = isShowDetails ? CommitDetailsView.ContentHeight : 0;

        keyHintBar.Visible = IsKeyHintBarShown;
        commitsView.Height = Dim.Fill(detailsHeight + hintsHeight);
        commitDetailsView.View.Y = Pos.AnchorEnd(CommitDetailsView.ContentHeight + hintsHeight);
        commitDetailsView.View.Height = detailsHeight;

        commitsView.SetNeedsDisplay();
        commitDetailsView.View.SetNeedsDisplay();
        keyHintBar.SetNeedsDisplay();
    }

    // A status message is shown on the key-hint line, and over the last row of the log when the key
    // hints are turned off, which is the one time the line is shown for it alone. The hints are only
    // for a repo that is shown: until one is, e.g. after one failed to open, the start menu is all
    // there is, and no key hinted would work there.
    bool IsKeyHintBarShown => status.Current != null || (config.ShowKeyHints && repo.Repo.Path != "");

    // The line is shown and drawn for a message, and put back when the message has been shown long
    // enough, which nothing else would redraw it for
    void OnStatusChanged()
    {
        UI.Post(() =>
        {
            UpdateStatusLine();
            UI.AddTimeout(
                StatusLine.Duration + TimeSpan.FromMilliseconds(100),
                _ =>
                {
                    UpdateStatusLine();
                    return false;
                }
            );
        });
    }

    void UpdateStatusLine()
    {
        keyHintBar.Visible = IsKeyHintBarShown;
        keyHintBar.SetNeedsDisplay();
        commitsView.SetNeedsDisplay(); // The row under the line, when it was shown over the log
    }

    IReadOnlyList<KeyHint> GetKeyHints()
    {
        if (isShowFilter)
            return KeyHints.ForFilter();
        if (repo.CurrentIndex < 0 || repo.CurrentIndex >= repo.Repo.ViewCommits.Count)
            return []; // No repo shown yet

        return KeyHints.For(repo, hoover, Selection, isShowDetails);
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

    // A change is shown by reading the repo again, unless the repo shown was read after the change
    // was told of, see ChangeEvent.IsSeenBy. This used to take every change told of within half a
    // second of a read as seen, which lost a change made in that time, e.g. by a fetch in another
    // terminal, until something else changed. And a change told of while a read was running, or while
    // the search was up, was dropped outright; it is kept now, and looked at again once a repo is
    // shown (ShowChangesMadeMeanwhile).
    void OnRefreshRepo(Server.ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (e.IsSeenBy(repo.Repo))
            return;
        if (isRepoUpdateInProgress || isShowFilter)
        {
            pendingRepoChange = e;
            return;
        }

        ShowRefreshedRepoAsync("", "").RunInBackground();
    }

    // The same for a file in the working folder, which only the status is read again for. A status
    // read does not move RepoTimeStamp, so a change one already saw can cost one more, which is cheap.
    void OnRefreshStatus(Server.ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (e.IsSeenBy(repo.Repo))
            return;
        if (isStatusUpdateInProgress || isRepoUpdateInProgress || isShowFilter)
        {
            pendingStatusChange = e;
            return;
        }

        ShowUpdatedStatusRepoAsync().RunInBackground();
    }

    // Looks again at a change that came while the repo was being read or the search was up, now
    // that a repo is shown: the read may have seen it, or not. Posted, so that it runs after what is
    // showing the repo has finished.
    void ShowChangesMadeMeanwhile()
    {
        if (pendingRepoChange == null && pendingStatusChange == null)
            return;

        UI.Post(() =>
        {
            var (repoChange, statusChange) = (pendingRepoChange, pendingStatusChange);
            (pendingRepoChange, pendingStatusChange) = (null, null);

            // Reading the repo reads the status too, so the status is only for when it is not read
            if (repoChange != null && !repoChange.IsSeenBy(repo.Repo))
                OnRefreshRepo(repoChange);
            else if (statusChange != null)
                OnRefreshStatus(statusChange);
        });
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

        // Whatever redraws the log may have changed what the keys do, so the hints follow it
        keyHintBar.SetNeedsDisplay();
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
                await FetchBestEffortAsync(isAsked: true);
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
        hoover.FollowRepo(serverRepo.BranchByName); // Redrawn below
        keyHintBar.Visible = IsKeyHintBarShown; // Once there is a repo, the hints are for it

        Console.Title = $"{Path.GetFileName(serverRepo.Path).TrimSuffix(".git")} - gmd";

        // Not while a search shows its results, which are not what the user has seen of the branches
        if (serverRepo.Filter == "")
            UpdateHiddenNews(serverRepo);
        applicationBarView.SetRepo(serverRepo, hiddenNews.Sum(n => n.Count));

        commitsView.SetNeedsDisplay();
        OnCurrentIndexChange();

        // Remember shown branch for next restart of program
        if (serverRepo.Filter != "")
            return;

        ShowChangesMadeMeanwhile();

        var names = repo.Repo.ViewBranches.Select(b => b.PrimaryBaseName).Distinct().Take(30).ToList();
        repoConfig.Set(serverRepo.Path, s => s.Branches = names);
    }

    // The shown branches are seen, and what is new on the hidden ones is counted against what was
    void UpdateHiddenNews(Server.Repo serverRepo)
    {
        var seenTips = repoConfig.Get(serverRepo.Path).SeenTips;
        var seen = Cui.RepoView.HiddenNews.Seen(serverRepo, seenTips);
        if (!Cui.RepoView.HiddenNews.IsSame(seen, seenTips))
            repoConfig.Set(serverRepo.Path, s => s.SeenTips = seen);

        hiddenNews = Cui.RepoView.HiddenNews.Of(serverRepo, seen);
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
    // or the repo has no 'origin', so a failure is expected and noted at Debug, not warned about.
    // It is said on the status line, though, since what the log shows of the remote has then gone
    // stale: always when the fetch was asked for (r, F5), and otherwise once, when fetching starts
    // to fail, rather than after every refresh and every five minutes. A repo with no remote
    // branches has nothing to fetch, and is not told so.
    async Task FetchBestEffortAsync(bool isAsked = false)
    {
        if (await server.FetchAsync(repo.Repo.Path) is not Error e)
        {
            isFetchFailing = false;
            return;
        }

        Log.Debug($"Fetch failed: {e.AllMessages()}");
        var isFirstFailure = !isFetchFailing;
        isFetchFailing = true;

        if ((isAsked || isFirstFailure) && repo.Repo.AllBranches.Any(b => b.IsRemote))
            status.Failure($"Fetch failed: {Reason(e)}");
    }

    // What git said, e.g. "Could not resolve host", rather than the whole of the error, which ends
    // with the command line: the first line git prefixed with 'fatal:' or 'error:', or the first
    static string Reason(Error e)
    {
        var lines = e.AllMessages().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var line =
            lines.FirstOrDefault(l => l.StartsWith("fatal:") || l.StartsWith("error:")) ?? lines.FirstOrDefault();
        return line?.Replace("fatal: ", "").Replace("error: ", "") ?? "";
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
