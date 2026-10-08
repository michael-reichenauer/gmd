using gmd.Common.Spelling;
using gmd.Cui.Common;

namespace gmd.Cui;

record TagInfo(string name, string message, bool isPush);

interface IAddTagDlg
{
    Result<TagInfo> Show(bool canPush);
}

class AddTagDlg : IAddTagDlg
{
    readonly ISpellChecker spellChecker;

    internal AddTagDlg(ISpellChecker spellChecker)
    {
        this.spellChecker = spellChecker;
    }

    // A tag on a commit of a branch with a remote can be pushed with it, which the dialog says and
    // lets be left out: adding a tag pushed it unasked, where every other push is a choice made in
    // view. Checked, as Create Branch's Publish is, since there is no other way to push it later.
    public Result<TagInfo> Show(bool canPush)
    {
        var dlg = new UIDialog("Add Tag", 60, canPush ? 15 : 13);

        dlg.AddLabel(1, 0, "Name:");
        var name = dlg.AddTextField(1, 1, 25);

        dlg.AddLabel(1, 3, "Message:");
        var message = dlg.AddMultiLineInputView(1, 5, 56, 4, "", spellChecker);

        var push = canPush ? dlg.AddCheckBox(1, 10, "Push to origin", true) : null;

        dlg.Validate(() => name.Text != "", "Empty tag name");

        if (!dlg.ShowOkCancel(name))
            return new Error();

        return new TagInfo(name.Text, message.Text, push?.Checked == true);
    }
}
