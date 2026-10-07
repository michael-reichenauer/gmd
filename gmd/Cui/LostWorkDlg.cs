using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;
using Color = gmd.Cui.Common.Color;

namespace gmd.Cui;

enum LostWorkAction
{
    Diff,
    CreateBranch,
}

// What the user picked in the lost commits dialog: an action, and the line of work it is for
record LostWorkChoice(LostWorkAction Action, LostWork Work);

interface ILostWorkDlg
{
    Result<LostWorkChoice> Show(IReadOnlyList<LostWork> works, int selectedIndex = 0);
}

// The lines of work no branch has any more, one row each, see LostWorkRows. A dumb dialog, as the
// worktrees one is: it returns what was picked and closes, and the command acts on it and shows it
// again.
class LostWorkDlg : ILostWorkDlg
{
    const int maxWidth = 110;
    const int maxListHeight = 15;

    public Result<LostWorkChoice> Show(IReadOnlyList<LostWork> works, int selectedIndex = 0)
    {
        var width = Math.Max(LostWorkRows.MinWidth + 6, Math.Min(maxWidth, Application.Driver.Cols - 2));
        var listWidth = width - 6;
        var listHeight = Math.Clamp(works.Count, 1, maxListHeight);
        var height = listHeight + 7;

        var rows = works.Select(w => LostWorkRows.Row(w, listWidth)).ToList();

        var dlg = new UIDialog("Recover Lost Commits", width, height);
        dlg.AddLabel(2, 0, LostWorkRows.Header(listWidth));

        var list = dlg.AddContentView(2, 2, listWidth, listHeight, rows);
        list.IsShowCursor = false;
        list.IsScrollMode = false;
        list.IsCursorMargin = false;
        list.IsHighlightCurrentIndex = true;
        dlg.AddBorderView(list, Color.Dark);

        var reason = dlg.AddLabel(2, listHeight + 3, "");

        LostWorkChoice? choice = null;
        LostWork Selected() => works[Math.Clamp(list.CurrentIndex, 0, works.Count - 1)];
        void Choose(LostWorkAction action)
        {
            choice = new LostWorkChoice(action, Selected());
            dlg.Close();
        }

        var y = listHeight + 4;
        dlg.AddButton(1, y, "_Diff", () => Choose(LostWorkAction.Diff));
        dlg.AddButton(11, y, "Create _Branch ...", () => Choose(LostWorkAction.CreateBranch));
        dlg.AddButton(width - 13, y, "Close", () => dlg.Close());

        list.CurrentIndexChange += () => reason.Text = Text.Dark(LostWorkRows.Reason(Selected())).ToText();

        // The buttons' hot keys, on the list too, so the actions are one key away while it has the
        // focus rather than a Tab away
        list.RegisterKeyHandler(Key.Enter, () => Choose(LostWorkAction.Diff));
        list.RegisterKeyHandler(Key.d, () => Choose(LostWorkAction.Diff));
        list.RegisterKeyHandler(Key.b, () => Choose(LostWorkAction.CreateBranch));
        list.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        list.SetCurrentIndex(Math.Clamp(selectedIndex, 0, works.Count - 1));
        reason.Text = Text.Dark(LostWorkRows.Reason(Selected())).ToText();

        dlg.Show(list);
        return choice != null ? choice : new Error();
    }
}
