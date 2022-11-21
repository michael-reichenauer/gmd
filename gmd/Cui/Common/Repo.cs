using gmd.Common;
using gmd.Git;
using Terminal.Gui;


namespace gmd.Cui.Common;


interface IRepo
{
    Server.Repo Repo { get; }
    Graph Graph { get; }
    int TotalRows { get; }
    int CurrentIndex { get; }
    Server.Commit CurrentIndexCommit { get; }
    Server.Branch? CurrentBranch { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    bool HasUncommittedChanges { get; }

    void ToggleDetails();
    void ShowAbout();
    void Refresh(string addBranchName = "", string commitId = "");
    void ShowRepo(string path);
    void ShowBrowseDialog();
    void Filter();

    void ShowBranch(string name);
    void HideBranch(string name);
    IReadOnlyList<Server.Branch> GetAllBranches();
    IReadOnlyList<Server.Branch> GetShownBranches();
    Server.Branch GetCurrentBranch();
    IReadOnlyList<Server.Branch> GetCommitBranches();

    void SwitchTo(string branchName);
    void Commit();
    void CommitFromMenu();
    void PushCurrentBranch();
    void PushBranch(string name);
    bool CanPush();
    bool CanPushCurrentBranch();
    void PullCurrentBranch();
    void PullBranch(string name);
    bool CanPull();
    bool CanPullCurrentBranch();
    void ShowUncommittedDiff();
    void ShowCurrentRowDiff();

    void MergeBranch(string name);
    void CreateBranch();
    void CreateBranchFromCommit();
    void DeleteBranch(string name);
}

class RepoImpl : IRepo
{
    readonly IRepoView repoView;
    private readonly IGraphService graphService;
    readonly Server.IServer server;
    private readonly ICommitDlg commitDlg;
    private readonly IDiffView diffView;
    private readonly ICreateBranchDlg createBranchDlg;
    private readonly IProgress progress;
    private readonly IFilterDlg filterDlg;
    private readonly IState state;
    private readonly IGit git;

    internal RepoImpl(
        IRepoView repoView,
        Server.Repo repo,
        IGraphService graphService,
        Server.IServer server,
        ICommitDlg commitDlg,
        IDiffView diffView,
        ICreateBranchDlg createBranchDlg,
        IProgress progress,
        IFilterDlg filterDlg,
        IState state,
        IGit git)
    {
        this.repoView = repoView;
        Repo = repo;
        this.graphService = graphService;
        this.server = server;
        this.commitDlg = commitDlg;
        this.diffView = diffView;
        this.createBranchDlg = createBranchDlg;
        this.progress = progress;
        this.filterDlg = filterDlg;
        this.state = state;
        this.git = git;
        Graph = graphService.CreateGraph(repo);
    }

    public Server.Repo Repo { get; init; }

    public Graph Graph { get; init; }
    public int TotalRows => Repo.Commits.Count;
    public int CurrentIndex => repoView.CurrentIndex;
    public Server.Commit CurrentIndexCommit => Repo.Commits[CurrentIndex];
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;
    public bool HasUncommittedChanges => !Repo.Status.IsOk;
    public Server.Branch? CurrentBranch => Repo.Branches.FirstOrDefault(b => b.IsCurrent);

    public void Refresh(string addName = "", string commitId = "") => repoView.Refresh(addName, commitId);
    public void UpdateRepoTo(Server.Repo newRepo, string branchName = "") => repoView.UpdateRepoTo(newRepo, branchName);
    public void ToggleDetails() => repoView.ToggleDetails();

