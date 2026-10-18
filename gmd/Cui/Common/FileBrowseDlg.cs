using Terminal.Gui.Input;

namespace gmd.Cui.Common;

// Picks a file of a commit, for Blame File and Full File History: the files as a tree of folders to
// browse (FileTree), or once something is typed, the files whose path has it (FileFinder), the
// files the commit changed marked with their letter in both. Clearing what was typed goes back to
// the tree as it was. The field has the keyboard, so the dialog takes the keys that move in the list
// before the field sees them, as Find Branch does, and ← → too while nothing is typed, which open and
// close the folders of the tree. Enter or a click picks a file, or opens or closes a folder. The rows
// are drawn by FileBrowseRows.
class FileBrowseDlg
{
    const int MaxWidth = 90;
    const int MaxHeight = 34;
    const int ListY = 2; // Below the text field and the line under it

    UIDialog dlg = null!;
    UITextField findField = null!;
    ContentView listView = null!;
    UILabel statusLabel = null!;
    FileTree tree = null!;
    IReadOnlyList<string> files = [];
    IReadOnlyDictionary<string, char> changes = new Dictionary<string, char>();
    IReadOnlyList<string> found = [];
    IReadOnlyList<string> words = [];
    string foundText = "";
    int treeIndex; // Where the cursor was in the tree when typing started
    string about = "";
    string action = "";
    Result<string> picked = new Error("No file picked");

    // The files to pick from, and the letter of each one the commit changed; about is what the files
    // are, e.g. 'commit 1be33bd1', and action what picking one does, e.g. 'blame'
    internal Result<string> Show(
        string title,
        IReadOnlyList<string> files,
        IReadOnlyDictionary<string, char> changes,
        string about,
        string action
    )
    {
        this.files = files;
        this.changes = changes;
        this.about = about;
        this.action = action;
        tree = new FileTree(files, changes);

        // As tall as the tree with every folder open needs, within the screen, so that a commit of
        // few files is not a box of blank rows: the list plus the field, the line under it, the
        // status and the borders
        var width = Math.Min(MaxWidth, UI.ScreenSize.Width - 4);
        var height = Math.Min(Math.Min(MaxHeight, UI.ScreenSize.Height - 4), Math.Max(10, tree.AllCount + 5));

        dlg = new UIDialog(title, width, height, OnKey);
        dlg.RegisterMouseHandler(OnMouse);

        dlg.AddLabel(1, 0, "Find:");
        findField = dlg.AddInputField(7, 0, width - 11);
        dlg.AddLine(0, 1, width - 2);
        var listHeight = height - ListY - 3;
        listView = dlg.AddContentView(0, ListY, width - 2, listHeight, OnGetContent);
        // Its size now rather than once the dialog is laid out, so that the cursor put on a changed
        // file far down is scrolled to: the view scrolls as far as its height, which it has none of yet
        listView.Frame = new System.Drawing.Rectangle(0, ListY, width - 2, listHeight);
        listView.IsShowCursor = false;
        listView.IsScrollMode = false;
        listView.CurrentIndexChange += UpdateStatus;
        statusLabel = dlg.AddLabel(1, height - 3);

        findField.TextChanged += (_, _) => Update();
        ShowTreeAt(tree.FirstChanged);

        dlg.Show(findField);
        return picked;
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

        found = FileFinder.Find(files, text);
        words = FileFinder.Words(text);
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

    // How many files there are, and the keys, with what Enter does where the cursor is
    void UpdateStatus()
    {
        var count = IsTree ? $"{files.Count} files of {about}" : $"{found.Count} of {files.Count} files";
        var row = IsTree ? tree.Rows.ElementAtOrDefault(listView.CurrentIndex) : null;
        var enter =
            row?.IsFolder != true ? action
            : row.IsOpen ? "close"
            : "open";

        var text = Text.Dark($"{count}   ").Cyan("↑↓").Dark(" select  ");
        if (IsTree)
            text.Cyan("←→").Dark(" folders  ");
        if (IsTree || found.Count > 0)
            text.Cyan("Enter").Dark($" {enter}");
        statusLabel.Text = text;
    }

    bool OnKey(Key key)
    {
        if (key == Key.Enter)
            Activate();
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
        else
            return false;
        return true;
    }

    // A click on a row does what Enter does on it, and the wheel scrolls the list. The dialog has the
    // mouse, so the coordinates are the dialog's, its border included.
    bool OnMouse(MouseEvent ev)
    {
        var listRow = ev.Y - 1 - ListY;
        var isInList = listRow >= 0 && listRow < listView.ViewHeight && ev.X > 0 && ev.X < listView.ViewWidth + 1;

        if (ev.Flags.HasFlag(MouseFlags.LeftButtonClicked) && isInList)
        {
            if (listView.FirstIndex + listRow >= listView.TotalCount)
                return true; // Below the last row
            listView.SetIndexAtViewY(listRow);
            Activate();
            return true;
        }
        if (ev.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            listView.Scroll(1);
            return true;
        }
        if (ev.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            listView.Scroll(-1);
            return true;
        }

        return false;
    }

    // Picks the file the cursor is on, or opens or closes the folder
    void Activate()
    {
        var index = listView.CurrentIndex;
        if (IsTree && tree.Rows.ElementAtOrDefault(index) is FileTree.Row { IsFolder: true })
        {
            tree.Toggle(index);
            ShowTreeAt(index);
            return;
        }

        var path = IsTree ? tree.Rows.ElementAtOrDefault(index)?.Path ?? "" : FoundAt(index);
        if (path == "")
            return; // Nothing matches, so there is nothing to pick; Esc goes back

        picked = path;
        dlg.Close();
    }

    string FoundAt(int index) => index >= 0 && index < found.Count ? found[index] : "";

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (IsTree)
        {
            var treeRows = tree
                .Rows.Skip(firstIndex)
                .Take(count)
                .Select((r, i) => Highlighted(FileBrowseRows.TreeRow(r, width), i + firstIndex == currentIndex));
            return (treeRows, tree.Rows.Count);
        }

        if (found.Count == 0)
            return ([Text.Dark("  No file has that in its path")], 1);

        var foundRows = found
            .Skip(firstIndex)
            .Take(count)
            .Select(
                (p, i) =>
                    Highlighted(
                        FileBrowseRows.FoundRow(p, changes.GetValueOrDefault(p, ' '), words, width),
                        i + firstIndex == currentIndex
                    )
            );
        return (foundRows, found.Count);
    }

    static Text Highlighted(Text row, bool isCurrent) => isCurrent ? row.ToHighlight() : row;
}
