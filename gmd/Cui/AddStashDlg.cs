using gmd.Cui.Common;

namespace gmd.Cui;


interface IAddStashDlg
{
    R<string> Show(string message);
}


class AddStashDlg : IAddStashDlg
{
    public R<string> Show(string message)
    {
        var dlg = new UIDialog("Stash", 44, 7);
        dlg.AddLabel(1, 0, "Stash Message:");
        var name = dlg.AddTextField(1, 1, 40, $"on '{message}'");

        if (!dlg.ShowOkCancel(name)) return R.Error();

        return name.Text.Trim();
    }
}

