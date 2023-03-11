using gmd.Common;
using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;


interface ICloneDlg
{
    R<(string, string)> Show(IReadOnlyList<string> recentParentFolders);
}

class CloneDlg : ICloneDlg
{
    TextField? uriField;
    TextField? pathField;

    string basePath = "";


    public R<(string, string)> Show(IReadOnlyList<string> recentParentFolders)
    {
        basePath = recentParentFolders.Any() ? recentParentFolders[0] + Path.DirectorySeparatorChar : "";

        const int width = 55;
        Label uriLabel = Components.Label(1, 0, "Uri:");
        uriField = Components.TextField(1, 1, width, "");
        var uriIndicator = Components.TextIndicator(uriField);
        uriField.KeyUp += _ => UpdatePath();


        Label pathLabel = Components.Label(1, 3, "Path:");
        pathField = Components.TextField(1, 4, width, basePath);
        var pathIdicator = Components.TextIndicator(pathField);

        Button browseButton = Buttons.Button("Browse ...", () =>
        {
            FolderBrowseDlg browseDlg = new FolderBrowseDlg();
            if (!Try(out var path, browseDlg.Show(recentParentFolders)) || path == "")
            {
                return;
            }
            SetBrowsedPath(path);
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


    // Update path field when uri changes
    void UpdatePath()
    {
        if (!basePath.EndsWith(Path.DirectorySeparatorChar)) return;

        var uri = Uri();
        if (!Try(out var name, TryParseRepoName(uri))) return;

        UpdatePathField(name);
    }


    // Update path after user has browsed target folders
    void SetBrowsedPath(string path)
    {
        path = path.Trim();

        var uri = Uri();
        if (!Try(out var name, TryParseRepoName(uri))) return;

        basePath = path + Path.DirectorySeparatorChar;

        UpdatePathField(name);
    }


    void UpdatePathField(string name)
    {
        var newName = name;
        var path = Path.Combine(basePath, newName);
        for (int i = 1; i < 50; i++)
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                break;
            }
            newName = $"{name}-{i}";
            path = Path.Combine(basePath, newName);
        }

        pathField!.Text = path;
        pathField!.SetNeedsDisplay();
    }


    // Try to extract git repo name
    static R<string> TryParseRepoName(string uri)
    {
        var name = "";
        if (!uri.EndsWith(".git") && uri.Length > 7) return R.Error();

        var i = uri.LastIndexOf('/');
        if (i == -1) return R.Error();

        name = uri.Substring(i + 1).TrimSuffix(".git");

        return name;
    }
}

