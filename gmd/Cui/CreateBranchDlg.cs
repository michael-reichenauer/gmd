using Terminal.Gui;

namespace gmd.Cui;

interface ICreateBranchDlg
{
    R<string> Show(string branchName, string commitId);
}

class Buttons
{
    internal static Button Cancel(bool isDefault = false, Func<bool>? clicked = null)
    {
        Button button = new Button("Cancel", isDefault) { ColorScheme = ColorSchemes.ButtonColorScheme };
        button.Clicked += () =>
        {
            if (clicked != null && !clicked())
            {
                return;
            }
            Application.RequestStop();
        };

        return button;
    }

    internal static Button OK(bool isDefault = false, Func<bool>? clicked = null)
    {
        Button button = new Button("OK", isDefault) { ColorScheme = ColorSchemes.ButtonColorScheme };
        button.Clicked += () =>
        {
            if (clicked != null && !clicked())
            {
                return;
            }
            Application.RequestStop();
        };

        return button;
    }
}

class CreateBranchDlg : ICreateBranchDlg
{
    TextField? nameField;

    public R<string> Show(string branchName, string commitSid)
    {
        var from = commitSid != "" ? $"{branchName} at {commitSid}" : branchName;
        var title = commitSid != "" ? $"Create Branch at Commit" : "Create Branch";

        Label infoLabel = new Label(1, 0, $"From: {from}");

        nameField = new TextField(1, 2, 40, "");
        Label sep1 = new Label(nameField.Frame.X - 1, nameField.Frame.Y + 1,
            "└" + new string('─', nameField.Frame.Width) + "┘");

        bool isOk = false;
        Button okButton = Buttons.OK(true, () =>
        {
            if (Name() == "")
            {
                UI.ErrorMessage("Empty branch name");
                return false;
            }
            isOk = true;
            return true;
        });


        var dialog = new Dialog(title, 44, 9, new[] { okButton, Buttons.Cancel() })
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
            ColorScheme = ColorSchemes.DialogColorScheme,
        };
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(infoLabel, infoLabel, nameField, sep1);

        nameField.SetFocus();
        UI.ShowCursor();
        Application.Run(dialog);

        if (!isOk)
        {
            return R.Error();
        }

        return Name();
    }

    private string Name() => nameField!.Text.ToString()?.Trim() ?? "";
}

