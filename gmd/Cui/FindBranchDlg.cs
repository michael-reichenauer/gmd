using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IFindBranchDlg
{
    // The branch picked, or an error when the dialog was closed without picking one
    Result<Branch> Show(Repo repo, string text);
}

// Finds a branch by typing part of its name, for a repo with more branches than a menu can list:
// the Open Branch menu opens it with the first letter typed in it. The list narrows as the name is
// typed, see BranchFinder for what matches and in what order; ↑ ↓ pick one, and Enter or a click
// shows it. The text field has the keyboard, so the dialog takes the keys that move in the list
// before the field sees them, as the commit filter does.
class FindBranchDlg : IFindBranchDlg
{
    const int MaxWidth = 76;
    const int MaxHeight = 22;
    const int ListY = 2; // Below the text field and the line under it

    readonly IBranchColorService branchColorService;

    UIDialog dlg = null!;
    UITextField findField = null!;
    ContentView listView = null!;
    UILabel statusLabel = null!;
    Repo repo = null!;
    IReadOnlyList<Branch> found = [];
    int totalCount; // The branches there are to find, which cannot change while the dialog is up
    string? foundText;
    Result<Branch> picked = new Error("No branch picked");

    internal FindBranchDlg(IBranchColorService branchColorService)
    {
        this.branchColorService = branchColorService;
    }

    public Result<Branch> Show(Repo repo, string text)
    {
        this.repo = repo;
        this.picked = new Error("No branch picked"); // The dialog is reused
        this.foundText = null;
        this.totalCount = BranchFinder.Find(repo, "").Count;
        // As tall as every branch needs, within the screen, so that a repo with few is not a box of
        // blank rows: the list plus the field, the line under it, the status and the borders
        var width = Math.Min(MaxWidth, Application.Driver.Cols - 4);
        var height = Math.Min(Math.Min(MaxHeight, Application.Driver.Rows - 4), Math.Max(8, totalCount + 5));

        dlg = new UIDialog("Find Branch", width, height, OnKey);
        dlg.RegisterMouseHandler(OnMouse);

        dlg.AddLabel(1, 0, "Name:");
        findField = dlg.AddInputField(7, 0, width - 11, text);
        dlg.AddLine(0, 1, width - 2);
        listView = dlg.AddContentView(0, ListY, Dim.Fill(), height - ListY - 3, OnGetContent);
        listView.IsShowCursor = false;
        listView.IsScrollMode = false;
        listView.IsCursorMargin = false;
        statusLabel = dlg.AddLabel(1, height - 3);

        findField.KeyUp += e =>
        {
            Update();
            e.Handled = true;
        };
        Update();

        dlg.Show(findField);
        return picked;
    }

    void Update()
    {
        // Every key released in the field comes here, the ones moving in the list too, which must
        // not move it back to the top
        var text = findField.Text;
        if (text == foundText)
            return;

        foundText = text;
        found = BranchFinder.Find(repo, text);
        listView.MoveToTop();
        listView.SetNeedsDisplay();
        UpdateStatus();
    }

    void UpdateStatus()
    {
        var matches = found.Count == totalCount ? $"{totalCount} branches" : $"{found.Count} of {totalCount} branches";
        statusLabel.Text = Text.Dark($"{matches}   ").Cyan("↑↓").Dark(" select  ").Cyan("Enter").Dark(" show");
    }

    bool OnKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                Pick();
                return true;
            case Key.CursorUp:
                listView.Move(-1);
                return true;
            case Key.CursorDown:
                listView.Move(1);
                return true;
            case Key.PageUp:
                listView.Move(-listView.ContentHeight);
                return true;
            case Key.PageDown:
                listView.Move(listView.ContentHeight);
                return true;
        }

        return false;
    }

    // A click on a branch picks it, and the wheel scrolls the list. The dialog has the mouse, so
    // the coordinates are the dialog's, its border included.
    bool OnMouse(MouseEvent ev)
    {
        var listRow = ev.Y - 1 - ListY;
        var isInList = listRow >= 0 && listRow < listView.ViewHeight && ev.X > 0 && ev.X < listView.ViewWidth + 1;

        if (ev.Flags.HasFlag(MouseFlags.Button1Clicked) && isInList)
        {
            listView.SetIndexAtViewY(listRow);
            Pick();
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

    void Pick()
    {
        var index = listView.CurrentIndex;
        if (index < 0 || index >= found.Count)
            return; // Nothing matches, so there is nothing to pick; Esc goes back

        picked = found[index];
        dlg.Close();
    }

    (IEnumerable<Text> rows, int total) OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (!found.Any())
            return ([Text.Dark("  No branch has that in its name")], 1);

        var rows = found
            .Skip(firstIndex)
            .Take(count)
            .Select((b, i) => i + firstIndex == currentIndex ? ToRow(b, width).ToHighlight() : ToRow(b, width));
        return (rows, found.Count);
    }

    // The same markers as the branch menus: '●' the current branch, 'o' one shown in the graph and
    // '~' one deleted, which gmd recovered from a merge message. The name is in its graph color,
    // and the initials of the author of its last commit are at the right, as in the menus.
    Text ToRow(Branch b, int width)
    {
        var marker =
            b.IsCurrent || b.IsLocalCurrent ? "●"
            : b.IsInView ? "o"
            : " ";
        var name = b.IsGitBranch ? $" {b.NiceNameUnique}" : $"~{b.NiceNameUnique}";
        var initials = BranchMenu.Initials(repo.CommitById[b.TipId].Author);

        var nameWidth = Math.Max(0, width - 4 - initials.Length - 1);
        var nameText = name.Length > nameWidth ? name[..Math.Max(0, nameWidth - 1)] + "┅" : name.PadRight(nameWidth);
        var color = b.IsGitBranch ? branchColorService.GetColor(repo, b) : Common.Color.Dark;

        return Text.White($" {marker}").Color(color, nameText).Dark($" {initials} ");
    }
}
