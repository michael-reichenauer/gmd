namespace gmd.Server;

// A remote branch rewritten by a force push since its local branch was built on it, e.g. a teammate
// rebased and pushed it, or gmd's Rebase pushed it and the rebase was then undone here. The local
// branch has the old version, origin the new one: a pull that merged the two would put every commit
// in twice, and a push of the merge would put the old version back on origin for everyone.
public record RemoteRewrite(
    string BranchName, // The local branch
    string RemoteName, // Its remote branch, e.g. 'origin/dev'
    string LocalTipId, // Where the local branch is, in git, i.e. never the uncommitted row
    string ForkPointId, // The old remote tip the local branch was built on
    string OldTipId, // What origin had before the rewrite, as last fetched
    string NewTipId, // What origin has now
    int OwnCount, // The local branch's own commits on top of the fork point, never on origin
    int OldCopyCount, // The commits of the old version the local branch has
    int NewCount, // The commits of the new version, which origin has and the local branch has not
    // The commits of the old version with no copy in the new one, by author and author time: what the
    // force push dropped, rather than rewrote
    IReadOnlyList<string> DroppedIds,
    bool IsByYou, // Rewritten by a push from this repository, rather than by a fetch of someone else's
    bool IsRestorable // Nothing came on top of the rewrite, so origin can be put back as it was
)
{
    public override string ToString() =>
        $"{RemoteName}: {OldTipId.Sid()} -> {NewTipId.Sid()}, own {OwnCount}, old {OldCopyCount}, dropped {DroppedIds.Count}";
}
