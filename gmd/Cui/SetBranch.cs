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

        dlg.AddOK(true, () =>
        {
            if (name.Text == "")
            {
                UI.ErrorMessage("Empty tag name");
                return false;
            }
            return true;
        });
        dlg.AddCancel();

        if (!dlg.Show(name))
        {
            return R.Error();
        }

        return name.Text;
    }
}


