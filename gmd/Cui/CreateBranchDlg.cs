using gmd.Cui.Common;
using Terminal.Gui;


namespace gmd.Cui;

record CreateBranchResult(string Name, bool IsCheckout, bool IsPush);
interface ICreateBranchDlg
{
    R<CreateBranchResult> Show(string branchName, string commitId);
}

class CreateBranchDlg : ICreateBranchDlg
{
    TextField? nameField;

    public R<CreateBranchResult> Show(string branchName, string commitSid)
    {
        var from = commitSid != "" ? $"{branchName} at {commitSid}" : branchName;
        var title = commitSid != "" ? $"Create Branch at Commit" : "Create Branch";

        Label infoLabel = Components.Label(1, 0, $"From: {from}");

        nameField = Components.TextField(1, 2, 40, "");
        var indicator = Components.TextIndicator(nameField);

        var isCheckout = Components.CheckBox("Checkout", true, 1, 4);
        var isPush = Components.CheckBox("Push", true, 1, 5);

        bool isOk = false;
        Button okButton = Buttons.OK(true, () =>
        {
            if (Name() == "")
            {
                UI.ErrorMessage("Empty branch name");
                return false;
            }
            isOk = true;
            return true;
        });


        var dialog = Components.Dialog(title, 44, 11, okButton, Buttons.Cancel());
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(infoLabel, nameField, indicator, isCheckout, isPush);

        nameField.SetFocus();
        UI.ShowCursor();
        UI.RunDialog(dialog);

        if (!isOk)
        {
            return R.Error();
        }

        return new CreateBranchResult(Name(), isCheckout.Checked, isPush.Checked);
    }

    private string Name() => nameField!.Text.ToString()?.Trim() ?? "";
}

