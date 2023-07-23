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
    string hooverBranchName = "";
    int hooverIndex = -1;
    int hooverCurrentCommitIndex = -1;

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
        commitsView.RegisterKeyHandler(Key.q, () => UI.Shutdown());
        commitsView.RegisterKeyHandler(Key.C | Key.CtrlMask, () => Copy());
        commitsView.RegisterKeyHandler(Key.m, () => OnMenu());
        commitsView.RegisterKeyHandler(Key.o, () => menuService.ShowOpenMenu());
        commitsView.RegisterKeyHandler(Key.CursorLeft, () => OnCursorLeft());
        commitsView.RegisterKeyHandler(Key.CursorRight, () => OnCursorRight());
        commitsView.RegisterKeyHandler(Key.CursorUp, () => OnCursorUp());
        commitsView.RegisterKeyHandler(Key.CursorDown, () => OnCursorDown());

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
        commitsView.RegisterKeyHandler(Key.D1, () => Cmd.ShowHelp());
        commitsView.RegisterKeyHandler(Key.F1, () => Cmd.ShowHelp());
        commitsView.RegisterKeyHandler(Key.f, () => Cmd.Filter());

        commitsView.RegisterKeyHandler(Key.y, () => Cmd.ShowBranch(repo.GetCurrentBranch().Name, false));
        commitsView.RegisterKeyHandler(Key.s, () => OnKeyS());
        commitsView.RegisterKeyHandler(Key.e, () => OnKeyE());
        commitsView.RegisterKeyHandler(Key.h, () => Cmd.HideBranch(repo.RowBranch.Name));

        commitsView.RegisterKeyHandler(Key.Enter, () => OnKeyEnter());
        commitsView.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocus());
        commitsView.RegisterKeyHandler(Key.g, () => Cmd.ChangeBranchColor());

        commitsView.RegisterMouseHandler(MouseFlags.Button1Clicked, (x, y) => OnClicked(x, y));
        commitsView.RegisterMouseHandler(MouseFlags.Button1DoubleClicked, (x, y) => OnDoubleClicked(x, y));
        commitsView.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => OnRightClicked(x, y));
        commitsView.RegisterMouseHandler(MouseFlags.ReportMousePosition, (x, y) => OnMouseMoved(x, y));

        // Keys on commit details view.
        commitDetailsView.View.RegisterKeyHandler(Key.Tab, () => ToggleDetailsFocus());
        commitDetailsView.View.RegisterKeyHandler(Key.d, () => Cmd.ShowCurrentRowDiff());
    }


    void OnKeyE()
    {
        if (hooverBranchName != "")
        {
            var branch = repo.Branch(hooverBranchName);
            if (branch.LocalName != "") branch = repo.Branch(branch.LocalName);
            if (!branch.IsCurrent && repo.Status.IsOk)
            {
                Cmd.MergeBranch(hooverBranchName);
            }
        }
    }


    void OnKeyS()
    {
        if (hooverBranchName != "")
        {
            var branchName = hooverBranchName;
            var currentName = repo.CurrentBranch?.PrimaryName ?? "";
            var branch = repo.Branch(branchName);
            if (branch.LocalName != "") branchName = branch.LocalName;

            if (branch.PrimaryName != currentName)
            {
                Cmd.SwitchTo(branchName);
            }

            return;
        }
    }

    void OnKeyEnter()
    {
        if (hooverBranchName != "")
        {
            var branch = repo.Graph.GetRowBranches(repo.CurrentIndex)
               .FirstOrDefault(b => b.B.PrimaryName == hooverBranchName);
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
        if (hooverBranchName != "")
        {
            var branch = repo.Graph.GetRowBranches(repo.CurrentIndex)
                .FirstOrDefault(b => b.B.PrimaryName == hooverBranchName);
            if (branch == null)
            {
                ClearHoover();
                return;
            }

            menuService.ShowBranchMenu(branch.X * 2 + 3, commitsView.CurrentIndex + 1, hooverBranchName);
            return;
        }

        menuService.ShowCommitMenu(repo.Graph.Width + 5, commitsView.CurrentIndex + 1, commitsView.CurrentIndex);
    }

    private void OnCursorLeft()
    {
        var branches = repo.Graph.GetRowBranches(repo.CurrentIndex);
        var hooverIndex = hooverBranchName == ""
            ? -1 : branches.FindIndexOf(b => b.B.PrimaryName == hooverBranchName);

        if (hooverIndex < 0)
        {   // No hoover branch, or not found, select right most branch
            var hb = branches.Last();
            SetHooverBranch(hb.B.PrimaryName, hb.X, repo.CurrentIndex);
            return;
        }

        if (hooverIndex == 0) return; // Reached left side

        var branch = branches[hooverIndex - 1];
        SetHooverBranch(branch.B.PrimaryName, branch.X, repo.CurrentIndex);
    }

    private void OnCursorRight()
    {
        if (hooverBranchName == "") return; // Commit is hoovered or selected

        var branches = repo.Graph.GetRowBranches(repo.CurrentIndex);
        var hooverIndex = branches.FindLastIndexOf(b => b.B.PrimaryName == hooverBranchName);

        if (hooverIndex == branches.Count - 1 || hooverIndex == -1)
        {   // Reached right side (or hover branch not found), clear hoover commit
            ClearHoover();
            return;
        }
        var hb = branches[hooverIndex + 1];
        SetHooverBranch(hb.B.PrimaryName, hb.X, repo.CurrentIndex);
    }

    void OnCursorUp()
    {
        // Store current hover branch (if any)
        var hoverName = hooverBranchName;
        var hoverColumnIndex = hoverName != "" ? repo.Graph.GetRowBranches(repo.CurrentIndex)
            .FindIndexOf(b => b.B.PrimaryName == hoverName) : -1;

        commitsView.ClearSelection();
        commitsView.Move(-1);

        if (hoverName != "")
        {    // Try restore hover branch after moving upp
            // Try locate the same branch on the previous row or the next branch on the previous row
            var branches = repo.Graph.GetRowBranches(repo.CurrentIndex);
            var branch = branches.FirstOrDefault(b => b.B.PrimaryName == hoverName);
            if (branch == null)
            {// Try locate some other branch on this row
                branch = branches[Math.Max(0, Math.Min(hoverColumnIndex, branches.Count - 1))];
            }

            SetHooverBranch(branch.B.PrimaryName, branch.X, repo.CurrentIndex);
        }
    }

    void OnCursorDown()
    {
        // Store current hover branch (if any)
        var hoverName = hooverBranchName;
        var hoverColumnIndex = hoverName != "" ? repo.Graph.GetRowBranches(repo.CurrentIndex)
            .FindIndexOf(b => b.B.PrimaryName == hoverName) : -1;

        commitsView.ClearSelection();
        commitsView.Move(1);

        if (hoverName != "")
        {   // Try restore hover branch after moving down
            // Try locate the same branch on the previous row or the next branch on the previous row
            var branches = repo.Graph.GetRowBranches(repo.CurrentIndex);
            var branch = branches.FirstOrDefault(b => b.B.PrimaryName == hoverName);
            if (branch == null)
            {// Try locate some other branch on this row
                branch = branches[Math.Max(0, Math.Min(hoverColumnIndex, branches.Count - 1))];
            }

            SetHooverBranch(branch.B.PrimaryName, branch.X, repo.CurrentIndex);
        }
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

    void OnDoubleClicked(int x, int y)
    {
        commitsView.SetIndexAtViewY(y);
        ToggleDetails();
    }

    void OnClicked(int x, int y)
    {
        var index = y + commitsView.FirstIndex;
        commitsView.SetCurrentIndex(index);

        if (repo.Graph.TryGetBranchByPos(x, index, out var _))
        {   // Clicked on a branch, try to show/hide branch if point is a e.g. a merge, branch-out commit
            TryShowHideCommitBranch(x, y);
        }
    }


    void OnRightClicked(int x, int y)
    {
        int index = y + commitsView.FirstIndex;

        if (x > repo.Graph.Width)
        {   // Right-clicked on commit, show commit menu
            menuService.ShowCommitMenu(x, y + 1, index);
            ClearHoover();
            return;
        }

        if (repo.Graph.TryGetBranchByPos(x, index, out var branch))
        {   // Right-clicked on branch, show commit menu
            menuService.ShowBranchMenu(x + 2, y + 1, branch.B.Name);
            ClearHoover();
            return;
        }
    }

    bool OnMouseMoved(int x, int y)
    {
        SetHoover(x, y);
        return false;
    }


    (IEnumerable<Text> rows, int total) onGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (hooverBranchName != "" && hooverCurrentCommitIndex != currentIndex)
        {
            if (null == repo.Graph
                .GetRowBranches(repo.CurrentIndex)
                .FirstOrDefault(b => b.B.PrimaryName == hooverBranchName))
            {
                ClearHoover();
            }
            hooverIndex = currentIndex;
        }

        return (repoWriter.ToPage(repo, firstIndex, count, currentIndex, hooverBranchName, hooverIndex, width), repo.Commits.Count);
    }


    void SetHoover(int x, int y)
    {
        if (x > repo.Graph.Width)
        {
            SetHoverCommit(x, y);
            return;
        }

        var index = y + commitsView.FirstIndex;
        if (repo.Graph.TryGetBranchByPos(x, index, out var branch))
        {   // Moved over a branch
            SetHooverBranch(branch.B.PrimaryName, branch.X * 2 + 3, index);
            return;
        }

        ClearHoover();
    }

    void SetHoverCommit(int x, int y)
    {
        if (x > repo.Graph.Width)
        {
            var index = y + commitsView.FirstIndex;
            hooverBranchName = "";
            hooverIndex = index;
            hooverCurrentCommitIndex = repo.CurrentIndex;
            commitsView.SetNeedsDisplay();
            return;
        }
    }


    void SetHooverBranch(string branchName, int graphColumn, int index)
    {
        if (hooverBranchName != branchName)
        {
            hooverBranchName = branchName;
            hooverIndex = index;
            hooverCurrentCommitIndex = repo.CurrentIndex;
            commitsView.SetNeedsDisplay();
        }
    }

    void ClearHoover()
    {
        if (hooverBranchName != "" || hooverIndex != -1 || hooverCurrentCommitIndex != -1)
        {
            hooverBranchName = "";
            hooverIndex = -1;
            hooverCurrentCommitIndex = -1;
            commitsView.SetNeedsDisplay();
        }
    }

    void TryShowHideCommitBranch(int x, int y)
    {
        var commit = repo.RowCommit;
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
        else if (commit.Id == repo.Branch(commit.BranchPrimaryName).TipId
            || commit.Id == repo.Branch(commit.BranchPrimaryName).BottomId)
        {
            Cmd.HideBranch(commit.BranchPrimaryName);
        }
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
