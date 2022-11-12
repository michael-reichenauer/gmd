namespace gmd.ViewRepos;

interface IViewRepoService
{
    event Action<ChangeEvent> RepoChange;
    event Action<ChangeEvent> StatusChange;

    Task<R<Repo>> GetRepoAsync(string path);
    Task<R<Repo>> GetRepoAsync(string path, string[] showBranches);
    Task<R<Repo>> GetFreshRepoAsync(Repo repo);
    Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo);

    IReadOnlyList<Branch> GetAllBranches(Repo repo);
    IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId);
    Repo ShowBranch(Repo repo, string branchName);
    Repo HideBranch(Repo repo, string name);

    // Git commands
    Task<R> CommitAllChangesAsync(Repo repo, string message);
    Task<R<CommitDiff>> GetCommitDiffAsync(Repo repo, string commitId);
    Task<R<CommitDiff>> GetUncommittedDiff(Repo repo);
    Task<R> PushBranchAsync(Repo repo, string name);
    Task<R> SwitchToAsync(Repo repo, string branchName);
    Task<R> MergeBranch(Repo repo, string name);
}

internal record ChangeEvent(DateTime TimeStamp);

