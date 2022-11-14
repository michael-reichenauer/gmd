using gmd.ViewRepos;
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

    void Refresh();

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
}

class ViewRepo : IRepo
{
    readonly IRepoView repoView;
    private readonly IGraphService graphService;
    readonly IViewRepoService viewRepoService;
    private readonly Func<IRepo, ICommitDlg> newCommitDlg;
    private readonly Func<IRepo, IDiffView> newDiffView;

    internal ViewRepo(
        IRepoView repoView,
        Repo repo,
        IGraphService graphService,
        IViewRepoService viewRepoService,
        Func<IRepo, ICommitDlg> newCommitDlg,
        Func<IRepo, IDiffView> newDiffView)
    {
        this.repoView = repoView;
        Repo = repo;
        this.graphService = graphService;
        this.viewRepoService = viewRepoService;
        this.newCommitDlg = newCommitDlg;
        this.newDiffView = newDiffView;
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

    public void Refresh() => repoView.Refresh();
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
        UI.RunInBackground(async () =>
        {
            if (!Try(out var e, await viewRepoService.SwitchToAsync(Repo.Path, branchName)))
            {
                UI.ErrorMessage($"Failed to switch to {branchName}:\n{e}");
                return;
            }
        });
    }


    public void Commit()
    {
        UI.RunInBackground(async () =>
        {
            var commitDlg = newCommitDlg(this);
            if (!commitDlg.Show(out var message))
            {
                return;
            }

            if (!Try(out var e, await viewRepoService.CommitAllChangesAsync(Repo.Path, message)))
            {
                UI.ErrorMessage($"Failed to commit:\n{e}");
                return;
            }

            Refresh();
        });
    }


    public void PushCurrentBranch()
    {
        UI.RunInBackground(async () =>
        {
            var branch = Repo.Branches.First(b => b.IsCurrent);

            if (!Try(out var e, await viewRepoService.PushBranchAsync(Repo.Path, branch.Name)))
            {
                UI.ErrorMessage($"Failed to push branch {branch.Name}:\n{e}");
                return;
            }

            Refresh();
        });
    }

    public void ShowUncommittedDiff()
    {
        var diffView = newDiffView(this);
        diffView.ShowUncommittedDiff();
    }

    public void MergeBranch(string name)
    {
        UI.RunInBackground(async () =>
        {
            if (!Try(out var e, await viewRepoService.MergeBranch(Repo.Path, name)))
            {
                UI.ErrorMessage($"Failed to merge branch {name}:\n{e}");
                return;
            }

            Refresh();
        });
    }

    public bool CanPush() => CanPushCurrentBranch();

    public bool CanPushCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return Repo.Status.IsOk &&
         branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }
}
