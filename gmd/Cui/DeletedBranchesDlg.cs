using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;
using Color = gmd.Cui.Common.Color;

namespace gmd.Cui;

interface IDeletedBranchesDlg
{
    Result<DeletedBranch> Show(IReadOnlyList<DeletedBranch> branches);
}

// The branches gmd deleted that can be brought back, one row each, see DeletedBranchRows. A dumb
// dialog, as the lost commits one is: it returns the branch picked and closes, and the command
// restores it.
class DeletedBranchesDlg : IDeletedBranchesDlg
{
    const int maxWidth = 100;
    const int maxListHeight = 15;

    public Result<DeletedBranch> Show(IReadOnlyList<DeletedBranch> branches)
    {
        var width = Math.Max(DeletedBranchRows.MinWidth + 6, Math.Min(maxWidth, Application.Driver.Cols - 2));
        var listWidth = width - 6;
        var listHeight = Math.Clamp(branches.Count, 1, maxListHeight);
        var height = listHeight + 7;

        var rows = branches.Select(b => DeletedBranchRows.Row(b, listWidth)).ToList();

        var dlg = new UIDialog("Restore Deleted Branch", width, height);
        dlg.AddLabel(2, 0, DeletedBranchRows.Header(listWidth));

        var list = dlg.AddContentView(2, 2, listWidth, listHeight, rows);
        list.IsShowCursor = false;
        list.IsScrollMode = false;
        list.IsCursorMargin = false;
        list.IsHighlightCurrentIndex = true;
        dlg.AddBorderView(list, Color.Dark);

        var reason = dlg.AddLabel(2, listHeight + 3, "");

        DeletedBranch? picked = null;
        DeletedBranch Selected() => branches[Math.Clamp(list.CurrentIndex, 0, branches.Count - 1)];
        void Restore()
        {
            picked = Selected();
            dlg.Close();
        }

        var y = listHeight + 4;
        dlg.AddButton(1, y, "_Restore", Restore);
        dlg.AddButton(width - 13, y, "Close", () => dlg.Close());

        list.CurrentIndexChange += () => reason.Text = Text.Dark(DeletedBranchRows.Reason(Selected())).ToText();

        // The button's hot key, on the list too, so restoring is one key away while it has the focus
        list.RegisterKeyHandler(Key.Enter, Restore);
        list.RegisterKeyHandler(Key.r, Restore);
        list.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        list.SetCurrentIndex(0);
        reason.Text = Text.Dark(DeletedBranchRows.Reason(Selected())).ToText();

        dlg.Show(list);
        return picked != null ? picked : new Error();
    }
}
