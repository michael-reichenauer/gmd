using gmd.Server;

namespace gmd.Cui.RepoView;

// What is said of a local branch whose branch on origin was deleted, e.g. by GitHub once its pull
// request was merged, and which a fetch then pruned (Branch.GoneRemoteName). It was pushed, so it is
// not said to be never pushed: Publish makes it again on origin, for everyone, and asks first. Plain
// functions, so they are testable without a view.
static class DeletedOnOrigin
{
    public static bool IsGone(Branch branch) => branch.GoneRemoteName != "";

    // The branch it was squash merged or rebase merged into on origin, e.g. 'main', or "" when it is
    // not known to be (Repo.MergedOnOrigin)
    public static string MergedInto(Repo repo, Branch branch) =>
        repo.MergedOnOrigin.TryGetValue(branch.Name, out var into) ? into : "";

    // Why 'p' does not push it, which would publish it again, as Publish in its branch menu does
    public static string WhyNoPush(Repo repo, Branch branch) =>
        MergedInto(repo, branch) is var into && into != ""
            ? $"'{branch.NiceNameUnique}' was merged into '{into}' on origin, and deleted there: nothing to push"
            : $"'{branch.NiceNameUnique}' was deleted on origin, e.g. its pull request was merged: "
                + "Publish in its branch menu puts it back";

    // Asked before the branch is published again
    public static string PublishAgainQuestion(Repo repo, Branch branch) =>
        $"Publish '{branch.NiceNameUnique}' to origin again?\n\n"
        + (
            MergedInto(repo, branch) is var into && into != ""
                ? $"'{branch.GoneRemoteName}' was merged into '{into}' on origin, by a squash\n"
                    + "or a rebase, and deleted there. Publishing makes it again, for everyone."
                : $"'{branch.GoneRemoteName}' was deleted on origin, e.g. when its pull\n"
                    + "request was merged. Publishing makes it again there, for everyone."
        );
}

// Which branches merged on origin have been told on the status line, so that each is told once, when
// a read first finds it (Repo.MergedOnOrigin): it looks unmerged, as git counts it, and is done with
class MergedOnOriginNotes
{
    readonly HashSet<(string Branch, string Tip)> told = [];

    // What to say of the branches of a shown repo not told yet, or null
    public string? Untold(Repo repo)
    {
        var untold = repo
            .MergedOnOrigin.OrderBy(m => m.Key)
            .Where(m => repo.BranchByName.TryGetValue(m.Key, out var b) && told.Add((m.Key, b.TipId)))
            .ToList();
        return untold.Count switch
        {
            0 => null,
            // The command first, which a status line cut short on a narrow terminal kept the least of
            1 =>
                $"Delete Merged Branches in the repo menu (⇧m) deletes '{untold[0].Key}', merged into '{untold[0].Value}' on origin",
            // Counted rather than named, since two long names leave the status line no room
            _ => $"Delete Merged Branches in the repo menu (⇧m) deletes the {untold.Count} branches merged on origin",
        };
    }
}
