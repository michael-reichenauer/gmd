using gmd.Cui.Common;
using Terminal.Gui;

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
        var nameField = dlg.AddTextField(1, 1, 25);

        dlg.AddOK(true, () =>
        {
            if (nameField.GetText() == "")
            {
                UI.ErrorMessage("Empty tag name");
                return false;
            }
            return true;
        });

        dlg.AddCancel();

        if (!dlg.Show(nameField))
        {
            return R.Error();
        }

        return nameField.GetText();
    }
}


