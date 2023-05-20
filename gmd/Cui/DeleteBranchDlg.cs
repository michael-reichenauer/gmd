using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

record DeleteBranchResult(bool IsLocal, bool IsRemote, bool IsForce);

interface IDeleteBranchDlg
{
    R<DeleteBranchResult> Show(string branchName, bool isLocal, bool isRemote);
}

class DeleteBranchDlg : IDeleteBranchDlg
{
    public R<DeleteBranchResult> Show(string branchName, bool isLocal, bool isRemote)
    {
        var dlg = new UIDialog("Delete Branch", 44, 11);

        dlg.AddLabel(1, 0, $"Delete: {branchName}");

        var isLocalCheck = dlg.AddCheckBox(1, 2, "Delete Local", isLocal);
        isLocalCheck.Enabled = isLocal;
        var isRemoteCheck = dlg.AddCheckBox(1, 3, "Delete Remote", isRemote);
        isRemoteCheck.Enabled = isRemote;
        var isForceCheck = dlg.AddCheckBox(1, 4, "Force Delete", false);

        if (!dlg.ShowOkCancel()) return R.Error();

        return new DeleteBranchResult(isLocalCheck.Checked, isRemoteCheck.Checked, isForceCheck.Checked);
    }
}

