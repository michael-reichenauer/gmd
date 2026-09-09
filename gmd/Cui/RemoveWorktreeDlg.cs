using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

record RemoveWorktreeResult(bool IsDeleteBranch, bool IsForce);

interface IRemoveWorktreeDlg
{
    R<RemoveWorktreeResult> Show(Worktree worktree, bool isUnmerged);
}

// Removing a worktree, and whether to delete its branch with it — offered checked when the branch
// is merged, i.e. nothing would be lost, and unchecked but named as unmerged otherwise, so that
// checking it is a deliberate choice: the branch is then force deleted. Uncommitted changes in the
// worktree have to be discarded on purpose too, with the Force box.
class RemoveWorktreeDlg : IRemoveWorktreeDlg
{
    public R<RemoveWorktreeResult> Show(Worktree worktree, bool isUnmerged)
    {
        var dlg = new UIDialog("Remove Worktree", 64, 10);

        dlg.AddLabel(1, 0, $"Remove: {ShortPath(worktree.Path)}");

        var deleteBranch = dlg.AddCheckBox(
            1,
            2,
            DeleteBranchLabel(worktree, isUnmerged),
            IsDeleteBranchChecked(worktree, isUnmerged)
        );
        deleteBranch.Enabled = worktree.Branch != "";
        var force = dlg.AddCheckBox(1, 3, ForceLabel(worktree), false);
        force.Enabled = worktree.HasChanges;

        dlg.Validate(
            () => !worktree.HasChanges || force.Checked,
            $"The worktree has {worktree.ChangesCount} uncommitted changes,\ncheck Force to discard them."
        );

        if (!dlg.ShowOkCancel())
            return R.Error();

        return new RemoveWorktreeResult(deleteBranch.Checked && worktree.Branch != "", force.Checked);
    }

    internal static string DeleteBranchLabel(Worktree worktree, bool isUnmerged) =>
        worktree.Branch == "" ? "Delete branch too (none, detached)"
        : isUnmerged ? $"Delete branch '{worktree.Branch}' too (has unmerged commits)"
        : $"Delete branch '{worktree.Branch}' too";

    internal static bool IsDeleteBranchChecked(Worktree worktree, bool isUnmerged) =>
        worktree.Branch != "" && !isUnmerged;

    internal static string ForceLabel(Worktree worktree) =>
        worktree.HasChanges
            ? $"Force (discard {worktree.ChangesCount} uncommitted changes)"
            : "Force (no uncommitted changes to discard)";

    static string ShortPath(string path) => path.Length <= 50 ? path : $"┅{path[^50..]}";
}
