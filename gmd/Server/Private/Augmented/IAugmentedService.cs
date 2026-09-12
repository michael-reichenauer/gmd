namespace gmd.Server.Private.Augmented;

// AugmentedRepoService returns augmented repos of git repo information, The augmentations
// adds information not available in git directly, but can be inferred by parsing the
// git information.
// Examples of augmentation is which branch a commits belongs to and the hierarchical structure
// of branches.
interface IAugmentedService
{
    // RepoChange events when git repo changes like new commit, new branches, ...
    public event Action<ChangeEvent> RepoChange;

    // StatusChange events when working folder changes like changed, added or removed files.
    public event Action<ChangeEvent> StatusChange;

    // GetRepoAsync returns an augmented repo based on new git info like branches, commits, ...
    Task<Result<Repo>> GetRepoAsync(string path);

    // UpdateRepoStatusAsync returns the repo with new fresh git status ...
    Task<Result<Repo>> UpdateRepoStatusAsync(Repo augRepo);

    // The repo with the worktrees re-read: which exist, and the uncommitted changes of each
    Task<Result<Repo>> GetUpdatedWorktreesRepoAsync(Repo repo);
    Task<Result> AddWorktreeAsync(string path, string branchName, bool isNewBranch, string startPoint, string wd);
    Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd);
    Task<Result> PruneWorktreesAsync(string wd);

    Task<Result> FetchAsync(string path);
    Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd);
    Task<Result> CreateBranchFromBranchAsync(
        Repo augmentedRepo,
        string newBranchName,
        string sourceBranch,
        bool isCheckout,
        string wd
    );
    Task<Result> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd);
    Task<Result> RenameBranchAsync(string oldName, string newName, string wd);

    Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName);
    Task<Result> UnresolveAmbiguityAsync(Repo augmentedRepo, string commitId);
    Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName);
    Task<Result> PushMetaDataAsync(string wd);
    Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName);
    Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName);
    Task<Result> RebaseBranchAsync(Repo repo, string name);
    Task<Result> SwitchToAsync(Repo repo, string branchName);
    Task<Result> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd);
    Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, bool hasRemoteBranch, string wd);
    Task<Result> RemoveTagAsync(string name, bool hasRemoteBranch, string wd);
    Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<Result> SquashCommits(Repo repo, string id1, string id2, string msg);
}
