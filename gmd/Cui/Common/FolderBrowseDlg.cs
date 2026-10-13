using Terminal.Gui;
using Terminal.Gui.Trees;

namespace gmd.Cui.Common;

public class FolderBrowseDlg
{
    string selectedPath = "";

    // Starting from the folders given, the first of them opened, see Roots
    internal Result<string> Show(IReadOnlyList<string> folders)
    {
        const int width = 50;
        const int height = 20;

        var dlg = new UIDialog("Select Folder", width, height);
        var folderView = new TreeView<FileSystemInfo>()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2,
        };

        folderView.Style.ShowBranchLines = true;
        folderView.Style.ExpandableSymbol = '+';
        folderView.Style.CollapseableSymbol = null;
        folderView.MultiSelect = false;
        folderView.ObjectActivated += ItemSelected;
        SetCustomColors(folderView);

        dlg.Add(folderView);
        dlg.AddLine(0, height - 4, width - 2);
        dlg.AddDlgCancel();

        dlg.Show(
            folderView,
            () =>
            {
                SetupFileTree(folderView, folders);
                SetupScrollBar(folderView);
                folderView.GoToFirst();
                if (folders.Any())
                {
                    folderView.Expand();
                }
            }
        );

        if (selectedPath == "")
            return new Error();

        return selectedPath;
    }

    void ItemSelected(ObjectActivatedEventArgs<FileSystemInfo> obj)
    {
        var item = obj.ActivatedObject;
        if (item is DirectoryInfo dir)
        {
            selectedPath = dir.FullName ?? "";
        }

        Application.RequestStop();
    }

    static void SetCustomColors(TreeView<FileSystemInfo> treeView)
    {
        var yellow = new ColorScheme { Focus = new Color(Color.White, Color.Dark), Normal = Color.White };

        treeView.ColorGetter = m => m is DirectoryInfo ? yellow : null;
    }

    static void SetupScrollBar(TreeView<FileSystemInfo> treeView)
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

    void SetupFileTree(TreeView<FileSystemInfo> treeView, IReadOnlyList<string> folders)
    {
        treeView.TreeBuilder = new DelegateTreeBuilder<FileSystemInfo>(GetChildren, HasChildren);
        treeView.AspectGetter = GetName;

        var drives = OperatingSystem.IsWindows()
            ? DriveInfo.GetDrives().Select(d => d.RootDirectory.FullName).ToList()
            : ["/"];
        var roots = Roots(folders, Files.HomePath, drives)
            .Where(Directory.Exists)
            .Select(f => GetDirInfo(f))
            .Where(f => f != null)
            .Select(f => f!);

        treeView.AddObjects(roots);
    }

    // The folders the tree starts from: the ones given, most likely first, then home and the roots of
    // the file system, each once. Off Windows that root is '/', rather than every mount point, which
    // put '/proc', '/sys/fs/cgroup' and the like in front of someone who just wanted their home.
    internal static IReadOnlyList<string> Roots(
        IReadOnlyList<string> folders,
        string home,
        IReadOnlyList<string> drives
    )
    {
        List<string> roots = [];
        foreach (var folder in folders.Append(home).Concat(drives).Where(f => f != ""))
        {
            if (!roots.Any(r => Files.IsSamePath(r, folder)))
                roots.Add(folder);
        }
        return roots;
    }

    static DirectoryInfo? GetDirInfo(string path)
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
                return directoryInfo.EnumerateDirectories().Where(f => f is not null).OrderBy(f => f.Name);
            }
            catch (SystemException)
            {
                // Access violation or other error getting the file list for directory
                return [];
            }
        }

        return [];
        ;
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
