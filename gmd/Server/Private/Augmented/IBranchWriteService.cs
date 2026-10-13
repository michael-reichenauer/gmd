namespace gmd.Server.Private.Augmented;

// The git commands that change which branches exist and where they point, and which branch a commit
// is on as the user chose it. They are the writes that need the augmented repo to work out what to
// actually run, i.e. which commit a created branch should remember it was branched from, whether a
// branch that git no longer has needs to be recreated first, and whether the local or the remote
// branch of a pair holds the tip to merge or rebase onto.
interface IBranchWriteService
{
    Task<Result> CreateBranchAsync(Repo repo, string newBranchName, bool isCheckout);
    Task<Result> CreateBranchFromBranchAsync(Repo repo, string newBranchName, string sourceBranch, bool isCheckout);
    Task<Result> CreateBranchFromCommitAsync(Repo repo, string newBranchName, string sha, bool isCheckout);
    Task<Result> RenameBranchAsync(string oldName, string newName, string wd);
    Task<Result> SwitchToAsync(Repo repo, string branchName);
    Task<Result<IReadOnlyList<Commit>>> MergeBranchAsync(Repo repo, string name);
    Task<Result<IReadOnlyList<Commit>>> MergeToBranchAsync(Repo repo, string targetName);
    Task<Result> RebaseBranchAsync(Repo repo, string name);

    // The branch of the ambiguous tip of a branch, of any commit, or none any more
    Task<Result> ResolveAmbiguityAsync(Repo repo, string branchName, string setHumanName);
    Task<Result> SetBranchManuallyAsync(Repo repo, string commitId, string setHumanName);
    Task<Result> UnresolveAmbiguityAsync(Repo repo, string commitId);
}
