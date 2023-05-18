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
    public R<CreateBranchResult> Show(string branchName, string commitSid)
    {
        var from = commitSid != "" ? $"{branchName} at {commitSid}" : branchName;
        var title = commitSid != "" ? $"Create Branch at Commit" : "Create Branch";

        var dlg = new UIDialog(title, 44, 11);
        dlg.AddLabel(1, 0, $"From: {from}");
        var nameField = dlg.AddTextField(1, 2, 40);
        var isCheckout = dlg.AddCheckBox("Checkout", true, 1, 4);
        var isPush = dlg.AddCheckBox("Push", true, 1, 5);

        dlg.AddOK(true, () =>
        {
            if (nameField.GetText() == "")
            {
                UI.ErrorMessage("Empty branch name");
                return false;
            }
            return true;
        });

        dlg.AddCancel();

        if (!dlg.Show(nameField))
        {
            return R.Error();
        }

        return new CreateBranchResult(nameField.GetText(), isCheckout.Checked, isPush.Checked);
    }
}

