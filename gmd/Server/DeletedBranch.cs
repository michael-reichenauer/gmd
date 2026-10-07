namespace gmd.Server;

// A branch gmd deleted that can be brought back, which gmd recorded when it deleted it, since git
// deletes a branch's reflog with the branch (RepoConfig.DeletedBranches): the local branch Name at
// TipId, and the remote branch RemoteName at RemoteTipId. A tip is "" for a side that was not deleted
// or that cannot be restored now. A local branch that tracked a remote branch, kept or deleted with
// it, has its RemoteName, and tracks it again once restored. A remote branch no local branch tracked
// has the Name its local branch would have.
public record DeletedBranch(
    string Name,
    string TipId,
    string RemoteName,
    string RemoteTipId,
    string Subject, // The local tip's, or the remote one's when only that can be restored
    DateTime Time // When it was deleted
)
{
    public bool IsLocal => TipId != "";
    public bool IsRemote => RemoteTipId != "";

    public override string ToString() =>
        $"{Name} {(IsLocal ? TipId.Sid() : "-")} {(IsRemote ? $"{RemoteName} {RemoteTipId.Sid()}" : "-")}";
}
