namespace gmd.Server;

// A branch gmd deleted that can be brought back, which gmd recorded when it deleted it, since git
// deletes a branch's reflog with the branch (RepoConfig.DeletedBranches): the local branch Name at
// TipId, and the remote branch RemoteName at RemoteTipId. A tip is "" for a side that was not deleted
// or that cannot be restored now. A local branch that tracked a remote branch, kept or deleted with
// it, has its RemoteName, and tracks it again once restored, if it is there.
public record DeletedBranch(
    string Name,
    string TipId,
    string RemoteName,
    string RemoteTipId,
    string Subject, // The tip's, the local one's when both were deleted
    DateTime Time // When it was deleted
)
{
    public bool IsLocal => TipId != "";
    public bool IsRemote => RemoteTipId != "";

    public override string ToString() =>
        $"{Name} {(IsLocal ? TipId.Sid() : "-")} {(IsRemote ? $"{RemoteName} {RemoteTipId.Sid()}" : "-")}";
}
