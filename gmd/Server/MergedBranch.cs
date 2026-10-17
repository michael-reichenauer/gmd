namespace gmd.Server;

// A local branch whose work is in main or an integration branch already, with no branch on origin,
// which Delete Merged Branches lists: merged as git counts it, or squash merged or rebase merged on
// origin (Repo.MergedOnOrigin), whose commits went in as copies
public record MergedBranch(
    string Name,
    string IntoName, // The branch its work is in, e.g. 'main'
    bool IsByContent, // Squash merged or rebase merged on origin, rather than merged as git counts it
    string TipId,
    string Subject, // Of its last commit
    DateTime Time, // Of its last commit
    bool IsCurrent, // Checked out here, which git does not delete
    string WorktreePath // The other worktree it is checked out in, which git does not delete either
);
