using gmd.Cui.Common;

namespace gmd.Cui;

record CreateBranchResult(string Name, bool IsCheckout, bool IsPush);

interface ICreateBranchDlg
{
    // With a name to start from, and whether to publish it by default
    Result<CreateBranchResult> Show(string branchName, string commitId, string name = "", bool isPublish = true);
}

class CreateBranchDlg : ICreateBranchDlg
{
    public Result<CreateBranchResult> Show(string branchName, string commitSid, string name = "", bool isPublish = true)
    {
        var from = commitSid != "" ? $"{branchName} at {commitSid}" : branchName;
        var title = commitSid != "" ? $"Create Branch at Commit" : "Create Branch";

        var dlg = new UIDialog(title, 44, 11);
        dlg.AddLabel(1, 0, $"From: {from}");
        var nameField = dlg.AddTextField(1, 2, 40, name);
        var isCheckout = dlg.AddCheckBox(1, 4, "Checkout", true);
        var isPublishBox = dlg.AddCheckBox(1, 5, "Publish", isPublish);

        dlg.Validate(() => nameField.Text != "", "Empty branch name");

        if (!dlg.ShowOkCancel(nameField))
            return new Error();

        return new CreateBranchResult(nameField.Text, isCheckout.Checked, isPublishBox.Checked);
    }
}
