using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

record DeleteBranchResult(bool IsLocal, bool IsRemote, bool IsForce);

interface IDeleteBranchDlg
{
    Result<DeleteBranchResult> Show(string branchName, bool isLocal, bool isRemote);
}

class DeleteBranchDlg : IDeleteBranchDlg
{
    public Result<DeleteBranchResult> Show(string branchName, bool isLocal, bool isRemote)
    {
        var dlg = new UIDialog("Delete Branch", 44, 11);

        var (isLocalCheck, isRemoteCheck) = BranchSides.Add(dlg, "Delete", branchName, isLocal, isRemote);
        var isForceCheck = dlg.AddCheckBox(1, 4, "Force Delete", false);

        if (!dlg.ShowOkCancel())
            return new Error();

        return new DeleteBranchResult(isLocalCheck.Checked, isRemoteCheck.Checked, isForceCheck.Checked);
    }
}

// Which sides of a branch to act on, here and on origin, as the delete and the restore dialogs ask:
// the branch's name, and a box for each side, checked, and enabled only for a side there is
static class BranchSides
{
    public static (CheckBox Local, CheckBox Remote) Add(
        UIDialog dlg,
        string verb,
        string name,
        bool isLocal,
        bool isRemote
    )
    {
        dlg.AddLabel(1, 0, $"{verb}: {name}");

        var local = dlg.AddCheckBox(1, 2, $"{verb} Local", isLocal);
        local.Enabled = isLocal;
        var remote = dlg.AddCheckBox(1, 3, $"{verb} Remote", isRemote);
        remote.Enabled = isRemote;
        return (local, remote);
    }
}
