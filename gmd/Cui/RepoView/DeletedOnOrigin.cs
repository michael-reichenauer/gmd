using gmd.Server;

namespace gmd.Cui.RepoView;

// What is said of a local branch whose branch on origin was deleted, e.g. by GitHub once its pull
// request was merged, and which a fetch then pruned (Branch.GoneRemoteName). It was pushed, so it is
// no branch to publish, as one never pushed is: a push makes it again on origin, for everyone. Plain
// functions, so they are testable without a view.
static class DeletedOnOrigin
{
    public static bool IsGone(Branch branch) => branch.GoneRemoteName != "";

    // Why 'p' does not push the current branch, which it would publish if it had never been pushed
    public static string WhyNoPush(Branch branch) =>
        $"'{branch.NiceNameUnique}' was deleted on origin, e.g. its pull request was merged: "
        + "Push in its branch menu puts it back";

    // Asked before the branch is pushed again
    public static string PushAgainQuestion(Branch branch) =>
        $"Push '{branch.NiceNameUnique}' to origin again?\n\n"
        + $"'{branch.GoneRemoteName}' was deleted on origin, e.g. when its pull\n"
        + "request was merged. Pushing makes it again there, for everyone.";
}
