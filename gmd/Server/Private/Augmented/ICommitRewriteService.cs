namespace gmd.Server.Private.Augmented;

// Rewrites commits of the current branch that are not pushed yet, each rewrite recorded as one change
// for Undo (see IUndoService)
interface ICommitRewriteService
{
    // The commits from id1 down to id2 made one, with the commits on top of them picked back on it
    Task<Result> SquashCommitsAsync(Repo repo, string id1, string id2, string msg);

    // An older commit not pushed yet, given a new message and the changes in the paths, none for a
    // new message alone, or taken out of the branch
    Task<Result> AmendOlderCommitAsync(Repo repo, string id, string message, IReadOnlyList<string> paths);
    Task<Result> DropCommitAsync(Repo repo, string id);

    // A commit of the current branch moved past the next one up the branch, or down it, i.e. the two
    // swapped, and the newer ones replayed after them
    Task<Result> MoveCommitAsync(Repo repo, string id, bool isUp);
}
