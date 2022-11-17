using Terminal.Gui;

namespace gmd.Cui;


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

    void Refresh(string name = "");

    void ShowBranch(string name);
    void HideBranch(string name);
    IReadOnlyList<Server.Branch> GetAllBranches();
    IReadOnlyList<Server.Branch> GetShownBranches();
    Server.Branch GetCurrentBranch();
    IReadOnlyList<Server.Branch> GetCommitBranches();

    void SwitchTo(string branchName);
    void Commit();
    void PushCurrentBranch();
    void ShowUncommittedDiff();

    bool CanPush();
    bool CanPushCurrentBranch();
    void MergeBranch(string name);
    void CreateBranch();
    void CreateBranchFromCommit();
    void DeleteBranch(string name);
}

class ViewRepo : IRepo
{
    readonly IRepoView repoView;
    private readonly IGraphService graphService;
    readonly Server.IServer viewRepoService;
    private readonly ICommitDlg commitDlg;
    private readonly Func<IRepo, IDiffView> newDiffView;
    private readonly ICreateBranchDlg createBranchDlg;
    private readonly IProgress progress;

    internal ViewRepo(
        IRepoView repoView,
        Server.Repo repo,
        IGraphService graphService,
        Server.IServer viewRepoService,
        ICommitDlg commitDlg,
        Func<IRepo, IDiffView> newDiffView,
        ICreateBranchDlg createBranchDlg,
        IProgress progress)
    {
        this.repoView = repoView;
        Repo = repo;
        this.graphService = graphService;
        this.viewRepoService = viewRepoService;
        this.commitDlg = commitDlg;
        this.newDiffView = newDiffView;
        this.createBranchDlg = createBranchDlg;
        this.progress = progress;
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

    public void Refresh(string addName = "") => repoView.Refresh(addName);
    public void UpdateRepoTo(Server.Repo newRepo) => repoView.UpdateRepoTo(newRepo);


    public void ShowBranch(string name)
    {
        Server.Repo newRepo = viewRepoService.ShowBranch(Repo, name);
        UpdateRepoTo(newRepo);
    }

    public void HideBranch(string name)
    {
        Server.Repo newRepo = viewRepoService.HideBranch(Repo, name);
        UpdateRepoTo(newRepo);
    }

    public Server.Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);

    public IReadOnlyList<Server.Branch> GetAllBranches() => viewRepoService.GetAllBranches(Repo);
    public IReadOnlyList<Server.Branch> GetShownBranches() => Repo.Branches;

    public IReadOnlyList<Server.Branch> GetCommitBranches() =>
        viewRepoService.GetCommitBranches(Repo, CurrentIndexCommit.Id);


    public void SwitchTo(string branchName) => Do(async () =>
    {
        using (progress.Show())
        {
            if (!Try(out var e, await viewRepoService.SwitchToAsync(branchName, Repo.Path)))
            {
                return R.Error($"Failed to switch to {branchName}", e);
            }
            return R.Ok;
        }
    });



    public void Commit() => Do(async () =>
    {
        if (!commitDlg.Show(this, out var message)) return R.Ok;


        if (!Try(out var e, await viewRepoService.CommitAllChangesAsync(message, Repo.Path)))
        {
            return R.Error($"Failed to commit", e);
        }

        Refresh();
        return R.Ok;
    });



    public void PushCurrentBranch() => Do(async () =>
    {
        var branch = Repo.Branches.First(b => b.IsCurrent);

        if (!Try(out var e, await viewRepoService.PushBranchAsync(branch.Name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {branch.Name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public void ShowUncommittedDiff()
    {
        var diffView = newDiffView(this);
        diffView.ShowUncommittedDiff();
    }

    public void MergeBranch(string name) => Do(async () =>
    {
        if (!Try(out var e, await viewRepoService.MergeBranch(name, Repo.Path)))
        {
            return R.Error($"Failed to merge branch {name}", e);
        }

        Refresh();
        return R.Ok;
    });


    public bool CanPush() => CanPushCurrentBranch();

    public bool CanPushCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return Repo.Status.IsOk &&
         branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }

    public void CreateBranch() => Do(async () =>
     {
         var currentBranchName = GetCurrentBranch().Name;
         if (!Try(out var name, createBranchDlg.Show(currentBranchName, ""))) return R.Ok;

         if (!Try(out var e, await viewRepoService.CreateBranchAsync(name, true, Repo.Path)))
         {
             return R.Error($"Failed to create branch {name}", e);
         }

         if (!Try(out e, await viewRepoService.PushBranchAsync(name, Repo.Path)))
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
            await viewRepoService.CreateBranchFromCommitAsync(name, commit.Sid, true, Repo.Path)))
        {
            return R.Error($"Failed to create branch {name}", e);
        }

        if (!Try(out e, await viewRepoService.PushBranchAsync(name, Repo.Path)))
        {
            return R.Error($"Failed to push branch {name} to remote server", e);
        }

        Refresh(name);
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
                await viewRepoService.DeleteLocalBranchAsync(branch.Name, false, Repo.Path)))
            {
                return R.Error($"Failed to delete branch {branch.Name}", e);
            }
        }

        if (remoteBranch != null)
        {
            if (!Try(out var e,
                await viewRepoService.DeleteRemoteBranchAsync(remoteBranch.Name, Repo.Path)))
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
                if (!Try(out var e, await action()!))
                {
                    UI.ErrorMessage($"{e.AllErrorMessages()}");
                }
            }
        });
    }
}

