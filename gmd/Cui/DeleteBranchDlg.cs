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
        Label infoLabel = Components.Label(1, 0, $"Delete: {branchName}");

        var isLocalCheck = Components.CheckBox("Delete Local", isLocal, 1, 2);
        isLocalCheck.Enabled = isLocal;
        var isRemoteCheck = Components.CheckBox("Delete Remote", isRemote, 1, 3);
        isRemoteCheck.Enabled = isRemote;
        var isForceCheck = Components.CheckBox("Force Delete", false, 1, 4);

        bool isOk = false;
        Button okButton = Buttons.OK(true, () =>
        {
            isOk = true;
            return true;
        });


        var dialog = Components.Dialog("Delete Branch", 44, 11, okButton, Buttons.Cancel());
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(infoLabel, isLocalCheck, isRemoteCheck, isForceCheck);

        UI.RunDialog(dialog);

        if (!isOk)
        {
            return R.Error();
        }

        return new DeleteBranchResult(isLocalCheck.Checked, isRemoteCheck.Checked, isForceCheck.Checked);
    }
}

