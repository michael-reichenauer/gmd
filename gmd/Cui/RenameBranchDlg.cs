using gmd.Cui.Common;

namespace gmd.Cui;

interface IRenameBranchDlg
{
    Result<string> Show(string branchName, bool hasRemote, IReadOnlyList<string> existingNames);
}

class RenameBranchDlg : IRenameBranchDlg
{
    public Result<string> Show(string branchName, bool hasRemote, IReadOnlyList<string> existingNames)
    {
        var dlg = new UIDialog("Rename Branch", 60, 9);
        dlg.AddLabel(1, 0, $"From: {branchName}");
        var name = dlg.AddTextField(1, 2, 56, branchName);
        if (hasRemote)
        { // The remote branch is renamed as well, which deletes the old remote branch, so say so
            dlg.AddLabel(1, 4, $"Also renames origin/{branchName}");
        }

        dlg.Validate(() => name.Text != "", "Empty branch name");
        dlg.Validate(() => name.Text != branchName, "Same branch name");
        dlg.ValidateRefName(() => name.Text, "branch");
        dlg.Validate(
            () => !existingNames.Contains(name.Text) && !existingNames.Contains($"origin/{name.Text}"),
            "A branch of that name exists already"
        );

        if (!dlg.ShowOkCancel(name))
            return new Error();

        return name.Text;
    }
}
