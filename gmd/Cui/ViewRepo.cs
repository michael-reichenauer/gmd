using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;


interface IRepo
{
    Repo Repo { get; }
    Graph Graph { get; }
    int TotalRows { get; }
    int CurrentIndex { get; }
    Commit CurrentIndexCommit { get; }
    Branch? CurrentBranch { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    bool HasUncommittedChanges { get; }

    void Refresh(string name = "");

    void ShowBranch(string name);
    void HideBranch(string name);
    IReadOnlyList<Branch> GetAllBranches();
    Branch GetCurrentBranch();
    IReadOnlyList<Branch> GetCommitBranches();

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
    readonly IServer viewRepoService;
    private readonly Func<IRepo, ICommitDlg> newCommitDlg;
    private readonly Func<IRepo, IDiffView> newDiffView;
    private readonly ICreateBranchDlg createBranchDlg;
    private readonly IProgress progress;

    internal ViewRepo(
        IRepoView repoView,
        Repo repo,
        IGraphService graphService,
        IServer viewRepoService,
        Func<IRepo, ICommitDlg> newCommitDlg,
        Func<IRepo, IDiffView> newDiffView,
        ICreateBranchDlg createBranchDlg,
        IProgress progress)
    {
        this.repoView = repoView;
        Repo = repo;
        this.graphService = graphService;
        this.viewRepoService = viewRepoService;
        this.newCommitDlg = newCommitDlg;
        this.newDiffView = newDiffView;
        this.createBranchDlg = createBranchDlg;
        this.progress = progress;
        Graph = graphService.CreateGraph(repo);
    }

    public Repo Repo { get; init; }

    public Graph Graph { get; init; }
    public int TotalRows => Repo.Commits.Count;
    public int CurrentIndex => repoView.CurrentIndex;
    public Commit CurrentIndexCommit => Repo.Commits[CurrentIndex];
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;
    public bool HasUncommittedChanges => !Repo.Status.IsOk;
    public Branch? CurrentBranch => Repo.Branches.FirstOrDefault(b => b.IsCurrent);

    public void Refresh(string addName = "") => repoView.Refresh(addName);
    public void UpdateRepoTo(Repo newRepo) => repoView.UpdateRepoTo(newRepo);


    public void ShowBranch(string name)
    {
        Repo newRepo = viewRepoService.ShowBranch(Repo, name);
        UpdateRepoTo(newRepo);
    }

    public void HideBranch(string name)
    {
        Repo newRepo = viewRepoService.HideBranch(Repo, name);
        UpdateRepoTo(newRepo);
    }

    public Branch GetCurrentBranch() => GetAllBranches().First(b => b.IsCurrent);

    public IReadOnlyList<Branch> GetAllBranches() => viewRepoService.GetAllBranches(Repo);

    public IReadOnlyList<Branch> GetCommitBranches() =>
        viewRepoService.GetCommitBranches(Repo, CurrentIndexCommit.Id);


    public void SwitchTo(string branchName)
    {
        Do(async () =>
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
    }


    public void Commit()
    {
        Do(async () =>
        {
            var commitDlg = newCommitDlg(this);
            if (!commitDlg.Show(out var message)) return R.Ok;

            using (progress.Show())
            {
                if (!Try(out var e, await viewRepoService.CommitAllChangesAsync(message, Repo.Path)))
                {
                    return R.Error($"Failed to commit", e);
                }

                Refresh();
                return R.Ok;
            }
        });
    }


    public void PushCurrentBranch()
    {
        Do(async () =>
        {
            using (progress.Show())
            {
                var branch = Repo.Branches.First(b => b.IsCurrent);

                if (!Try(out var e, await viewRepoService.PushBranchAsync(branch.Name, Repo.Path)))
                {
                    return R.Error($"Failed to push branch {branch.Name}", e);
                }

                Refresh();
                return R.Ok;
            }
        });
    }

    public void ShowUncommittedDiff()
    {
        var diffView = newDiffView(this);
        diffView.ShowUncommittedDiff();
    }

    public void MergeBranch(string name)
    {
        Do(async () =>
        {
            using (progress.Show())
            {
                if (!Try(out var e, await viewRepoService.MergeBranch(name, Repo.Path)))
                {
                    return R.Error($"Failed to merge branch {name}", e);
                }

                Refresh();
                return R.Ok;
            }
        });
    }

    public bool CanPush() => CanPushCurrentBranch();

    public bool CanPushCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return Repo.Status.IsOk &&
         branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }

    public void CreateBranch()
    {
        Do(async () =>
        {
            var currentBranchName = GetCurrentBranch().Name;
            if (!Try(out var name, createBranchDlg.Show(currentBranchName, ""))) return R.Ok;

            using (progress.Show())
            {
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
            }
        });
    }

    public void CreateBranchFromCommit()
    {
        Do(async () =>
        {
            var commit = CurrentIndexCommit;
            var branchName = commit.BranchName;

            if (!Try(out var name, createBranchDlg.Show(branchName, commit.Sid))) return R.Ok;

            using (progress.Show())
            {
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
            }
        });
    }

    public void DeleteBranch(string name)
    {
        Do(async () =>
        {
            using (progress.Show())
            {
                var allBranches = GetAllBranches();
                var branch = allBranches.First(b => b.Name == name);

                Branch? localBranch = null;
                Branch? remoteBranch = null;

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
            }
        });
    }

    internal void Do(Func<Task<R>> action)
    {
        UI.RunInBackground(async () =>
        {
            if (!Try(out var e, await action()!))
            {
                UI.ErrorMessage($"{e.AllErrorMessages()}");
            }
        });
    }
}

