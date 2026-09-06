using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

// The rows of the worktrees dialog and what can be done with each worktree, kept apart from the
// dialog so they can be asserted without a driver. One row per worktree:
//
//     Kind    Branch                Changes State    Merged    Path
//   ● main    main                   -                         ┅/home/me/repo
//     linked  feature/login         ©2      in use   unmerged  ┅/home/me/repo-feature
//
// The dot marks the worktree gmd is running in. 'in use' is a locked worktree (Claude Code locks
// the one a session runs in), 'missing' one whose folder is gone. The path keeps its end, which is
// what tells worktrees apart.
static class WorktreeRows
{
    const int MarkerWidth = 2;
    const int KindWidth = 8;
    const int BranchWidth = 22;
    const int ChangesWidth = 8;
    const int StateWidth = 9;
    const int MergedWidth = 10;
    const int FixedWidth = MarkerWidth + KindWidth + BranchWidth + ChangesWidth + StateWidth + MergedWidth;

    // Narrower than this and the path column has no room to say anything
    public const int MinWidth = FixedWidth + 12;

    public static Text Header(int width) =>
        Text.Dark(new string(' ', MarkerWidth))
            .Dark("Kind".Max(KindWidth, true))
            .Dark("Branch".Max(BranchWidth, true))
            .Dark("Changes".Max(ChangesWidth, true))
            .Dark("State".Max(StateWidth, true))
            .Dark("Merged".Max(MergedWidth, true))
            .Dark("Path".Max(Math.Max(0, width - FixedWidth), true))
            .ToText();

    public static Text Row(Worktree w, Color branchColor, bool isUnmerged, int width)
    {
        var text = Text.White(w.IsCurrent ? "● " : "  ");
        text.Dark((w.IsMain ? "main" : "linked").Max(KindWidth, true));

        if (w.IsDetached)
            text.Dark($"(detached {Sid(w.HeadId)})".Max(BranchWidth - 1, true) + " ");
        else
            text.Color(branchColor, w.Branch.Max(BranchWidth - 1, true) + " ");

        if (w.ChangesCount > 0)
            text.Yellow($"©{w.ChangesCount}".Max(ChangesWidth, true));
        else if (w.ChangesCount == 0)
            text.Dark(" -".Max(ChangesWidth, true));
        else
            text.Dark("".Max(ChangesWidth, true));

        if (w.IsLocked)
            text.Cyan("in use".Max(StateWidth, true));
        else if (w.IsPrunable)
            text.Red("missing".Max(StateWidth, true));
        else
            text.Dark("".Max(StateWidth, true));

        // Merged says whether the branch is safe to delete along with the worktree, which is a
        // question only for a linked worktree holding a branch
        if (w.IsMain || w.IsDetached)
            text.Dark("".Max(MergedWidth, true));
        else if (isUnmerged)
            text.Yellow("unmerged".Max(MergedWidth, true));
        else
            text.Dark("merged".Max(MergedWidth, true));

        text.White(ShortPath(w.Path, Math.Max(0, width - FixedWidth)));
        return text.ToText();
    }

    // What the row cannot show: why a worktree is in use or missing
    public static string Reason(Worktree w) =>
        w.IsLocked ? $"In use: {(w.LockReason != "" ? w.LockReason : "locked")}"
        : w.IsPrunable ? $"Missing: {w.PruneReason}"
        : "";

    // The one gmd is in is already open, and a folder that is gone cannot be opened
    public static bool CanOpen(Worktree w) => !w.IsCurrent && !w.IsPrunable;

    // The main worktree is the repository itself, the current one is where gmd is running, a
    // locked one is in use by whatever locked it, and a missing one is what prune is for
    public static bool CanRemove(Worktree w) => !w.IsMain && !w.IsCurrent && !w.IsLocked && !w.IsPrunable;

    public static bool CanPrune(IEnumerable<Worktree> worktrees) => worktrees.Any(w => w.IsPrunable);

    static string Sid(string id) => id.Length >= 6 ? id[..6] : id;

    static string ShortPath(string path, int width)
    {
        if (width <= 1)
            return "";
        return path.Length <= width ? path : $"┅{path[^(width - 1)..]}";
    }
}
