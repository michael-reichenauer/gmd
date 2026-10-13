namespace gmd.Server.Private.Augmented;

// Deletes branches, and brings back the ones gmd deleted: git deletes a branch's reflog with the
// branch, so gmd records each one it deletes (RepoConfig.DeletedBranches), see DeletedBranchRecords
interface IDeletedBranchService
{
    // Deletes a branch on origin, here or both (a name is "" for a side not to delete), and records it
    Task<Result> DeleteBranchAsync(Repo repo, string localName, string remoteName, bool isForce);

    // The branches gmd deleted that can be brought back, newest first, and bringing one back
    Task<Result<IReadOnlyList<DeletedBranch>>> GetDeletedBranchesAsync(Repo repo);
    Task<Result> RestoreBranchAsync(Repo repo, DeletedBranch deleted, bool isLocal, bool isRemote);
}
