using gmd.Cui.Common;

namespace gmd.Cui.Start;

// What to clone and where to put it
record CloneInfo(string Uri, string Path);

interface ICloneDlg
{
    Result<CloneInfo> Show(IReadOnlyList<string> recentParentFolders);
}

class CloneDlg : ICloneDlg
{
    const int width = 60;

    UITextField? path;

    public Result<CloneInfo> Show(IReadOnlyList<string> recentParentFolders)
    {
        // The folder the clone goes in, the repo's name after it: the recent one, or the one browsed
        // to. The browsed one was dropped when the uri had no name in it yet, and replaced by the
        // recent one again with the next key typed into the uri.
        var parent = recentParentFolders.Any() ? recentParentFolders[0] : "";

        var dlg = new UIDialog("Clone Repo", width + 4, 8);

        dlg.AddLabel(1, 0, "Uri:");
        var uri = dlg.AddInputField(6, 0, width - 6, "");
        uri.TextChanged += (_, _) => UpdatePath(parent, uri.Text);

        dlg.AddLabel(1, 2, "Path:");
        path = dlg.AddInputField(7, 2, width - 16, WithSeparator(parent), InputMarkers.None);

        dlg.AddButton(
            width - 9,
            2,
            "Browse",
            () =>
            {
                FolderBrowseDlg browseDlg = new FolderBrowseDlg();
                if (
                    browseDlg.Show("Folder to Clone Into", "choose", recentParentFolders) is not string folder
                    || folder.Trim() == ""
                )
                    return;
                parent = folder.Trim();
                UpdatePath(parent, uri.Text, isBrowsed: true);
            }
        );

        dlg.Validate(() => uri.Text != "", "Empty uri is not allowed");
        dlg.Validate(() => path.Text != "", "Empty path is not allowed");

        if (!dlg.ShowOkCancel(uri))
            return new Error();

        return new CloneInfo(uri.Text, path.Text);
    }

    // The path is the folder and the repo's name from the uri, once the uri has one; just browsed
    // to, the folder alone until it has
    void UpdatePath(string parent, string uri, bool isBrowsed = false)
    {
        if (parent == "")
            return;

        if (TryParseRepoName(uri) is string name)
            SetPath(Files.UniqueFolderPath(WithSeparator(parent), name));
        else if (isBrowsed)
            SetPath(WithSeparator(parent));
    }

    void SetPath(string text)
    {
        this.path!.Text = text;
        this.path!.SetNeedsDraw();
    }

    static string WithSeparator(string folder) =>
        folder == "" || folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar;

    // Try to extract git repo name
    static Result<string> TryParseRepoName(string uri)
    {
        var i = uri.LastIndexOf('/');
        if (i == -1)
            return new Error();

        return uri[(i + 1)..].Trim().TrimSuffix(".git").Replace("%20", "");
    }
}
