namespace gmd.Server;

// The kind of change a move of a branch was, as the branch's reflog says, which is what Undo is
// named after and decides how the change is taken back (see UndoMode)
public enum StepKind
{
    Other,
    Commit,
    Amend,
    Merge,
    CherryPick,
    Revert,
    Pull,
    Rebase,
    Uncommit,
    Reset,
    Squash,
    Drop, // A commit taken out of the branch, recorded by gmd, since the reflog says rebase
    Created, // The branch was made here, so there is nothing before it to go back to
    Renamed, // The branch was renamed or copied, which moved nothing
}

// How the current branch is moved back, chosen so that nothing uncommitted can be lost. A branch
// that is not checked out has no files, and is just moved.
public enum UndoMode
{
    Mixed, // The branch and the index move, the files stay: a commit's changes come back as uncommitted
    Keep, // The files move too, and git refuses rather than overwrite an uncommitted change
    KeepWhenClean, // Keep with no uncommitted changes, else Mixed
}

// The last change of a local branch, which Undo takes back: the branch is at TipId, and goes back to
// TargetId. Name is what the change was of, a subject or a merged branch, for the label. A redo is
// the undo of an undo, i.e. the change it names comes back.
public record UndoStep(
    string BranchName,
    StepKind Kind,
    string Name,
    string TipId,
    string TargetId,
    bool IsPushed, // The tip is on the remote branch, so undoing it locally leaves it there
    bool IsRedo
)
{
    public UndoMode Mode =>
        Kind switch
        {
            StepKind.Commit or StepKind.Amend => UndoMode.Mixed,
            StepKind.Reset or StepKind.Uncommit => UndoMode.KeepWhenClean,
            _ => UndoMode.Keep,
        };

    public override string ToString() =>
        $"{BranchName}: {(IsRedo ? "redo" : "undo")} {Kind} '{Name}' {TipId.Sid()} -> {TargetId.Sid()}";
}
