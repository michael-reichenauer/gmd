namespace gmd.Server;

// A branch whose work is in main or an integration branch already, which Delete Merged Branches lists:
// a local branch with no branch on origin, merged as git counts it, or squash merged or rebase merged
// on origin (Repo.MergedOnOrigin), whose commits went in as copies; or a branch origin still has, with
// its local branch if there is one, merged as git counts it into origin's main or integration branch
public record MergedBranch(
    string Name, // The local branch's, or origin's without the 'origin/' when there is no local one
    string LocalName, // "" when it is only on origin
    string RemoteName, // On origin, e.g. 'origin/feature', or "" when origin has no branch of it
    string IntoName, // The branch its work is in, e.g. 'main'
    bool IsByContent, // Squash merged or rebase merged on origin, rather than merged as git counts it
    string TipId,
    string Subject, // Of its last commit
    string Author, // Of its last commit
    DateTime Time, // Of its last commit
    bool IsCurrent, // Checked out here, which git does not delete
    string WorktreePath // The other worktree it is checked out in, which git does not delete either
)
{
    public bool IsLocal => LocalName != "";

    // Deleting it deletes it on origin too, for everyone
    public bool IsOnOrigin => RemoteName != "";
}
