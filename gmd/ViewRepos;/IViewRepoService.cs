namespace gmd.ViewRepos;

interface IViewRepoService
{
    public event EventHandler RepoChange;
    Task<R<Repo>> GetRepoAsync(string path);
    Task<R<Repo>> GetRepoAsync(string path, string[] showBranches);
    Task<R<Repo>> GetFreshRepoAsync(Repo repo);

    IReadOnlyList<Branch> GetAllBranches(Repo repo);
    IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId);
    Repo ShowBranch(Repo repo, string branchName);
    Repo HideBranch(Repo repo, string name);

    // Git commands
    Task<R> CommitAllChangesAsync(Repo repo, string message);
}
