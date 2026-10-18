using Terminal.Gui.Input;

namespace gmd.Cui.Common;

// Picks a folder: a repository to open (Browse in the repo menu, Open a folder on the start page), or
// the folder Clone, Init and Create Worktree put theirs in. Laid out as the file browser is
// (FileBrowseDlg): a field to type in over the list, and a status line with the keys. With nothing
// typed, the list is the tree of folders from the folders given, home and the root of the file system
// (FolderTree); once something is typed, the folders FolderFinder lists for it, the folders listed so
// far that have the words in their path, or the folders in the folder of a path typed. Clearing it
// goes back to the tree as it was. The field has the keyboard, so the dialog takes the keys that move
// in the list before the field sees them, and ← → too while nothing is typed, which open and close the
// folders of the tree, and → at the end of the text, which goes into the folder found, as its path.
// Enter or a click on a name picks the folder, and a click on a folder's ▸ or ▾ opens or closes it.
// The rows are drawn by FolderBrowseRows.
class FolderBrowseDlg
{
    const int MaxWidth = 90;
    const int MaxHeight = 34;
    const int ListY = 2; // Below the text field and the line under it

    readonly IFolderReader reader = new FolderReader();
    readonly string home = Files.HomePath;

    UIDialog dlg = null!;
    UITextField findField = null!;
    ContentView listView = null!;
    UILabel statusLabel = null!;
    FolderTree tree = null!;
    IReadOnlyList<string> found = [];
    IReadOnlyList<string> words = [];
    string foundText = "";
    string foundIn = ""; // The folder of a path typed, or "" for words found among the folders listed
    string foundName = ""; // The start of a name typed after that folder
    int treeIndex; // Where the cursor was in the tree when typing started
    string action = "";
    int statusWidth;
    Result<string> picked = new Error("No folder picked");

