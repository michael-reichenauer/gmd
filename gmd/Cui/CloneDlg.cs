using gmd.Cui.Common;

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

        dlg.AddButton(width - 13, 3, "Browse ...", () =>
        {
            FolderBrowseDlg browseDlg = new FolderBrowseDlg();
            if (!Try(out var path, browseDlg.Show(recentParentFolders)) || path == "") return;
            SetBrowsedPath(uri.Text, path);
        });

        dlg.Validate(() => uri.Text != "", "Empty uri is not allowed");
        dlg.Validate(() => path.Text != "", "Empty path is not allowed");

        if (!dlg.ShowOkCancel(uri)) return R.Error();

        return (uri.Text, path.Text);
    }


    // Update path field when uri changes
    void UpdatePath(string basePath, string uri)
    {
        if (!basePath.EndsWith(Path.DirectorySeparatorChar)) return;

        if (!Try(out var name, TryParseRepoName(uri))) return;

        UpdatePathField(basePath, name);
    }


    // Update path after user has browsed target folders
    void SetBrowsedPath(string uri, string path)
    {
        path = path.Trim();

        if (!Try(out var name, TryParseRepoName(uri))) return;

        string basePath = path + Path.DirectorySeparatorChar;

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
        var i = uri.LastIndexOf('/');
        if (i == -1) return R.Error();

        return uri[(i + 1)..].Trim().TrimSuffix(".git").Replace("%20", "");
    }
}

