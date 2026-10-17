namespace gmd.Cui.Common;

// Whether a dialog or question that rewrites commits offers to push the branch with force when done,
// as it does when some of them are on origin, and whether that is ticked as it opens: not when the
// rewrite was asked about first, on a branch others build on or of someone else's commits, which 'p'
// pushes then (CommitRewrite.ForcePushOf)
enum ForcePush
{
    NotOffered,
    Offered,
    Ticked,
}
