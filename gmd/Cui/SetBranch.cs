using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;

interface ISetBranchDlg
{
    R<string> Show();
}


class SetBranchDlg : ISetBranchDlg
{
    TextField nameField = null!;

    public R<string> Show()
    {
        Label infoLabel = Components.Label(1, 0, "Branch Name:");
        nameField = Components.TextField(1, 1, 25, "");
        var indicator = Components.TextIndicator(nameField);

        bool isOk = false;
        Button okButton = Buttons.OK(true, () =>
        {
            if (Name() == "")
            {
                UI.ErrorMessage("Empty tag name");
                return false;
            }
            isOk = true;
            return true;
        });

        var dialog = Components.Dialog("Set Branch Name", 29, 7, okButton, Buttons.Cancel());
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(infoLabel, indicator, nameField);

        nameField.SetFocus();
        UI.ShowCursor();
        UI.RunDialog(dialog);

        if (!isOk)
        {
            return R.Error();
        }

        return Name();
    }

    private string Name() => nameField!.Text.ToString()?.Trim() ?? "";

}


