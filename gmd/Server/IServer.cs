namespace gmd.Server;

interface IServer
{
    event Action<ChangeEvent> RepoChange;
    event Action<ChangeEvent> StatusChange;

    Task<R<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches);
    Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo);

    IReadOnlyList<Commit> GetFilterCommits(Repo repo, string filter);
    IReadOnlyList<Branch> GetAllBranches(Repo repo);
    IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId);
    Branch AllBanchByName(Repo repo, string name);

    Repo ShowBranch(Repo repo, string branchName, bool includeAmbiguous);
    Repo HideBranch(Repo repo, string name);
    Task<R> ResolveAmbiguityAsync(Repo repo, string name, string parentName);
    Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId);

    // Git commands
    Task<R> FetchAsync(string wd);
    Task<R> CommitAllChangesAsync(string message, string wd);
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd);
    Task<R> PushBranchAsync(string name, string wd);
    Task<R> PullCurrentBranchAsync(string wd);
    Task<R> PullBranchAsync(string name, string wd);
    Task<R> SwitchToAsync(string branchName, string wd);
    Task<R> MergeBranch(string name, string wd);
    Task<R> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<R> DeleteRemoteBranchAsync(string name, string wd);
    Task<R> UndoAllUncommittedChangesAsync(string wd);
    Task<R> UndoUncommittedFileAsync(string path, string wd);
    Task<R> CleanWorkingFolderAsync(string wd);
    Task<R> UndoCommitAsync(string id, string wd);
    Task<R> UncommitLastCommitAsync(string wd);
}

internal record ChangeEvent(DateTime TimeStamp);

