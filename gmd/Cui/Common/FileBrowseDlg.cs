using Terminal.Gui;
using Terminal.Gui.Trees;



namespace gmd.Cui.Common;

public class FileBrowseDlg
{
    string selectedPath = "";

    internal R<string> Show(IReadOnlyList<string> files)
    {
        const int width = 50;
        const int height = 20;

        var folderView = new TreeView() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() - 2 };

        folderView.Style.ShowBranchLines = true;
        folderView.Style.ExpandableSymbol = '+';
        folderView.Style.CollapseableSymbol = null;
        folderView.MultiSelect = false;
        folderView.ObjectActivated += ItemSelected;
        SetCustomColors(folderView);

        Button cancelButton = new Button("Cancel", false);
        cancelButton.Clicked += () => Application.RequestStop();
        cancelButton.ColorScheme = ColorSchemes.Button;

        Label sep1 = new Label(0, height - 4, new string('─', width - 2));

        Dialog dialog = Components.Dialog("Select File", width, height, cancelButton);
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(folderView, sep1);

        SetupFileTree(folderView, files);
        SetupScrollBar(folderView);
        folderView.GoToFirst();

        folderView.SetFocus();
        UI.RunDialog(dialog);

        if (selectedPath == "")
        {
            return R.Error();
        }

        return selectedPath;
    }

    private void ItemSelected(ObjectActivatedEventArgs<ITreeNode> obj)
    {
        if (obj.ActivatedObject.Children.Any())
        {   // Ignore selecting folders
            return;
        }

        selectedPath = ((string)obj.ActivatedObject.Tag).TrimPrefix("/");
        Application.RequestStop();
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
            .Select(g => new TreeNode(g.Key) { Tag = $"{key}/{g.Key}", Children = Get(g.Select(y => y.Skip(1)), $"{key}/{g.Key}") })
            .OrderBy(tn => tn.Children.Any() ? 0 : 1)
            .Cast<ITreeNode>()
            .ToList();
    }


    void SetCustomColors(TreeView<ITreeNode> treeView)
    {
        var scheme = new ColorScheme
        {
            Focus = new Terminal.Gui.Attribute(Color.White, Color.DarkGray),
            Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
        };

        treeView.ColorGetter = m => scheme;
    }

    private void SetupScrollBar(TreeView<ITreeNode> treeView)
    {
        // When using scroll bar leave the last row of the control free (for over-rendering with scroll bar)
        treeView.Style.LeaveLastRow = true;

        var scrollBar = new ScrollBarView(treeView, true);

        scrollBar.ChangedPosition += () =>
        {
            treeView.ScrollOffsetVertical = scrollBar.Position;
            if (treeView.ScrollOffsetVertical != scrollBar.Position)
            {
                scrollBar.Position = treeView.ScrollOffsetVertical;
            }
            treeView.SetNeedsDisplay();
        };

        scrollBar.OtherScrollBarView.ChangedPosition += () =>
        {
            treeView.ScrollOffsetHorizontal = scrollBar.OtherScrollBarView.Position;
            if (treeView.ScrollOffsetHorizontal != scrollBar.OtherScrollBarView.Position)
            {
                scrollBar.OtherScrollBarView.Position = treeView.ScrollOffsetHorizontal;
            }
            treeView.SetNeedsDisplay();
        };

        treeView.DrawContent += (e) =>
        {
            scrollBar.Size = treeView.ContentHeight;
            scrollBar.Position = treeView.ScrollOffsetVertical;
            scrollBar.OtherScrollBarView.Size = treeView.GetContentWidth(true);
            scrollBar.OtherScrollBarView.Position = treeView.ScrollOffsetHorizontal;
            scrollBar.Refresh();
        };
    }
}

