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

    // Fetches the branches and the metadata, and pushes the metadata, i.e. the user's choices of
    // which branch a commit is on, see MetaDataService
    Task<Result> FetchAsync(string path);
    Task<Result> PushMetaDataAsync(string wd);

    // The lines of work no branch, tag or stash has any more, which the reflogs still mention
    Task<Result<IReadOnlyList<LostWork>>> GetLostWorkAsync(Repo repo);
}
