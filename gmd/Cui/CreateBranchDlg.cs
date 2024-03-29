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
        var isPublish = dlg.AddCheckBox(1, 5, "Publish", true);

        dlg.Validate(() => name.Text != "", "Empty branch name");

        if (!dlg.ShowOkCancel(name)) return R.Error();

        return new CreateBranchResult(name.Text, isCheckout.Checked, isPublish.Checked);
    }
}

