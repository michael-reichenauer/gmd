using gmd.Cui.Common;

namespace gmd.Cui;

interface IInitRepoDlg
{
    R<string> Show(IReadOnlyList<string> recentParentFolders);
}

class InitRepoDlg : IInitRepoDlg
{
    const int width = 55;
    UITextField? pathField;

    public R<string> Show(IReadOnlyList<string> recentParentFolders)
    {
        var basePath = recentParentFolders.Any() ?
            recentParentFolders[0] + Path.DirectorySeparatorChar :
            "";

        var dlg = new UIDialog("Init Repo", width + 4, 8);

        dlg.AddLabel(1, 1, "Path:");
        pathField = dlg.AddInputField(7, 1, width - 16, basePath, InputMarkers.None);

        dlg.AddButton(width - 9, 1, "Browse", () =>
        {
            FolderBrowseDlg browseDlg = new FolderBrowseDlg();
            if (!Try(out var path, browseDlg.Show(recentParentFolders)) || path == "") return;
            pathField.Text = path;
        });


        dlg.Validate(() => pathField.Text != "", "Empty path is not allowed");

        if (!dlg.ShowOkCancel(pathField)) return R.Error();

        return pathField.Text;
    }
}

