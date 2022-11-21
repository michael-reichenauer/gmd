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

    // GetRepoAsync returns the updated augmented repo with git status ...
    Task<R<Repo>> UpdateStatusRepoAsync(Repo augRepo);
}
