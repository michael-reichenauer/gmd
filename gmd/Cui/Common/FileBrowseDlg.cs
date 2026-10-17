using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace gmd.Cui.Common;

public class FileBrowseDlg
{
    string selectedPath = "";
    UIDialog? dlg;

    internal Result<string> Show(IReadOnlyList<string> files, string title = "Select File")
    {
        const int width = 50;
        const int height = 20;

        var dlg = this.dlg = new UIDialog(title, width, height);

        var fileTreeView = new TreeView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2,
        };
        fileTreeView.Style.ShowBranchLines = true;
        fileTreeView.Style.ExpandableSymbol = new System.Text.Rune('>');
        fileTreeView.Style.CollapseableSymbol = null;
        fileTreeView.MultiSelect = false;
        fileTreeView.Accepted += (_, _) => ItemSelected(fileTreeView.SelectedObject);
        SetCustomColors(fileTreeView);

        dlg.Add(fileTreeView);

        dlg.AddLine(0, height - 4, width - 2);
        dlg.AddDlgCancel();

        dlg.Show(
            fileTreeView,
            () =>
            {
                SetupFileTree(fileTreeView, files);
                fileTreeView.GoToFirst();
            }
        );

        if (selectedPath == "")
            return new Error();

        return selectedPath;
    }

    // Enter or a double click on a file picks it, and closes the dialog
    private void ItemSelected(ITreeNode? node)
    {
        if (node == null || node.Children.Any())
        { // Ignore selecting folders
            return;
        }

        selectedPath = ((string)node.Tag!).TrimPrefix("/");
        dlg?.Close();
    }

    void SetupFileTree(TreeView treeView, IReadOnlyList<string> files)
    {
        var items = files.OrderBy(f => f).Select(f => f.Split('/')).ToList();

        var roots = Get(items, "");

        treeView.AddObjects(roots);
    }

    IList<ITreeNode> Get(IEnumerable<IEnumerable<string>> paths, string key)
    {
        return paths
            .Where(p => p.Any())
            .GroupBy(p => p.First())
            .Select(g => new TreeNode
            {
                Text = g.Key,
                Tag = $"{key}/{g.Key}",
                Children = Get(g.Select(y => y.Skip(1)), $"{key}/{g.Key}"),
            })
            .OrderBy(tn => tn.Children.Any() ? 0 : 1)
            .Cast<ITreeNode>()
            .ToList();
    }

    static void SetCustomColors(TreeView<ITreeNode> treeView)
    {
        treeView.ColorGetter = m => ColorSchemes.Label;
    }
}
