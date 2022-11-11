using gmd.ViewRepos;
using NStack;
using Terminal.Gui;

namespace gmd.Cui;

interface IRepo
{
    int ViewWidth { get; }
    Repo Repo { get; }
    int CurrentIndex { get; }
    Point CurrentPoint { get; }

    void Refresh();
    void ShowRepo(Repo newRepo);
}


interface IRepoCommands
{
    void ShowBranch(IRepo repo, string name);
    void HideBranch(IRepo repo, string name);
    void Commit(IRepo repo);
    void PushCurrentBranch(IRepo repo);
    bool CanPushCurrentBranch(IRepo repo);
    bool CanPush(IRepo repo);
    void ShowUncommittedDiff(IRepo repo);
}

class RepoCommands : IRepoCommands
{
    private readonly IViewRepoService viewRepoService;
    private readonly Func<ICommitDlg> newCommitDlg;
    private readonly Func<IDiffView> newDiffView;

    internal RepoCommands(
        IViewRepoService viewRepoService,
        Func<ICommitDlg> newCommitDlg,
        Func<IDiffView> newDiffView)
    {
        this.viewRepoService = viewRepoService;
        this.newCommitDlg = newCommitDlg;
        this.newDiffView = newDiffView;
    }

    public void ShowBranch(IRepo repo, string name)
    {
        Repo newRepo = viewRepoService.ShowBranch(repo.Repo, name);
        repo.ShowRepo(newRepo);
    }

    public void HideBranch(IRepo repo, string name)
    {
        Repo newRepo = viewRepoService.HideBranch(repo.Repo, name);
        repo.ShowRepo(newRepo);
    }

    public void Commit(IRepo repo)
    {
        Do(async () =>
        {
            var commitDlg = newCommitDlg();
            if (!commitDlg.Show(repo, out var message))
            {
                return;
            }

            if (!Try(out var e, await viewRepoService.CommitAllChangesAsync(repo.Repo, message)))
            {
                UI.ErrorMessage($"Failed to commit:\n{e}");
                return;
            }

            repo.Refresh();
        });
    }


    public void PushCurrentBranch(IRepo repo)
    {
        Do(async () =>
        {
            var branch = repo.Repo.Branches.First(b => b.IsCurrent);

            if (!Try(out var e, await viewRepoService.PushBranchAsync(repo.Repo, branch.Name)))
            {
                UI.ErrorMessage($"Failed to push branch {branch.Name}:\n{e}");
                return;
            }

            repo.Refresh();
        });
    }

    public bool CanPushCurrentBranch(IRepo repo)
    {
        var branch = repo.Repo.Branches.FirstOrDefault(b => b.IsCurrent);
        return repo.Repo.Status.IsOk &&
         branch != null && branch.HasLocalOnly && !branch.HasRemoteOnly;
    }

    void Do(Func<Task> action)
    {
        action().RunInBackground();
    }

    public bool CanPush(IRepo repo) =>
        CanPushCurrentBranch(repo);

    public void ShowUncommittedDiff(IRepo repo)
    {
        var diffView = newDiffView();
        diffView.Show(repo.Repo, Repo.UncommittedId);
    }
}
