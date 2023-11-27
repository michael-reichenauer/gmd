using gmd.Cui.Common;

namespace gmd.Cui;


interface IAddStashDlg
{
    R<string> Show();
}


class AddStashDlg : IAddStashDlg
{
    public R<string> Show()
    {
        var dlg = new UIDialog("Stash", 44, 7);
        dlg.AddLabel(1, 0, "Stash Message:");
        var name = dlg.AddTextField(1, 1, 40);

        if (!dlg.ShowOkCancel(name)) return R.Error();

        return name.Text.Trim();
    }
}

