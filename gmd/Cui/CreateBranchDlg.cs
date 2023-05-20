using gmd.Cui.Common;

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
        var name = dlg.AddTextField(1, 2, 40);
        var isCheckout = dlg.AddCheckBox(1, 4, "Checkout", true);
        var isPush = dlg.AddCheckBox(1, 5, "Push", true);

        dlg.AddOK(true, () =>
        {
            if (name.Text == "")
            {
                UI.ErrorMessage("Empty branch name");
                return false;
            }
            return true;
        });

        dlg.AddCancel();

        if (!dlg.Show(name))
        {
            return R.Error();
        }

        return new CreateBranchResult(name.Text, isCheckout.Checked, isPush.Checked);
    }
}

