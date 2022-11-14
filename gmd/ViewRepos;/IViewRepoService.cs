namespace gmd.ViewRepos;

interface IViewRepoService
{
    event Action<ChangeEvent> RepoChange;
    event Action<ChangeEvent> StatusChange;

    Task<R<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches);
    Task<R<Repo>> GetFreshRepoAsync(Repo repo);
    Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo);

    IReadOnlyList<Branch> GetAllBranches(Repo repo);
    IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId);
    Repo ShowBranch(Repo repo, string branchName);
    Repo HideBranch(Repo repo, string name);

    // Git commands
    Task<R> CommitAllChangesAsync(string wd, string message);
    Task<R<CommitDiff>> GetCommitDiffAsync(string wd, string commitId);
    Task<R<CommitDiff>> GetUncommittedDiff(string wd);
    Task<R> PushBranchAsync(string wd, string name);
    Task<R> SwitchToAsync(string wd, string branchName);
    Task<R> MergeBranch(string wd, string name);
    Task<R> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd);
}

internal record ChangeEvent(DateTime TimeStamp);

