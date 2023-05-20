using gmd.Cui.Common;

namespace gmd.Cui;

interface ISetBranchDlg
{
    R<string> Show();
}


class SetBranchDlg : ISetBranchDlg
{
    public R<string> Show()
    {
        var dlg = new UIDialog("Set Branch Name", 29, 7);

        dlg.AddLabel(1, 0, "Branch Name:");
        var name = dlg.AddTextField(1, 1, 25, "");

        dlg.Validate(() => name.Text != "", "Empty branch name");

        if (!dlg.ShowOkCancel(name)) return R.Error();

        return name.Text;
    }
}


