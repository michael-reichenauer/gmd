namespace gmd.Server;

// What took a line of work out of the history: the move of its branch or HEAD that left it behind
public enum LostBy
{
    Other,
    Reset, // Including Uncommit, and the resets a squash makes
    Rebase,
    Amend,
    Pull, // A pull that rebased
    Deleted, // The branch it was made on was deleted
    Detached, // It was made on a detached HEAD, which was then moved to a branch
}

// A line of work no branch, tag or stash reaches any more, which git still has because a reflog
// mentions it, by default for about 30 days: its tip, a lost commit no other lost commit was made
// on, and the lost commits under it, down to where it left the history that is still there.
public record LostWork(
    string TipId,
    IReadOnlyList<string> CommitIds, // The tip first, then the others, newest first
    string OldestId, // The oldest lost commit on the tip's first parents, for the diff of the line
    DateTime Time, // The tip's commit time
    string Subject, // The tip's
    string Author, // The tip's
    string BranchName, // The branch it was made on, as the reflog witnessed it, or "" when it does not say
    LostBy LostBy,
    // Every commit of it has a copy that is still in the history, by its author and author time, which
    // an amend, a rebase and a cherry pick keep: an older version of work that is there, rather than
    // work that is gone
    bool IsRewritten
)
{
    public override string ToString() => $"{TipId.Sid()} {Subject} ({BranchName}, {LostBy}, {CommitIds.Count})";
}