    public void ShowRepo(string path) => Do(async () =>
    {
        if (!Try(out var e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });

    public void ShowBrowseDialog() => Do(async () =>
    {
        // Parent folders to recent work folders, usually other repos there as well
        var recentFolders = state.Get().RecentParentFolders;

        var browser = new FolderBrowseDlg();
        if (!Try(out var path, browser.Show(recentFolders))) return R.Ok;

        if (!Try(out var e, await repoView.ShowRepoAsync(path)))
        {
            return R.Error($"Failed to open repo at {path}", e);
        }
        return R.Ok;
    });

    public void ShowBranch(string name)
    {
        Server.Repo newRepo = server.ShowBranch(Repo, name);
        UpdateRepoTo(newRepo, name);

    }

    public void HideBranch(string name)
    {
        Server.Repo newRepo = server.HideBranch(Repo, name);
        UpdateRepoTo(newRepo);
    }

    public Server.Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);

    public IReadOnlyList<Server.Branch> GetAllBranches() => server.GetAllBranches(Repo);
    public IReadOnlyList<Server.Branch> GetShownBranches() => Repo.Branches;

    public IReadOnlyList<Server.Branch> GetCommitBranches() =>
        server.GetCommitBranches(Repo, CurrentIndexCommit.Id);


    public void SwitchTo(string branchName) => Do(async () =>
    {
        if (!Try(out var e, await server.SwitchToAsync(branchName, Repo.Path)))
        {
            return R.Error($"Failed to switch to {branchName}", e);
        }
        return R.Ok;
    });


    public void Filter() => Do(async () =>
     {
         if (!Try(out var commit, out var e, filterDlg.Show(this))) return R.Ok;
         await Task.Delay(0);

         Refresh(commit.BranchName, commit.Id);
         return R.Ok;
     });


    public void CommitFromMenu()
    {
        // For some unknown reason, calling commit directly from the menu will
        // Show the commit dialog, but diff will not work since async/await does not seem to work
        // However wrapping with a timeout seems to work as desired.
        UI.AddTimeout(TimeSpan.FromMilliseconds(100), (_) =>
        {
            Commit();
            return false;
        });
    }

    public void Commit() => Do(async () =>
    {
        if (!commitDlg.Show(this, out var message)) return R.Ok;


        if (!Try(out var e, await server.CommitAllChangesAsync(message, Repo.Path)))
        {
            return R.Error($"Failed to commit", e);
        }

        Refresh();
        return R.Ok;
    });


    public void ShowUncommittedDiff() => ShowDiff(Server.Repo.UncommittedId);

    public void ShowCurrentRowDiff() => ShowDiff(CurrentIndexCommit.Id);

    public void ShowDiff(string commitId) => Do(async () =>
    {
        if (!Try(out var diff, out var e, await server.GetCommitDiffAsync(commitId, Repo.Path)))
        {
            return R.Error($"Failed to get diff", e);
        }

        diffView.Show(diff, commitId);
        return R.Ok;
    });


    public void MergeBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.MergeBranch(name, Repo.Path)))
        {
            return R.Error($"Failed to merge branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushCurrentBranch() => Do(async () =>
    {
        var branch = Repo.Branches.First(b => b.IsCurrent);

        if (!Try(out var e, await server.PushBranchAsync(branch.Name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void PushBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PushBranchAsync(name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public bool CanPush() => Repo.Status.IsOk && GetShownBranches().Any(b => b.HasLocalOnly && !b.HasRemoteOnly);

    public bool CanPushCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return Repo.Status.IsOk &&
            branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }


    public void PullCurrentBranch() => Do(async () =>
    {
        if (!Try(out var e, await server.PullCurrentBranchAsync(Repo.Path)))
        {
            return R.Error($"Failed to pull current branch", e);
        }

        Refresh();
        return R.Ok;
    });

    public void PullBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await server.PullBranchAsync(name, Repo.Path)))
        {
            return R.Error($"Failed to pull branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });

    public bool CanPull() => Repo.Status.IsOk && GetShownBranches().Any(b => b.HasRemoteOnly);

    public bool CanPullCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return Repo.Status.IsOk && branch != null && branch.HasRemoteOnly;
    }


    public void CreateBranch() => Do(async () =>
     {
         var currentBranchName = GetCurrentBranch().Name;
         if (!Try(out var name, createBranchDlg.Show(currentBranchName, ""))) return R.Ok;

         if (!Try(out var e, await server.CreateBranchAsync(name, true, Repo.Path)))
         {
             return R.Error($"Failed to create branch {name}", e);
         }

         if (!Try(out e, await server.PushBranchAsync(name, Repo.Path)))
         {
             return R.Error($"Failed to push branch {name} to remote server", e);
         }

         Refresh(name);
         return R.Ok;
     });


    public void CreateBranchFromCommit() => Do(async () =>
    {
        var commit = CurrentIndexCommit;
        var branchName = commit.BranchName;

        if (!Try(out var name, createBranchDlg.Show(branchName, commit.Sid))) return R.Ok;

        if (!Try(out var e,
            await server.CreateBranchFromCommitAsync(name, commit.Sid, true, Repo.Path)))
        {
            return R.Error($"Failed to create branch {name}", e);
        }

        if (!Try(out e, await server.PushBranchAsync(name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {name} to remote server", e);
        }

        Refresh(name);
        return R.Ok;
    });

    public void ShowAbout() => Do(async () =>
     {
         var gmdVersion = Util.GetBuildVersion();
         var gmdBuildTime = Util.GetBuildTime().ToString("yyyy-MM-dd HH:mm");
         if (!Try(out var gitVersion, out var e, await git.Version())) return e;

         var msg =
             $"Version: {gmdVersion}\n" +
             $"Built:   {gmdBuildTime}\n" +
             $"Git:     {gitVersion}";

         UI.InfoMessage("About", msg);
         return R.Ok;
     });


    public void DeleteBranch(string name) => Do(async () =>
    {
        var allBranches = GetAllBranches();
        var branch = allBranches.First(b => b.Name == name);

        Server.Branch? localBranch = null;
        Server.Branch? remoteBranch = null;

        if (!branch.IsRemote)
        {
            // Branch is a local branch
            localBranch = branch;
            if (branch.RemoteName != "")
            {    //with a corresponding remote branch
                remoteBranch = allBranches.First(b => b.Name == branch.RemoteName);
            }
        }
        else
        {   // Branch is a remote branch 
            remoteBranch = branch;
            if (branch.LocalName != "")
            {   // with a coresponding local branch
                localBranch = allBranches.First(b => b.Name == branch.LocalName);
            }
        }

        if (localBranch != null)
        {
            if (!Try(out var e,
                await server.DeleteLocalBranchAsync(branch.Name, false, Repo.Path)))
            {
                return R.Error($"Failed to delete branch {branch.Name}", e);
            }
        }

        if (remoteBranch != null)
        {
            if (!Try(out var e,
                await server.DeleteRemoteBranchAsync(remoteBranch.Name, Repo.Path)))
            {
                return R.Error($"Failed to delete remote branch {branch.Name}", e);
            }
        }

        Refresh();
        return R.Ok;
    });


    void Do(Func<Task<R>> action)
    {
        UI.RunInBackground(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await action()))
                {
                    UI.ErrorMessage($"{e.AllErrorMessages()}");
                }
            }
        });
    }
}

