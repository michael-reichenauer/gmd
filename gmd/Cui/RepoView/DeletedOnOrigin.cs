using gmd.Server;

namespace gmd.Cui.RepoView;

// What is said of a local branch whose branch on origin was deleted, e.g. by GitHub once its pull
// request was merged, and which a fetch then pruned (Branch.GoneRemoteName). It was pushed, so it is
// no branch to publish, as one never pushed is: a push makes it again on origin, for everyone. Plain
// functions, so they are testable without a view.
static class DeletedOnOrigin
{
    public static bool IsGone(Branch branch) => branch.GoneRemoteName != "";

    // The branch it was squash merged or rebase merged into on origin, e.g. 'main', or "" when it is
    // not known to be (Repo.MergedOnOrigin)
    public static string MergedInto(Repo repo, Branch branch) =>
        repo.MergedOnOrigin.TryGetValue(branch.Name, out var into) ? into : "";

    // Why 'p' does not push the current branch, which it would publish if it had never been pushed
    public static string WhyNoPush(Repo repo, Branch branch) =>
        MergedInto(repo, branch) is var into && into != ""
            ? $"'{branch.NiceNameUnique}' was merged into '{into}' on origin, and deleted there: nothing to push"
            : $"'{branch.NiceNameUnique}' was deleted on origin, e.g. its pull request was merged: "
                + "Push in its branch menu puts it back";

    // Asked before the branch is pushed again
    public static string PushAgainQuestion(Repo repo, Branch branch) =>
        $"Push '{branch.NiceNameUnique}' to origin again?\n\n"
        + (
            MergedInto(repo, branch) is var into && into != ""
                ? $"'{branch.GoneRemoteName}' was merged into '{into}' on origin, by a squash\n"
                    + "or a rebase, and deleted there. Pushing makes it again, for everyone."
                : $"'{branch.GoneRemoteName}' was deleted on origin, e.g. when its pull\n"
                    + "request was merged. Pushing makes it again there, for everyone."
        );
}
