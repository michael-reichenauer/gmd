using gmd.Common;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Installation;
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
    void UpdateRepoToAtCommit(Server.Repo repo, string commitId);
    void Refresh(string addName = "", string commitId = "");
    void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Server.Commit>? commits = null);
    void RefreshAndFetch(string addName = "", string commitId = "");
    void ToggleDetails();
    void ShowFilter();
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
    readonly IState states;
    readonly IUpdater updater;
    readonly IRepoState repoState;
    readonly IProgress progress;
    readonly IGit git;
    readonly ICommitDetailsView commitDetailsView;
    readonly IFilterDlg filterDlg;
    readonly Func<View, int, IRepoWriter> newRepoWriter;
    readonly ContentView commitsView;
    readonly IRepoWriter repoWriter;

    // State data
    IRepo repo; // Is set once the repo has been retrieved the first time in ShowRepo().
    IRepoCommands Cmd => repo.Cmd;
    IRepoViewMenus menuService = null!;
    bool isStatusUpdateInProgress = false;
    bool isRepoUpdateInProgress = false;
    bool isShowDetails = false;
    bool isShowFilter;
    bool isRegistered = false;
    string hooverPrimaryBranchName = "";
    int hooverRow;
    //int hooverPreCommitRow;

    internal RepoView(
        Server.IServer server,
        Func<View, int, IRepoWriter> newRepoWriter,
        Func<IRepoView, Server.Repo, IRepo> newViewRepo,
        Func<IRepo, IRepoViewMenus> newMenuService,
        IState states,
        IUpdater updater,
        IRepoState repoState,
        IProgress progress,
        IGit git,
        ICommitDetailsView commitDetailsView,
        IFilterDlg filterDlg) : base()
    {
        this.server = server;
        this.newRepoWriter = newRepoWriter;
        this.newViewRepo = newViewRepo;
        this.newMenuService = newMenuService;
        this.states = states;
        this.updater = updater;
        this.repoState = repoState;
        this.progress = progress;
        this.git = git;
        this.commitDetailsView = commitDetailsView;
        this.filterDlg = filterDlg;
        commitsView = new ContentView(onGetContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            IsFocus = true,
            IsShowCursor = true,
            IsCursorMargin = false,
            IsScrollMode = false,
            IsHighlightCurrentIndex = false,
        };
        commitsView.CurrentIndexChange += () => OnCurrentIndexChange();

        repoWriter = newRepoWriter(commitsView, commitsView.ContentX);
        repo = newViewRepo(this, Server.Repo.Empty);

        server.RepoChange += OnRefreshRepo;
        server.StatusChange += OnRefreshStatus;
    }


    public View View => commitsView;
    public View DetailsView => commitDetailsView.View;
    public int ContentWidth => commitsView.ContentWidth;

    public int CurrentIndex => commitsView.CurrentIndex;
    public Point CurrentPoint => commitsView.CurrentPoint;

    public async Task<R> ShowInitialRepoAsync(string path)
    {
        if (!Try(out var e, await ShowRepoAsync(path))) return e;
        UI.AddTimeout(fetchInterval, (_) => FetchFromRemote());
        updater.StartCheckUpdatesRegularly().RunInBackground();

        RegisterShortcuts();
        return R.Ok;
    }

    public async Task<R> ShowRepoAsync(string path)
    {
        if (!Try(out var rootDir, out var e, git.RootPath(path))) return e;
        Log.Info($"Show repo for '{path}' ({rootDir})");

        var branches = repoState.Get(rootDir).Branches;
        if (!Try(out e, await ShowNewRepoAsync(rootDir, branches))) return e;
        FetchFromRemote();

        RememberRepoPaths(rootDir);

        return R.Ok;
    }


    public void UpdateRepoTo(Server.Repo serverRepo, string branchName = "")
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

    public void RefreshAndCommit(string addName = "", string commitId = "", IReadOnlyList<Server.Commit>? commits = null)
    {
        UI.Post(async () =>
        {
            await ShowRefreshedRepoAsync(addName, commitId, false);
            Cmd.Commit(false, commits);
        });
    }


    public void RefreshAndFetch(string addName = "", string commitId = "") =>
          ShowRefreshedRepoAsync(addName, commitId, true).RunInBackground();


    public void ShowFilter()
    {
        isShowFilter = true;
        // Make room for filter dialog
        commitsView.Y = 2;
        commitsView.IsFocus = false;
        commitsView.SetNeedsDisplay();

        var orgRepo = repo.Repo;
        var orgCommit = repo.RowCommit;
        Try(out var commit, out var e, filterDlg.Show(repo.Repo, r => ShowFilteredRepo(r), commitsView));

        // Show Commits view normal again
        isShowFilter = false;
        commitsView.Y = 0;
        commitsView.IsFocus = true;
        commitsView.SetFocus();
        commitsView.SetNeedsDisplay();

        if (commit != null)
        {   // User selected a commit, show it
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

    void OnRefreshRepo(Server.ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (isRepoUpdateInProgress) return;
        if (e.TimeStamp - repo.Repo.RepoTimeStamp < minRepoUpdateInterval) return;

        ShowRefreshedRepoAsync("", "").RunInBackground();
    }

    void OnRefreshStatus(Server.ChangeEvent e)
    {
        UI.AssertOnUIThread();
        if (isStatusUpdateInProgress || isRepoUpdateInProgress) return;
        if (e.TimeStamp - repo.Repo.RepoTimeStamp < minStatusUpdateInterval) return;

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
        commitsView.RegisterKeyHandler(Key.Esc, () => UI.Shutdown());
        commitsView.RegisterKeyHandler(Key.C | Key.CtrlMask, () => Copy());
        commitsView.RegisterKeyHandler(Key.m, () => OnMenu());
        commitsView.RegisterKeyHandler(Key.o, () => menuService.ShowOpenMenu());
        commitsView.RegisterKeyHandler(Key.CursorRight, () => OnCursorRight());
        commitsView.RegisterKeyHandler(Key.CursorLeft, () => OnCursorLeft());
        commitsView.RegisterKeyHandler(Key.r, () => RefreshAndFetch());
        commitsView.RegisterKeyHandler(Key.F5, () => RefreshAndFetch());
        commitsView.RegisterKeyHandler(Key.c, () => Cmd.Commit(false));
        commitsView.RegisterKeyHandler(Key.a, () => Cmd.Commit(true));
        commitsView.RegisterKeyHandler(Key.t, () => Cmd.AddTag());
        commitsView.RegisterKeyHandler(Key.b, () => Cmd.CreateBranch());
        commitsView.RegisterKeyHandler(Key.d, () => Cmd.ShowCurrentRowDiff());
        commitsView.RegisterKeyHandler(Key.D | Key.CtrlMask, () => Cmd.ShowCurrentRowDiff());
        commitsView.RegisterKeyHandler(Key.p, () => Cmd.PushCurrentBranch());
        commitsView.RegisterKeyHandler(Key.P, () => Cmd.PushAllBranches());
        commitsView.RegisterKeyHandler(Key.u, () => Cmd.PullCurrentBranch());
        commitsView.RegisterKeyHandler(Key.U, () => Cmd.PullAllBranches());
        commitsView.RegisterKeyHandler(Key.h, () => Cmd.ShowHelp());
        commitsView.RegisterKeyHandler(Key.F1, () => Cmd.ShowHelp());
        commitsView.RegisterKeyHandler(Key.f, () => Cmd.Filter());

        commitsView.RegisterKeyHandler(Key.Enter, () => OnKeyEnter());
        commitsView.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocus());
        commitsView.RegisterKeyHandler(Key.g, () => Cmd.ChangeBranchColor());

        commitsView.RegisterMouseHandler(MouseFlags.Button1Clicked, (x, y) => Clicked(x, y));
        commitsView.RegisterMouseHandler(MouseFlags.Button1DoubleClicked, (x, y) => DoubleClicked(x, y));
        commitsView.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => RightClicked(x, y));
        commitsView.RegisterMouseHandler(MouseFlags.ReportMousePosition, (x, y) => MouseMoved(x, y));


        // Keys on commit details view.
        commitDetailsView.View.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocus());
        commitDetailsView.View.RegisterKeyHandler(Key.d, () => Cmd.ShowCurrentRowDiff());
    }

    void OnKeyS()
    {
        if (hooverPrimaryBranchName != "")
        {
            if (repo.Status.IsOk && repo.RowCommit.Id != repo.GetCurrentCommit().Id)
            {
                Cmd.SwitchToCommit();
            }

            return;
        }

        ToggleDetails();
    }

    void OnKeyEnter()
    {
        if (hooverPrimaryBranchName != "")
        {
            var branch = repo.Graph.GetRowBranches(repo.CurrentRow)
               .FirstOrDefault(b => b.B.PrimaryName == hooverPrimaryBranchName);
            if (branch != null)
            {
                TryShowHideCommitBranch(branch.X * 2 + 3, commitsView.CurrentPoint.Y + 1);
                return;
            }

            return;
        }

        ToggleDetails();
    }

    void OnMenu()
    {
        if (hooverPrimaryBranchName != "")
        {
            var branch = repo.Graph.GetRowBranches(repo.CurrentRow)
                .FirstOrDefault(b => b.B.PrimaryName == hooverPrimaryBranchName);
            if (branch == null)
            {
                hooverPrimaryBranchName = "";
                commitsView.SetNeedsDisplay();
                return;
            }

            menuService.ShowBranchMenu(branch.X * 2 + 3, commitsView.CurrentPoint.Y, hooverPrimaryBranchName);
            return;
        }

        menuService.ShowCommitMenu(repo.Graph.Width + 5, commitsView.CurrentPoint.Y + 1, commitsView.CurrentIndex);
    }

    private void OnCursorLeft()
    {
        var branches = repo.Graph.GetRowBranches(repo.CurrentRow);

        var hooverIndex = hooverPrimaryBranchName == ""
            ? -1 : branches.FindIndexOf(b => b.B.PrimaryName == hooverPrimaryBranchName);

        if (hooverIndex < 0)
        {   // No hoover branch, or not found, select right most branch
            hooverPrimaryBranchName = branches.Last().B.PrimaryName;
            hooverRow = repo.CurrentRow;
            commitsView.SetNeedsDisplay();
            return;
        }

        if (hooverIndex == 0) return; // Reached left side

        var branch = branches[hooverIndex - 1];
        hooverPrimaryBranchName = branches[hooverIndex - 1].B.PrimaryName;
        hooverRow = repo.CurrentRow;
        commitsView.SetNeedsDisplay();
    }

    private void OnCursorRight()
    {
        if (hooverPrimaryBranchName == "") return;

        var branches = repo.Graph.GetRowBranches(repo.CurrentRow);
        var hooverIndex = branches.FindLastIndexOf(b => b.B.PrimaryName == hooverPrimaryBranchName);

        if (hooverIndex == branches.Count - 1 || hooverIndex == -1)
        {   // Reached right side (or hover branch not found), select commit
            hooverPrimaryBranchName = "";
            hooverRow = repo.CurrentRow;
            commitsView.SetNeedsDisplay();
            return;
        }

        hooverPrimaryBranchName = branches[hooverIndex + 1].B.PrimaryName;
        hooverRow = repo.CurrentRow;
        commitsView.SetNeedsDisplay();
    }

    void Copy()
    {
        var text = commitsView.CopySelectedText();
        if (text != "")
        {
            var lines = text.Split('\n');
            if (lines.Length > 1)
            {
                text = lines
                    .Where(l => l.Trim() != "")
                    .Select(l => l.Substring(repo.Graph.Width + 3))
                    .Join("\n");
            }
            Utils.Clipboard.Set(text);
        }
    }

    void DoubleClicked(int x, int y)
    {
        commitsView.SetIndex(y);
        ToggleDetails();
    }

    void Clicked(int x, int y)
    {
        commitsView.SetIndex(y);

        if (repo.Graph.TryGetBranchByPos(x, y + commitsView.FirstIndex, out var branch))
        {
            TryShowHideCommitBranch(x, y);
        }
    }

    void TryShowHideCommitBranch(int x, int y)
    {
        var commit = repo!.RowCommit;
        var commitBranches = repo.GetCommitBranches();
        var isMergeTargetOpen = commit.ParentIds.Count > 1 &&
            repo.Repo.CommitById.TryGetValue(commit.ParentIds[1], out var mergeParent);

        if (isMergeTargetOpen && commitBranches.Count == 0)
        {   // Close the merged branch
            Cmd.HideBranch(repo.Commit(commit.ParentIds[1]).BranchName);
        }
        else if (commitBranches.Count == 1)
        {   // Just one possible branch, open it
            Cmd.ShowBranch(commitBranches[0].Name, commit.Id);
        }
        else if (commitBranches.Count > 1)
        {   // Multiple possible branches, show menu to select which to open
            menuService.ShowCommitBranchesMenu(x, y);
        }
    }



    void RightClicked(int x, int y)
    {
        int index = y + commitsView.FirstIndex;
        if (x > repo.Graph.Width)
        {
            commitsView.SetIndex(y);
            menuService.ShowCommitMenu(x, y, index);
        }
        else if (repo.Graph.TryGetBranchByPos(x, index, out var branch))
        {
            commitsView.SetIndex(y);
            menuService.ShowBranchMenu(x + 2, y - 1, branch.Name);
        }

    }

    bool MouseMoved(int x, int y)
    {
        var index = y + commitsView.FirstIndex;
        if (repo.Graph.TryGetBranchByPos(x, index, out var branch))
        {   // Moved over a branch
            if (hooverPrimaryBranchName != branch.PrimaryName)
            {
                hooverPrimaryBranchName = branch.PrimaryName;
                hooverRow = index;
                commitsView.SetNeedsDisplay();
            }

            commitsView.SetIndex(y);
            commitsView.SetNeedsDisplay();
            return false;
        }

        if (hooverPrimaryBranchName != "")
        {   // No longer hovering over branch,
            hooverPrimaryBranchName = "";
            commitsView.SetNeedsDisplay();
        }

        return false;
    }


    (IEnumerable<Text> rows, int total) onGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (hooverPrimaryBranchName != "" && hooverRow != currentIndex)
        {
            if (null == repo.Graph
                .GetRowBranches(repo.CurrentRow)
                .FirstOrDefault(b => b.B.PrimaryName == hooverPrimaryBranchName))
            {
                hooverPrimaryBranchName = "";
            }
            hooverRow = currentIndex;
        }

        return (repoWriter.ToPage(repo, firstIndex, count, currentIndex, hooverPrimaryBranchName, hooverRow, width), repo.Commits.Count);
    }


    async Task<R> ShowNewRepoAsync(string path, IReadOnlyList<string> showBranches)
    {
        using (progress.Show())
        {
            var t = Timing.Start();
            if (!Try(out var viewRepo, out var e, await GetRepoAsync(path, showBranches))) return e;

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

            var branchNames = repo!.Branches.Select(b => b.Name).ToList();
            if (addBranchName != "")
            {
                branchNames.Add(addBranchName);
            }

            if (!Try(out var viewRepo, out var e, await GetRepoAsync(repo.RepoPath, branchNames)))
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
                await server.FetchAsync(repo.RepoPath);
            }
        }

        if (!isAwaitFetch)
        {
            server.FetchAsync(repo.RepoPath).RunInBackground();
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
        commitsView.SetNeedsDisplay();
        OnCurrentIndexChange();

        // Remember shown branch for next restart of program
        if (serverRepo.Filter != "") return;

        var names = repo.Branches.Select(b => b.PrimaryBaseName).Distinct().Take(30).ToList();
        repoState.Set(serverRepo.Path, s => s.Branches = names);
        Console.Title = $"{Path.GetFileName(serverRepo.Path).TrimSuffix(".git")} - gmd";
    }


    void ScrollToBranch(string branchName)
    {
        if (branchName != "")
        {
            var branch = repo.Branches.FirstOrDefault(b => b.Name == branchName);
            if (branch != null)
            {
                var tip = repo.Commit(branch.TipId);
                commitsView.ScrollToShowIndex(tip.Index);
                commitsView.SetCurrentIndex(tip.Index);
            }
        }
    }


    void ScrollToCommit(string commitId)
    {
        var commit = repo.Commits.FirstOrDefault(c => c.Id == commitId);
        if (commit != null)
        {
            commitsView.ScrollToShowIndex(commit.Index);
            commitsView.SetCurrentIndex(commit.Index);
        }
    }


    private void ToggleDetailsFocus()
    {
        if (!isShowDetails) return;

        // Shift focus (unfortunately SetFocus() does not seem to work)
        commitsView.IsFocus = !commitsView.IsFocus;
        commitDetailsView.View.IsFocus = !commitDetailsView.View.IsFocus;

        commitDetailsView.View.SetNeedsDisplay();
        commitsView.SetNeedsDisplay();
    }


    void OnCurrentIndexChange()
    {
        if (isShowDetails)
        {
            var commit = repo.RowCommit;
            var branch = repo.Branch(commit.BranchName);
            commitDetailsView.Set(repo.Repo, commit, branch);
        }
    }


    bool FetchFromRemote()
    {
        server.FetchAsync(repo.RepoPath).RunInBackground();
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
        if (isShowFilter) return repo.Repo;

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
        if (isShowFilter) return repo!;

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
