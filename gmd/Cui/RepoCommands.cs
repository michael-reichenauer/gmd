using gmd.ViewRepos;
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
    Task CommitAsync(IRepo repo);
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

    public async Task CommitAsync(IRepo repo)
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
        var rsp = await this.viewRepoService.CommitAllChangesAsync(repo.Repo, commitDlg.Message);
        if (rsp.IsError)
        {
            UI.ErrorMessage($"Failed to commit:\n{rsp.Error.Message}");
            return;
        }
        repo.Refresh();
    }

}
