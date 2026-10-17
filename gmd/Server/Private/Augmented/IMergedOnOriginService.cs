namespace gmd.Server.Private.Augmented;

// Which local branches deleted on origin were merged there all the same, though git does not count
// them merged: squash merged or rebase merged, e.g. by a pull request, so that what went in are
// copies of their commits (Repo.MergedOnOrigin). Told by content, a git command per branch, so it is
// read after the repo is shown, and what is known is kept for the session.
interface IMergedOnOriginService
{
    // The repo with what is known already, which takes no git command
    Repo WithKnown(Repo repo);

    // Whether a branch is not known yet, i.e. GetUpdatedRepoAsync has git commands to run
    bool HasUnknown(Repo repo);

    // The repo with every branch known, asking git of the ones not known yet
    Task<Result<Repo>> GetUpdatedRepoAsync(Repo repo);
}
