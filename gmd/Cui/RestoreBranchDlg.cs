using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

record RestoreBranchResult(bool IsLocal, bool IsRemote);

interface IRestoreBranchDlg
{
    Result<RestoreBranchResult> Show(DeletedBranch deleted);
}

// Which sides of a deleted branch to bring back, as the delete dialog asked which to delete. Asked
// only for a branch deleted on origin, since restoring that is a push.
class RestoreBranchDlg : IRestoreBranchDlg
{
    public Result<RestoreBranchResult> Show(DeletedBranch deleted)
    {
        var dlg = new UIDialog("Restore Branch", 44, 10);

        var (isLocalCheck, isRemoteCheck) = BranchSides.Add(
            dlg,
            "Restore",
            deleted.Name,
            deleted.IsLocal,
            deleted.IsRemote
        );

        if (!dlg.ShowOkCancel())
            return new Error();

        return new RestoreBranchResult(isLocalCheck.Checked, isRemoteCheck.Checked);
    }
}
