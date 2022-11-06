namespace gmd.ViewRepos;

interface IViewRepoService
{
    public event EventHandler RepoChange;
    Task<R<Repo>> GetRepoAsync(string path);
    Task<R<Repo>> GetRepoAsync(string path, string[] showBranches);

    IReadOnlyList<Branch> GetAllBranches(Repo repo);
    Task<Repo> ShowBranch(Repo repo, string branchName);
    Task<Repo> HideBranch(Repo repo, string name);
}
