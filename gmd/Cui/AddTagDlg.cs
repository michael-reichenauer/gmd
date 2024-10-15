using gmd.Cui.Common;

namespace gmd.Cui;

record TagInfo(string name, string message);

interface IAddTagDlg
{
    R<TagInfo> Show();
}

class AddTagDlg : IAddTagDlg
{
    public R<TagInfo> Show()
    {
        var dlg = new UIDialog("Add Tag", 60, 13);

        dlg.AddLabel(1, 0, "Name:");
        var name = dlg.AddTextField(1, 1, 25);

        dlg.AddLabel(1, 3, "Message:");
        var message = dlg.AddMultiLineInputView(1, 5, 56, 4, "");

        dlg.Validate(() => name.Text != "", "Empty tag name");

        if (!dlg.ShowOkCancel(name)) return R.Error();

        return new TagInfo(name.Text, message.Text);
    }
}
