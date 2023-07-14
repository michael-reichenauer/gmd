using gmd.Cui.Common;

namespace gmd.Cui;

interface ISetBranchDlg
{
    R<string> Show(string niceName, IReadOnlyList<string> possibleBranches);
}


class SetBranchDlg : ISetBranchDlg
{
    public R<string> Show(string niceName, IReadOnlyList<string> possibleBranches)
    {
        var dlg = new UIDialog("Set Branch Name", 50, 15, null, o => o.Y = 0);

        dlg.AddLabel(1, 0, "Branch Name:");
        var name = dlg.AddComboTextField(1, 1, 46, 10, () => possibleBranches, niceName);

        dlg.Validate(() => name.Text != "", "Empty branch name");

        if (!dlg.ShowOkCancel(name)) return R.Error();

        return name.Text;
    }
}


