using gmd.Common;
using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;


interface ICloneDlg
{
    R<(string, string)> Show(IReadOnlyList<string> recentFolders);
}

class CloneDlg : ICloneDlg
{
    TextField? uriField;
    TextField? pathField;


    public R<(string, string)> Show(IReadOnlyList<string> recentFolders)
    {
        const int width = 55;
        Label uriLabel = Components.Label(1, 0, "Uri:");
        uriField = Components.TextField(1, 1, width, "");
        var uriIndicator = Components.TextIndicator(uriField);
        Label pathLabel = Components.Label(1, 3, "Path:");
        pathField = Components.TextField(1, 4, width, "");
        var pathIdicator = Components.TextIndicator(pathField);

        Button browseButton = Buttons.Button("Browse ...", () =>
        {
            FolderBrowseDlg browseDlg = new FolderBrowseDlg();
            if (!Try(out var path, browseDlg.Show(recentFolders)) || path == "")
            {
                return;
            }
            SetPath(path);
        });
        browseButton.X = width - 13;
        browseButton.Y = 3;

        bool isOk = false;
        Button okButton = Buttons.OK(true, () =>
        {
            if (Uri() == "" || PathText() == "")
            {
                UI.ErrorMessage("Empty fields are not allowed.");
                return false;
            }
            isOk = true;
            return true;
        });


        var dialog = Components.Dialog("Clone Repo", width + 4, 10, okButton, Buttons.Cancel());
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(uriLabel, uriField, uriIndicator, pathLabel, pathField, pathIdicator, browseButton);

        uriField.SetFocus();
        UI.ShowCursor();
        UI.RunDialog(dialog);

        if (!isOk)
        {
            return R.Error();
        }

        return (Uri(), PathText());
    }

    string Uri() => uriField!.Text.ToString()?.Trim() ?? "";
    string PathText() => pathField!.Text.ToString()?.Trim() ?? "";

    void SetPath(string path)
    {
        path = path.Trim();

        // Try repo name from uri
        var name = "";
        var uri = Uri();
        if (uri.EndsWith(".git") && uri.Length > 7)
        {
            var i = uri.LastIndexOf('/');
            if (i > -1)
            {
                name = uri.Substring(i + 1).TrimSuffix(".git");
            }
        }

        pathField!.Text = Path.Combine(path, name);
        pathField!.SetNeedsDisplay();
    }
}

