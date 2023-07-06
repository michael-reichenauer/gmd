using Terminal.Gui;
using Terminal.Gui.Trees;


namespace gmd.Cui.Common;


public class FolderBrowseDlg
{
    string selectedPath = "";

    internal R<string> Show(IReadOnlyList<string> recentFolders)
    {
        const int width = 50;
        const int height = 20;

        var dlg = new UIDialog("Select Folder", width, height);
        var folderView = new TreeView<FileSystemInfo>() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() - 2, };

        folderView.Style.ShowBranchLines = true;
        folderView.Style.ExpandableSymbol = '+';
        folderView.Style.CollapseableSymbol = null;
        folderView.MultiSelect = false;
        folderView.ObjectActivated += ItemSelected;
        SetCustomColors(folderView);

        dlg.Add(folderView);
        dlg.AddLine(0, height - 4, width - 2);
        dlg.AddDlgCancel();

        dlg.Show(folderView, () =>
        {
            SetupFileTree(folderView, recentFolders);
            SetupScrollBar(folderView);
            folderView.GoToFirst();
            if (recentFolders.Any())
            {
                folderView.Expand();
            }
        });

        if (selectedPath == "") return R.Error();

        return selectedPath;
    }

    private void ItemSelected(ObjectActivatedEventArgs<FileSystemInfo> obj)
    {
        var item = obj.ActivatedObject;
        if (item is DirectoryInfo dir)
        {
            selectedPath = dir.FullName ?? "";
        }

        Application.RequestStop();
    }

    void SetCustomColors(TreeView<FileSystemInfo> treeView)
    {
        var yellow = new ColorScheme
        {
            Focus = new Terminal.Gui.Attribute(Color.White, Color.DarkGray),
            Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
        };

        treeView.ColorGetter = m => m is DirectoryInfo ? yellow : null;
    }

    private void SetupScrollBar(TreeView<FileSystemInfo> treeView)
    {
        // When using scroll bar leave the last row of the control free (for over-rendering with scroll bar)
        treeView.Style.LeaveLastRow = true;

        var scrollBar = new ScrollBarView(treeView, true) { ColorScheme = ColorSchemes.Scrollbar };

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

    private void SetupFileTree(
        TreeView<FileSystemInfo> treeView,
        IReadOnlyList<string> recentFolders)
    {
        treeView.TreeBuilder = new DelegateTreeBuilder<FileSystemInfo>(GetChildren, HasChildren);
        treeView.AspectGetter = GetName;

        var roots = recentFolders
            .Select(f => GetDirInfo(f))
            .Where(f => f != null).Select(f => f!)
            .OrderBy(f => f.Name)
            .Concat(DriveInfo.GetDrives()
                .Select(d => d.RootDirectory)
                .OrderBy(f => f.Name));

        treeView.AddObjects(roots);
    }

    private DirectoryInfo? GetDirInfo(string path)
    {
        try
        {
            return new DirectoryInfo(path);
        }
        catch (SystemException)
        {
            // Access violation or other error getting the file list for directory
            return null;
        }
    }

    bool HasChildren(FileSystemInfo item)
    {
        try
        {
            return item is DirectoryInfo di && di.EnumerateDirectories().Any();
        }
        catch (SystemException)
        {
            // Access violation or other error getting the file list for directory
            return false;
        }
    }

    private IEnumerable<FileSystemInfo> GetChildren(FileSystemInfo item)
    {
        // If it is a directory it's children are all contained files and dirs
        if (item is DirectoryInfo directoryInfo)
        {
            try
            {
                return directoryInfo.EnumerateDirectories()
                    .Where(f => f is DirectoryInfo)
                    .OrderBy(f => f.Name);
            }
            catch (SystemException)
            {
                // Access violation or other error getting the file list for directory
                return Enumerable.Empty<FileSystemInfo>();
            }
        }

        return Enumerable.Empty<FileSystemInfo>(); ;
    }

    private string GetName(FileSystemInfo item)
    {
        if (item is DirectoryInfo d)
        {
            return d.Name;
        }
        if (item is FileInfo f)
        {
            return f.Name;
        }

        return item.ToString();
    }
}


public abstract class TreeBuilder<T> : ITreeBuilder<T>
{
    public bool SupportsCanExpand { get; protected set; } = false;

    public virtual bool CanExpand(T toExpand) => GetChildren(toExpand).Any();

    public abstract IEnumerable<T> GetChildren(T forObject);

    public TreeBuilder(bool supportsCanExpand)
    {
        SupportsCanExpand = supportsCanExpand;
    }
}

public class DelegateTreeBuilder<T> : TreeBuilder<T>
{
    Func<T, IEnumerable<T>> childGetter;
    Func<T, bool> canExpand;

    public DelegateTreeBuilder(Func<T, IEnumerable<T>> childGetter, Func<T, bool> canExpand)
        : base(true)
    {
        this.childGetter = childGetter;
        this.canExpand = canExpand;
    }

    public override bool CanExpand(T toExpand) =>
        canExpand?.Invoke(toExpand) ?? base.CanExpand(toExpand);

    public override IEnumerable<T> GetChildren(T forObject) => childGetter.Invoke(forObject);
}