    // The title, e.g. 'Open Repo', what picking a folder does, e.g. 'open', and the folders to start
    // from, the most likely first, see Roots
    internal Result<string> Show(string title, string action, IReadOnlyList<string> folders)
    {
        this.action = action;
        var drives = OperatingSystem.IsWindows()
            ? DriveInfo.GetDrives().Select(d => d.RootDirectory.FullName).ToList()
            : ["/"];
        tree = new FolderTree(Roots(folders, home, drives).Where(Directory.Exists).ToList(), home, reader);

        // As large as the screen allows, since the tree grows as folders are opened
        var width = Math.Min(MaxWidth, UI.ScreenSize.Width - 4);
        var height = Math.Min(MaxHeight, UI.ScreenSize.Height - 4);
        statusWidth = width - 4;

        dlg = new UIDialog(title, width, height, OnKey);
        dlg.RegisterMouseHandler(OnMouse);

        dlg.AddLabel(1, 0, "Find:");
        findField = dlg.AddInputField(7, 0, width - 11);
        dlg.AddLine(0, 1, width - 2);
        var listHeight = height - ListY - 3;
        listView = dlg.AddContentView(0, ListY, width - 2, listHeight, OnGetContent);
        // Its size now rather than once the dialog is laid out, so that the cursor can be scrolled to
        listView.Frame = new System.Drawing.Rectangle(0, ListY, width - 2, listHeight);
        listView.IsShowCursor = false;
        listView.IsScrollMode = false;
        listView.CurrentIndexChange += UpdateStatus;
        statusLabel = dlg.AddLabel(1, height - 3);

        findField.TextChanged += (_, _) => Update();
        ShowTreeAt(0);

        dlg.Show(findField);
        return picked;
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

    bool IsTree => foundText == "";

    // Called for every change of the text: a text as it was must not move the cursor
    void Update()
    {
        var text = findField.Text;
        if (text == foundText)
            return;

        if (foundText == "")
            treeIndex = listView.CurrentIndex; // Leaving the tree, to come back to it as it was
        foundText = text;
        if (text == "")
        {
            ShowTreeAt(treeIndex);
            return;
        }

        if (FolderFinder.IsPath(text))
        {
            var (folder, name) = FolderFinder.Split(text, home);
            foundIn = folder;
            foundName = name;
            found = FolderFinder.InFolder(folder, name, tree.FoldersIn(folder));
            words = name == "" ? [] : [name];
        }
        else
        {
            foundIn = "";
            found = FolderFinder.Find(tree.Listed, text, home);
            words = FileFinder.Words(text);
        }

        listView.SetTotalCount(Math.Max(1, found.Count));
        listView.MoveToTop();
        listView.SetNeedsDraw();
        UpdateStatus();
    }

    // The cursor on the row, scrolled to only when out of sight, so that opening a folder does not
    // move the rows under the cursor
    void ShowTreeAt(int index)
    {
        index = Math.Clamp(index, 0, Math.Max(0, tree.Rows.Count - 1));
        listView.SetTotalCount(tree.Rows.Count);
        listView.ScrollToShowIndex(index, 1);
        listView.SetCurrentIndex(index);
        listView.SetNeedsDraw();
        UpdateStatus();
    }

    // The folder the cursor is on in the tree, or what was found and how many, and the keys
    void UpdateStatus()
    {
        var keys = new TextBuilder();
        if (IsTree)
            keys.Cyan("↑↓").Dark(" select  ").Cyan("←→").Dark(" folders  ");
        else if (found.Count > 0)
            keys.Cyan("↑↓").Dark(" select  ").Cyan("→").Dark(" into  ");
        if (IsTree || found.Count > 0)
            keys.Cyan("Enter").Dark($" {action}");

        // The folder itself is listed first when nothing is typed after its path, and is not in it
        var about =
            IsTree ? Shown(tree.Rows.ElementAtOrDefault(listView.CurrentIndex)?.Path ?? "")
            : foundIn != ""
                ? $"{Folders(found.Count - (foundName == "" && found.Count > 0 ? 1 : 0))} in {Shown(foundIn)}"
            : $"{found.Count} of {Folders(tree.Listed.Count)}";
        var aboutWidth = Math.Max(0, statusWidth - keys.Length - 3);
        statusLabel.Text = Text.Dark($"{CutStart(about, aboutWidth)}   ").Add(keys);
    }

    bool OnKey(Key key)
    {
        if (key == Key.Enter)
            Pick(listView.CurrentIndex);
        else if (key == Key.CursorUp)
            listView.Move(-1);
        else if (key == Key.CursorDown)
            listView.Move(1);
        else if (key == Key.PageUp)
            listView.Move(-listView.ContentHeight);
        else if (key == Key.PageDown)
            listView.Move(listView.ContentHeight);
        else if (key == Key.CursorRight && IsTree)
            ShowTreeAt(tree.Expand(listView.CurrentIndex));
        else if (key == Key.CursorLeft && IsTree)
            ShowTreeAt(tree.Collapse(listView.CurrentIndex));
        else if (key == Key.CursorRight && findField.CursorPosition == findField.RawText.Length && found.Count > 0)
            Into(listView.CurrentIndex);
        else
            return false;
        return true;
    }

    // A click on a name does what Enter does, one on a folder's marker opens or closes it, and the
    // wheel scrolls the list. The dialog has the mouse, so the coordinates are the dialog's, its border
    // included.
    bool OnMouse(MouseEvent ev)
    {
        var listRow = ev.Y - 1 - ListY;
        var listX = ev.X - 1;
        var isInList = listRow >= 0 && listRow < listView.ViewHeight && listX >= 0 && listX < listView.ViewWidth;

        if (ev.Flags.HasFlag(MouseFlags.LeftButtonClicked) && isInList)
        {
            var index = listView.FirstIndex + listRow;
            if (index >= (IsTree ? tree.Rows.Count : found.Count))
                return true; // Below the last row
            listView.SetIndexAtViewY(listRow);
            if (IsTree && FolderBrowseRows.IsOnMarker(tree.Rows[index], listX))
            {
                tree.Toggle(index);
                ShowTreeAt(index);
            }
            else
            {
                Pick(index);
            }
            return true;
        }
        if (ev.Flags.HasFlag(MouseFlags.WheeledDown) || ev.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            listView.Scroll(ev.Flags.HasFlag(MouseFlags.WheeledDown) ? 1 : -1);
            return true;
        }

        return false;
    }

    void Pick(int index)
    {
        if (PathAt(index) is not string path)
            return; // Nothing found, so there is nothing to pick; Esc goes back

        picked = path;
        dlg.Close();
    }

    // Goes into the folder found, typing its path, so the list is the folders in it
    void Into(int index)
    {
        if (PathAt(index) is not string path)
            return;

        var shown = Shown(path);
        var separator = Path.DirectorySeparatorChar;
        findField.Text = shown.EndsWith(separator) ? shown : shown + separator;
        findField.CursorPosition = findField.RawText.Length;
    }

    string? PathAt(int index) =>
        IsTree ? tree.Rows.ElementAtOrDefault(index)?.Path
        : index >= 0 && index < found.Count ? found[index]
        : null;

    // A path as the rows show it, with home as '~', and with no separator at its end but the root's
    string Shown(string path) => Files.ShortHomePath(Path.TrimEndingDirectorySeparator(path), home);

    static string Folders(int count) => count == 1 ? "1 folder" : $"{count} folders";

    static string CutStart(string text, int width) =>
        text.Length <= width || width < 2 ? text : "…" + text[(text.Length - (width - 1))..];

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (IsTree)
        {
            var treeRows = tree
                .Rows.Skip(firstIndex)
                .Take(count)
                .Select((r, i) => Highlighted(FolderBrowseRows.TreeRow(r, width), i + firstIndex == currentIndex));
            return (treeRows, tree.Rows.Count);
        }

        if (found.Count == 0)
            return ([Text.Dark($"  {NoneFound()}")], 1);

        var foundRows = found
            .Skip(firstIndex)
            .Take(count)
            .Select(
                (p, i) =>
                    Highlighted(
                        FolderBrowseRows.FoundRow(Shown(p), tree.IsRepo(p), words, width),
                        i + firstIndex == currentIndex
                    )
            );
        return (foundRows, found.Count);
    }

    // Why nothing is listed, and for words, where else to look
    string NoneFound() =>
        foundIn == "" ? "No folder listed so far has that in its path; a path, as ~/code, looks in a folder"
        : tree.FoldersIn(foundIn) == null ? $"{Shown(foundIn)} is no folder that can be read"
        : $"No folder in {Shown(foundIn)} has that in its name";

    static Text Highlighted(Text row, bool isCurrent) => isCurrent ? row.ToHighlight() : row;
}
