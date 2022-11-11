using gmd.ViewRepos;
using Terminal.Gui;

namespace gmd.Cui;


interface IRepo
{
    Repo Repo { get; }
    Graph Graph { get; }
    int TotalRows { get; }
    int CurrentIndex { get; }
    Commit CurrentRowCommit { get; }
    int ContentWidth { get; }
    Point CurrentPoint { get; }
    bool HasUncommittedChanges { get; }

    void Refresh();
    void UpdateRepo(Repo newRepo);

    void ShowBranch(string name);
    void HideBranch(string name);
    IReadOnlyList<Branch> GetAllBranches();
    IReadOnlyList<Branch> GetCommitBranches(Repo repo);

    void Commit();
    void PushCurrentBranch();
    void ShowUncommittedDiff();

    bool CanPush();
    bool CanPushCurrentBranch();
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
    public Commit CurrentRowCommit => Repo.Commits[CurrentIndex];
    public int ContentWidth => repoView.ContentWidth;
    public Point CurrentPoint => repoView.CurrentPoint;
    public bool HasUncommittedChanges => !Repo.Status.IsOk;

    public void Refresh() => repoView.Refresh();
    public void UpdateRepo(Repo newRepo) => repoView.UpdateRepo(newRepo);


    public void ShowBranch(string name)
    {
        Repo newRepo = viewRepoService.ShowBranch(Repo, name);
        UpdateRepo(newRepo);
    }

    public void HideBranch(string name)
    {
        Repo newRepo = viewRepoService.HideBranch(Repo, name);
        UpdateRepo(newRepo);
    }

    public IReadOnlyList<Branch> GetAllBranches() => viewRepoService.GetAllBranches(Repo);

    public IReadOnlyList<Branch> GetCommitBranches(Repo repo) =>
        viewRepoService.GetCommitBranches(repo, CurrentRowCommit.Id);

    public void Commit()
    {
        Do(async () =>
        {
            var commitDlg = newCommitDlg(this);
            if (!commitDlg.Show(out var message))
            {
                return;
            }

            if (!Try(out var e, await viewRepoService.CommitAllChangesAsync(Repo, message)))
            {
                UI.ErrorMessage($"Failed to commit:\n{e}");
                return;
            }

            Refresh();
        });
    }


    public void PushCurrentBranch()
    {
        Do(async () =>
        {
            var branch = Repo.Branches.First(b => b.IsCurrent);

            if (!Try(out var e, await viewRepoService.PushBranchAsync(Repo, branch.Name)))
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

    public bool CanPush() => CanPushCurrentBranch();

    public bool CanPushCurrentBranch()
    {
        var branch = Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return Repo.Status.IsOk &&
         branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }

    void Do(Func<Task> action)
    {
        action().RunInBackground();
    }
}
