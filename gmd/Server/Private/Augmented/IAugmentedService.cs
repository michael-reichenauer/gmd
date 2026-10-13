namespace gmd.Server.Private.Augmented;

// The augmented repo: what git has, read with one git command per kind in parallel, plus what gmd
// infers from it and git does not record, i.e. which branch each commit is on and how the branches
// relate (BranchStructureService), and what the reflogs say (undo steps, remote rewrites).
interface IAugmentedService
{
    // The commits or branches may have changed, e.g. a commit, a fetch or a checkout
    public event Action<ChangeEvent> RepoChange;

    // The working folder may have changed, i.e. the status: files changed, added or removed
    public event Action<ChangeEvent> StatusChange;

    // The repo read afresh, and augmented, with no branches shown yet
    Task<Result<Repo>> GetRepoAsync(string path);

    // The repo with the status read afresh, the rest as it was
    Task<Result<Repo>> UpdateRepoStatusAsync(Repo augRepo);

    // The repo with the worktrees re-read: which exist, and the uncommitted changes of each
    Task<Result<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo);
    Task<Result> AddWorktreeAsync(string path, string branchName, bool isNewBranch, string startPoint, string wd);
    Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd);
    Task<Result> PruneWorktreesAsync(string wd);

    Task<Result> FetchAsync(string path);
    Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout);
    Task<Result> CreateBranchFromBranchAsync(Repo repo, string newBranchName, string sourceBranch, bool isCheckout);
    Task<Result> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout);
    Task<Result> RenameBranchAsync(string oldName, string newName, string wd);

    Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName);
    Task<Result> UnresolveAmbiguityAsync(Repo augmentedRepo, string commitId);
    Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName);
    Task<Result> PushMetaDataAsync(string wd);
    Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName);
    Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName);
    Task<Result> RebaseBranchAsync(Repo repo, string name);
    Task<Result> SwitchToAsync(Repo repo, string branchName);
    Task<Result> AddTagAsync(string name, string commitId, bool isPush, string wd);
    Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, bool isPush, string wd);
    Task<Result> RemoveTagAsync(string name, bool isOnOrigin, string wd);
    Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<Result> CommitFilesAsync(string message, bool isAmend, IReadOnlyList<string> paths, string wd);
    Task<Result> SquashCommitsAsync(Repo repo, string id1, string id2, string msg);

    // An older commit not pushed yet, given a new message and the changes in the paths, or taken out
    Task<Result> AmendOlderCommitAsync(Repo repo, string id, string message, IReadOnlyList<string> paths);
    Task<Result> DropCommitAsync(Repo repo, string id);
    Task<Result> UndoStepAsync(Repo repo, UndoStep step);
    Task<Result<IReadOnlyList<LostWork>>> GetLostWorkAsync(Repo repo);
    Task<Result> DeleteBranchAsync(Repo repo, string localName, string remoteName, bool isForce);
    Task<Result<IReadOnlyList<DeletedBranch>>> GetDeletedBranchesAsync(Repo repo);
    Task<Result> RestoreBranchAsync(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote);
    Task<Result> PullRewrittenAsync(Repo repo, RemoteRewrite rewrite);
}
