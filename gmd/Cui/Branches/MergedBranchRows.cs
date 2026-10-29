using System.Globalization;
using gmd.Cui.Common;
using gmd.Cui.Worktrees;
using gmd.Server;

namespace gmd.Cui.Branches;

// The rows of Delete Merged Branches, kept apart from the dialog so they can be asserted without a
// driver, and what is said of it. One row per branch whose work is in main or an integration branch
// already, newest first, with where it is, where it went and who made its last commit, its name in its
// color in the log: a local branch with no branch on origin ('here'), and one origin has ('origin'),
// with its local branch ('both'), which Delete deletes on origin too, after asking:
//
//     Time              Branch              On      Merged into             Author        Subject
//     2024-10-15 12:03  feature/login       here    main, squash or rebase  Ann Lee       Add login form
//     2024-10-15 12:02  fix/typo ⌂          here    main                    Bo Ek         Fix the typo
//     2024-10-15 12:01  feature/search      origin  main                    Cy Ray        Add search
static class MergedBranchRows
{
    const int TimeWidth = 18; // 'yyyy-MM-dd HH:mm' and two spaces
    const int BranchWidth = 20;
    const int OnWidth = 8; // 'origin' and two spaces
    const int IntoWidth = 24; // 'main, squash or rebase' and two spaces
    const int AuthorWidth = 14;
    const int FixedWidth = TimeWidth + BranchWidth + OnWidth + IntoWidth + AuthorWidth;

    // Narrower than this and the subject column has no room to say anything
    public const int MinWidth = FixedWidth + 12;

    public const string NoneToDelete =
        "No merged branch to delete: no branch is merged into main, or squash or rebase merged on origin";

    public static Text Header(int width) =>
        Text.Dark("Time".Max(TimeWidth, true))
            .Dark("Branch".Max(BranchWidth, true))
            .Dark("On".Max(OnWidth, true))
            .Dark("Merged into".Max(IntoWidth, true))
            .Dark("Author".Max(AuthorWidth, true))
            .Dark("Subject".Max(Math.Max(0, width - FixedWidth), true))
            .ToText();

    public static Text Row(MergedBranch merged, Color branchColor, int width)
    {
        var time = merged.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).Max(TimeWidth, true);
        var marker =
            merged.IsCurrent ? " ●"
            : merged.WorktreePath != "" ? $" {WorktreeRows.WorktreeMarker}"
            : "";
        var name = merged.Name.Max(BranchWidth - 1 - marker.Length);
        var into = (merged.IsByContent ? $"{merged.IntoName}, squash or rebase" : merged.IntoName).Max(IntoWidth, true);
        var author = merged.Author.Max(AuthorWidth - 2).Max(AuthorWidth, true);
        var subject = merged.Subject.Max(Math.Max(0, width - FixedWidth), true);

        return Text.Dark(time)
            .Color(branchColor, name)
            .White(marker)
            .Dark(new string(' ', BranchWidth - name.Length - marker.Length))
            .Color(merged.IsOnOrigin ? Color.Yellow : Color.Dark, On(merged).Max(OnWidth, true))
            .Green(into)
            .Dark(author)
            .White(subject)
            .ToText();
    }

    // Where the branch is: here, on origin, or both, which Delete deletes it in
    static string On(MergedBranch merged) =>
        merged.IsLocal && merged.IsOnOrigin ? "both"
        : merged.IsOnOrigin ? "origin"
        : "here";

    // Whether Delete can act on it: git deletes no branch that is checked out, here or elsewhere
    public static bool CanDelete(MergedBranch merged) => !merged.IsCurrent && merged.WorktreePath == "";

    // What Delete does with the selected branch, or why it cannot
    public static string Reason(MergedBranch merged) =>
        merged.IsCurrent ? $"'{merged.Name}' is checked out: switch to another branch, and then delete it"
        : merged.WorktreePath != ""
            ? $"'{merged.Name}' is checked out in another worktree: remove that in Worktrees (w), with the branch"
        : merged.IsByContent
            ? $"Delete deletes '{merged.Name}' here: squash or rebase merged into '{merged.IntoName}' on origin"
        : merged.IsLocal && merged.IsOnOrigin
            ? $"Delete deletes '{merged.Name}' here and on origin, for everyone, after asking: its commits are in '{merged.IntoName}'"
        : merged.IsOnOrigin
            ? $"Delete deletes '{merged.RemoteName}' on origin, for everyone, after asking: its commits are in '{merged.IntoName}'"
        : $"Delete deletes '{merged.Name}' here: its commits are in '{merged.IntoName}'";

    // Asked before a branch is deleted on origin, which deletes it for everyone who works with origin
    public static string DeleteOnOriginQuestion(MergedBranch merged)
    {
        var also = merged.IsLocal ? $"\n  {merged.LocalName}, here" : "";
        return $"Delete '{merged.Name}' on origin?\n\n"
            + $"  {merged.RemoteName}{also}\n\n"
            + "This deletes it on origin, for everyone, not only here.\n"
            + $"Its commits are in '{merged.IntoName}' already, and Restore Deleted\n"
            + "Branch, in the Undo menu, pushes it back.";
    }

    // Asked before every branch listed that can be is deleted at once. Those on origin are deleted
    // there only when the user picks Also on Origin, rather than the plain answer.
    public static string DeleteAllQuestion(IReadOnlyList<MergedBranch> branches)
    {
        var deletable = branches.Where(CanDelete).ToList();
        var names = deletable.Take(MaxListed).Select(b => b.IsOnOrigin ? $"  {b.Name} (on origin)" : $"  {b.Name}");
        var more = deletable.Count > MaxListed ? $"\n  and {deletable.Count - MaxListed} more" : "";
        var left = branches.Count - deletable.Count;
        var leftText =
            left == 0
                ? ""
                : $"\n\n{Branches(left)} checked out {(left == 1 ? "is" : "are")} left as {(left == 1 ? "it is" : "they are")}.";
        var onOrigin = deletable.Count(b => b.IsOnOrigin);
        var onOriginText =
            onOrigin == 0 ? ""
            : onOrigin == deletable.Count
                ? $"\n\n{(onOrigin == 1 ? "It is" : "They are")} on origin: Delete on Origin deletes {(onOrigin == 1 ? "it" : "them")} there, for everyone."
            : $"\n\n{onOrigin} of them {(onOrigin == 1 ? "is" : "are")} on origin: Here Only leaves {(onOrigin == 1 ? "it" : "those")} as {(onOrigin == 1 ? "it is" : "they are")},\n"
                + $"Also on Origin deletes {(onOrigin == 1 ? "it" : "them")} there too, for everyone.";
        return $"Delete {Branches(deletable.Count, "merged")}?\n\n"
            + string.Join("\n", names)
            + more
            + leftText
            + onOriginText
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
