namespace gmd.Server.Private.Augmented;

// Takes back the last change of a branch, as its reflog names it, or what gmd recorded when the
// reflog would name it wrong (RepoConfig.UndoSteps), and pulls a branch whose remote branch a force
// push rewrote, which is undoing it there
interface IUndoService
{
    // Takes back the last change of a branch (Repo.UndoSteps), or a redo
    Task<Result> UndoStepAsync(Repo repo, UndoStep step);

    // Pulls a branch whose remote branch a force push rewrote (Repo.RemoteRewrites): its own commits
    // are moved onto the new version, rather than the two versions merged
    Task<Result> PullRewrittenAsync(Repo repo, RemoteRewrite rewrite);
}
