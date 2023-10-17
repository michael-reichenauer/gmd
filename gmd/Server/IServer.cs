namespace gmd.Server;

enum ShowBranches
{
    Specified,
    AllRecent,
    AllActive,
    AllActiveAndDeleted,
}

interface IServer
{
    event Action<ChangeEvent> RepoChange;
    event Action<ChangeEvent> StatusChange;

    Task<R<Repo>> GetRepoAsync(string path, IReadOnlyList<string> showBranches);
    Task<R<Repo>> GetUpdateStatusRepoAsync(Repo repo);
    Task<R<Repo>> GetFilteredRepoAsync(Repo repo, string filter, int maxCount);

    IReadOnlyList<Branch> GetCommitBranches(Repo repo, string commitId, bool isAll = false);
    IReadOnlyList<string> GetPossibleBranchNames(Repo repo, string commitId, int maxCount);

    Repo ShowBranch(Repo repo, string branchName, bool includeAmbiguous, ShowBranches show = ShowBranches.Specified, int count = 1);
    Repo HideBranch(Repo repo, string name, bool hideAllBranches = false);
    Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName);
    Task<R> UnresolveAmbiguityAsync(Repo repo, string commitId);
    Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName);
    Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd);
    Task<R> CreateBranchFromBranchAsync(Repo serverRepo, string newBranchName, string sourceBranch, bool isCheckout, string repoPath);
    Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd);
    Task<R> StashAsync(string wd);
    Task<R> StashPopAsync(string name, string wd);

    // Git commands
    Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd);
    Task<R> FetchAsync(string wd);
    Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd);
    Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd);
    Task<R<CommitDiff>> GetPreviewMergeDiffAsync(string sha1, string sha2, string message, string wd);
    //Task<R<string>> GetFileTextAsync(string path, string wd);

    Task<R> PushBranchAsync(string name, string wd);
    Task<R> PullCurrentBranchAsync(string wd);
    Task<R> PullBranchAsync(string name, string wd);
    Task<R> SwitchToAsync(Repo repo, string branchName);
    Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName);
    Task<R> RebaseBranchAsync(Repo repo, string branchName);
    Task<R> CherryPickAsync(string sha, string wd);
    Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<R> DeleteRemoteBranchAsync(string name, string wd);
    Task<R> UndoAllUncommittedChangesAsync(string wd);
    Task<R> UndoUncommittedFileAsync(string path, string wd);
    Task<R> CleanWorkingFolderAsync(string wd);
    Task<R> UndoCommitAsync(string id, int parent, string wd);
    Task<R> UncommitLastCommitAsync(string wd);
    Task<R> CloneAsync(string uri, string path, string wd);
    Task<R> InitRepoAsync(string path, string wd);
    Task<R<CommitDiff>> GetStashDiffAsync(string name, string wd);
    Task<R> StashDropAsync(string name, string wd);
    Task<R<string>> GetChangeLogAsync();
    Task<R> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd);
    Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd);
    Task<R> SwitchToCommitAsync(string commitId, string wd);

}

internal record ChangeEvent(DateTime TimeStamp);

