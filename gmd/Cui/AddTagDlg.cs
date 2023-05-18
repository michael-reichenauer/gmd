using gmd.Cui.Common;

namespace gmd.Cui;


interface IAddTagDlg
{
    R<string> Show();
}

class AddTagDlg : IAddTagDlg
{
    public R<string> Show()
    {
        var dlg = new UIDialog("Add Tag", 29, 7);
        dlg.AddLabel(1, 0, "Tag Name:");
        var name = dlg.AddTextField(1, 1, 25);

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


