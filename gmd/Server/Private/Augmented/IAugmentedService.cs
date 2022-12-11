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
    Task<R> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout, string wd);

    Task<R> ResolveAmbiguityAsync(Repo repo, string branchName, string setDisplayName);
    Task<R> UnresolveAmbiguityAsync(Repo augmentedRepo, string commitId);
    Task<R> PushMetaDataAsync(string wd);
    Task<R> MergeBranchAsync(Repo repo, string branchName, string wd);
}
