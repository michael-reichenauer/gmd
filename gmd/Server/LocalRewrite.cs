namespace gmd.Server;

// A local branch rewritten here after it was pushed, e.g. its commits squashed, amended or rebased,
// while origin still has the version it replaced: the mirror of a RemoteRewrite. The two have
// diverged, which looks like new commits on both sides, but origin has nothing new: a pull would
// merge the old version back in, putting every change in twice, or rebase the rewrite onto it,
// which undoes it. What the branch needs is a force push, which replaces the old version on origin.
public record LocalRewrite(
    string BranchName, // The local branch
    string RemoteName, // Its remote branch, e.g. 'origin/dev'
    string LocalTipId, // Where the local branch is, in git, i.e. never the uncommitted row
    string OldTipId, // What origin has, which is where the branch was before the rewrite
    StepKind Kind, // What moved the branch off it, e.g. a squash or a rebase, Other when not known
    string Name, // What that was of, e.g. the subject of an amend, or ""
    int NewCount, // The branch's commits origin has not: the new version, and any made on it since
    IReadOnlyList<string> OldIds // origin's commits the branch no longer has, newest first: the old version
)
{
    public override string ToString() =>
        $"{BranchName}: {Kind} off {OldTipId.Sid()} -> {LocalTipId.Sid()}, new {NewCount}, old {OldIds.Count}";
}
