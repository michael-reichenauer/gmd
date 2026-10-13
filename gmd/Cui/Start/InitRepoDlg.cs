using gmd.Cui.Common;

namespace gmd.Cui.Start;

interface IInitRepoDlg
{
    // The folder to init, proposed as 'defaultPath', and browsed for from 'browseFolders'
    Result<string> Show(string defaultPath, IReadOnlyList<string> browseFolders);
}

class InitRepoDlg : IInitRepoDlg
{
    const int width = 55;
    UITextField? pathField;

    public Result<string> Show(string defaultPath, IReadOnlyList<string> browseFolders)
    {
        var dlg = new UIDialog("Init Repo", width + 4, 8);

        dlg.AddLabel(1, 1, "Path:");
        pathField = dlg.AddInputField(7, 1, width - 16, defaultPath, InputMarkers.None);

        dlg.AddButton(
            width - 9,
            1,
            "Browse",
            () =>
            {
                FolderBrowseDlg browseDlg = new FolderBrowseDlg();
                if (browseDlg.Show(browseFolders) is not string path || path == "")
                    return;
                pathField.Text = path;
            }
        );

        dlg.Validate(() => pathField.Text != "", "Empty path is not allowed");

        if (!dlg.ShowOkCancel(pathField))
            return new Error();

        return pathField.Text;
    }
}
