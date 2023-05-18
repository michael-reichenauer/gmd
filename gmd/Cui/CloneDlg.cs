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
    const int width = 55;

    UITextField? path;


    public R<(string, string)> Show(IReadOnlyList<string> recentParentFolders)
    {
        var basePath = recentParentFolders.Any() ?
            recentParentFolders[0] + Path.DirectorySeparatorChar :
            "";

        var dlg = new UIDialog("Clone Repo", width + 4, 10);

        dlg.AddLabel(1, 0, "Uri:");
        var uri = dlg.AddTextField(1, 1, width, "");
        uri.KeyUp += _ => UpdatePath(basePath, uri.Text);

        dlg.AddLabel(1, 3, "Path:");
        path = dlg.AddTextField(1, 4, width, basePath);

        dlg.AddButton("Browse ...", width - 13, 3, () =>
        {
            FolderBrowseDlg browseDlg = new FolderBrowseDlg();
            if (!Try(out var path, browseDlg.Show(recentParentFolders)) || path == "") return;
            SetBrowsedPath(basePath, uri.Text, path);
        });

        dlg.AddOK(true, () =>
        {
            if (uri.Text == "" || path.Text == "")
            {
                UI.ErrorMessage("Empty fields are not allowed.");
                return false;
            }
            return true;
        });
        dlg.AddCancel();

        if (!dlg.Show(uri))
        {
            UI.HideCursor();
            return R.Error();
        }

        UI.HideCursor();
        return (uri.Text, path.Text);
    }


    // Update path field when uri changes
    void UpdatePath(string basePath, string uri)
    {
        Log.Debug("UpdatePath");

        if (!basePath.EndsWith(Path.DirectorySeparatorChar)) return;

        if (!Try(out var name, TryParseRepoName(uri))) return;

        UpdatePathField(basePath, name);
    }


    // Update path after user has browsed target folders
    void SetBrowsedPath(string basePath, string uri, string path)
    {
        path = path.Trim();

        if (!Try(out var name, TryParseRepoName(uri))) return;

        basePath = path + Path.DirectorySeparatorChar;

        UpdatePathField(basePath, name);
    }


    void UpdatePathField(string basePath, string name)
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

        this.path!.Text = path;
        this.path!.SetNeedsDisplay();
    }


    // Try to extract git repo name
    static R<string> TryParseRepoName(string uri)
    {
        var name = "";

        var i = uri.LastIndexOf('/');
        if (i == -1) return R.Error();

        name = uri.Substring(i + 1).Trim().TrimSuffix(".git").Replace("%20", "");

        return name;
    }
}

