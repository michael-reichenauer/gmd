using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace gmd.Cui.Common;

public class FolderBrowseDlg
{
    string selectedPath = "";
    UIDialog? dlg;

    // Starting from the folders given, the first of them opened, see Roots
    internal Result<string> Show(IReadOnlyList<string> folders)
    {
        const int width = 50;
        const int height = 20;

        var dlg = this.dlg = new UIDialog("Select Folder", width, height);
        var folderView = new TreeView<FileSystemInfo>()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2,
        };

        folderView.Style.ShowBranchLines = true;
        folderView.Style.ExpandableSymbol = new System.Text.Rune('+');
        folderView.Style.CollapseableSymbol = null;
        folderView.MultiSelect = false;
        folderView.Accepted += (_, _) => ItemSelected(folderView.SelectedObject);
        SetCustomColors(folderView);

        dlg.Add(folderView);
        dlg.AddLine(0, height - 4, width - 2);
        dlg.AddDlgCancel();

        dlg.Show(
            folderView,
            () =>
            {
                SetupFileTree(folderView, folders);
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

    // Enter or a double click on a folder picks it, and closes the dialog
    void ItemSelected(FileSystemInfo? item)
    {
        if (item is DirectoryInfo dir)
        {
            selectedPath = dir.FullName ?? "";
        }

        dlg?.Close();
    }

    static void SetCustomColors(TreeView<FileSystemInfo> treeView)
    {
        treeView.ColorGetter = m => m is DirectoryInfo ? ColorSchemes.Label : null;
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
