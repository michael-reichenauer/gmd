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
    Task<R<Repo>> GetRepoAsync(string path);

    // UpdateRepoStatusAsync returns the repo with new fresh git status ...
    Task<R<Repo>> UpdateRepoStatusAsync(Repo augRepo);

    Task<R> FetchAsync(string path);
    Task<R> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout, string wd);
    Task<R> CreateBranchFromBranchAsync(Repo augmentedRepo, string newBranchName, string sourceBranch, bool isCheckout, string wd);
    Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd);

    Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName);
    Task<R> UnresolveAmbiguityAsync(Repo augmentedRepo, string commitId);
    Task<R> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName);
    Task<R> PushMetaDataAsync(string wd);
    Task<R<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string branchName);
    Task<R> RebaseBranchAsync(Repo repo, string name);
    Task<R> SwitchToAsync(Repo repo, string branchName);
    Task<R> AddTagAsync(string name, string commitId, bool hasRemoteBranch, string wd);
    Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd);
    Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd);
}
