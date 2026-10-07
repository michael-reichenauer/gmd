using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

enum WorktreeAction
{
    Open,
    Add,
    Remove,
    Prune,
    CopyPath,
}

// What the user picked in the worktrees dialog: an action, and the worktree it is for
record WorktreeChoice(WorktreeAction Action, Worktree Worktree);

interface IWorktreesDlg
{
    Result<WorktreeChoice> Show(Repo repo, IReadOnlyList<Worktree> worktrees, int selectedIndex = 0);
}

// The list of the repository's worktrees, one row each, see WorktreeRows, in a ListDlg: it returns
// what was picked and closes, and the command acts on it and shows it again.
class WorktreesDlg : IWorktreesDlg
{
    const int maxWidth = 100;

    readonly IBranchColorService branchColorService;

    public WorktreesDlg(IBranchColorService branchColorService)
    {
        this.branchColorService = branchColorService;
    }

    public Result<WorktreeChoice> Show(Repo repo, IReadOnlyList<Worktree> worktrees, int selectedIndex = 0) =>
        new ListDlg<Worktree, WorktreeChoice>(
            "Worktrees",
            WorktreeRows.MinWidth,
            maxWidth,
            WorktreeRows.Header,
            (w, width) => WorktreeRows.Row(w, ColorOf(repo, w), IsUnmerged(repo, w), width),
            WorktreeRows.Reason
        )
            .Action("_Open", w => new WorktreeChoice(WorktreeAction.Open, w), WorktreeRows.CanOpen)
            .Action("_Add ...", w => new WorktreeChoice(WorktreeAction.Add, w))
            .Action("_Remove ...", w => new WorktreeChoice(WorktreeAction.Remove, w), WorktreeRows.CanRemove)
            .Action("_Prune", w => new WorktreeChoice(WorktreeAction.Prune, w), _ => WorktreeRows.CanPrune(worktrees))
            .Action("_Copy Path", w => new WorktreeChoice(WorktreeAction.CopyPath, w))
            .Show(worktrees, selectedIndex);

    Color ColorOf(Repo repo, Worktree w) =>
        w.Branch != "" && repo.BranchByName.TryGetValue(w.Branch, out var branch)
            ? branchColorService.GetColor(repo, branch)
            : Color.White;

    static bool IsUnmerged(Repo repo, Worktree w) =>
        w.Branch != "" && repo.BranchByName.TryGetValue(w.Branch, out var branch) && repo.HasUnmergedCommits(branch);
}
