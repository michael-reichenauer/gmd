using System.Globalization;
using gmd.Cui.Common;
using gmd.Cui.Worktrees;
using gmd.Server;

namespace gmd.Cui.Branches;

// The rows of Delete Merged Branches, kept apart from the dialog so they can be asserted without a
// driver, and what is said of it. One row per local branch with no branch on origin whose work is in
// main or an integration branch already, newest first, with where it went:
//
//     Time              Branch              Merged into               Subject
//     2024-10-15 12:03  feature/login       main, squash or rebase    Add login form
//     2024-10-15 12:01  fix/typo ⌂          main                      Fix the typo
static class MergedBranchRows
{
    const int TimeWidth = 18; // 'yyyy-MM-dd HH:mm' and two spaces
    const int BranchWidth = 22;
    const int IntoWidth = 26;
    const int FixedWidth = TimeWidth + BranchWidth + IntoWidth;

    // Narrower than this and the subject column has no room to say anything
    public const int MinWidth = FixedWidth + 20;

    public const string NoneToDelete =
        "No merged branch to delete: no local branch is merged into main, or squash or rebase merged on origin";

    public static Text Header(int width) =>
        Text.Dark("Time".Max(TimeWidth, true))
            .Dark("Branch".Max(BranchWidth, true))
            .Dark("Merged into".Max(IntoWidth, true))
            .Dark("Subject".Max(Math.Max(0, width - FixedWidth), true))
            .ToText();

    public static Text Row(MergedBranch merged, int width)
    {
        var time = merged.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).Max(TimeWidth, true);
        var marker =
            merged.IsCurrent ? " ●"
            : merged.WorktreePath != "" ? $" {WorktreeRows.WorktreeMarker}"
            : "";
        var name = merged.Name.Max(BranchWidth - 1 - marker.Length);
        var into = (merged.IsByContent ? $"{merged.IntoName}, squash or rebase" : merged.IntoName).Max(IntoWidth, true);
        var subject = merged.Subject.Max(Math.Max(0, width - FixedWidth), true);

        return Text.Dark(time)
            .Cyan(name)
            .White(marker)
            .Dark(new string(' ', BranchWidth - name.Length - marker.Length))
            .Green(into)
            .White(subject)
            .ToText();
    }

    // Whether Delete can act on it: git deletes no branch that is checked out, here or elsewhere
    public static bool CanDelete(MergedBranch merged) => !merged.IsCurrent && merged.WorktreePath == "";

    // What Delete does with the selected branch, or why it cannot
    public static string Reason(MergedBranch merged) =>
        merged.IsCurrent ? $"'{merged.Name}' is checked out: switch to another branch, and then delete it"
        : merged.WorktreePath != ""
            ? $"'{merged.Name}' is checked out in another worktree: remove that in Worktrees (w), with the branch"
        : merged.IsByContent
            ? $"Delete deletes '{merged.Name}' here: squash or rebase merged into '{merged.IntoName}' on origin"
        : $"Delete deletes '{merged.Name}' here: its commits are in '{merged.IntoName}'";

    // Asked before every branch listed that can be is deleted at once
    public static string DeleteAllQuestion(IReadOnlyList<MergedBranch> branches)
    {
        var deletable = branches.Where(CanDelete).ToList();
        var names = deletable.Take(MaxListed).Select(b => $"  {b.Name}");
        var more = deletable.Count > MaxListed ? $"\n  and {deletable.Count - MaxListed} more" : "";
        var left = branches.Count - deletable.Count;
        var leftText =
            left == 0
                ? ""
                : $"\n\n{Branches(left)} checked out {(left == 1 ? "is" : "are")} left as {(left == 1 ? "it is" : "they are")}.";
        return $"Delete {Branches(deletable.Count, "merged")}?\n\n"
            + string.Join("\n", names)
            + more
            + leftText
            + "\n\nTheir work is in main or an integration branch already.\n"
            + "Restore Deleted Branch, in the Undo menu, brings each back.";
    }

    // What the status line says once they are deleted
    public static string Deleted(int count) =>
        $"Deleted {Branches(count, "merged")}: Restore Deleted Branch in the Undo menu brings them back";

    const int MaxListed = 10;

    static string Branches(int count, string kind = "") =>
        (count, kind) switch
        {
            (1, "") => "1 branch",
            (1, _) => $"1 {kind} branch",
            (_, "") => $"{count} branches",
            _ => $"{count} {kind} branches",
        };
}
