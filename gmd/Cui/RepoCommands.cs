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
}

class RepoCommands : IRepoCommands
{
    private readonly IViewRepoService viewRepoService;

    internal RepoCommands(IViewRepoService viewRepoService)
    {
        this.viewRepoService = viewRepoService;
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
            var commit = repo.Repo.Commits[0];
            if (commit.Id != Repo.UncommittedId)
            {
                return;
            }

            var commitDlg = new CommitDlg(commit.BranchName, repo.Repo.Status.ChangesCount, repo.Repo.Status.MergeMessage);
            if (!commitDlg.Show())
            {
                return;
            }

            if (!Try(out var e, await viewRepoService.CommitAllChangesAsync(repo.Repo, commitDlg.Message)))
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
}
