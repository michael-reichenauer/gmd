using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;
using Color = gmd.Cui.Common.Color;

namespace gmd.Cui;

enum WorktreeAction
{
    Open,
    Add,
    Remove,
    Prune,
    CopyPath,
}

// What the user picked in the worktrees dialog: an action, and the worktree it is for
record WorktreeChoice(WorktreeAction Action, Worktree Worktree);

interface IWorktreesDlg
{
    R<WorktreeChoice> Show(Repo repo, IReadOnlyList<Worktree> worktrees, int selectedIndex = 0);
}

// The list of the repository's worktrees, one row each, see WorktreeRows. A dumb dialog: it
// returns what was picked and closes, and the command acts on it and shows it again.
class WorktreesDlg : IWorktreesDlg
{
    const int maxWidth = 100;
    const int maxListHeight = 15;

    readonly IBranchColorService branchColorService;

    public WorktreesDlg(IBranchColorService branchColorService)
    {
        this.branchColorService = branchColorService;
    }

    public R<WorktreeChoice> Show(Repo repo, IReadOnlyList<Worktree> worktrees, int selectedIndex = 0)
    {
        var width = Math.Max(WorktreeRows.MinWidth + 6, Math.Min(maxWidth, Application.Driver.Cols - 2));
        var listWidth = width - 6;
        var listHeight = Math.Clamp(worktrees.Count, 1, maxListHeight);
        var height = listHeight + 7;

        var rows = worktrees
            .Select(w => WorktreeRows.Row(w, ColorOf(repo, w), IsUnmerged(repo, w), listWidth))
            .ToList();

        var dlg = new UIDialog("Worktrees", width, height);
        dlg.AddLabel(2, 0, WorktreeRows.Header(listWidth));

        var list = dlg.AddContentView(2, 2, listWidth, listHeight, rows);
        list.IsShowCursor = false;
        list.IsScrollMode = false;
        list.IsCursorMargin = false;
        list.IsHighlightCurrentIndex = true;
        dlg.AddBorderView(list, Color.Dark);

        var reason = dlg.AddLabel(2, listHeight + 3, "");

        WorktreeChoice? choice = null;
        Worktree Selected() => worktrees[Math.Clamp(list.CurrentIndex, 0, worktrees.Count - 1)];
        void Choose(WorktreeAction action)
        {
            choice = new WorktreeChoice(action, Selected());
            dlg.Close();
        }

        var y = listHeight + 4;
        var open = dlg.AddButton(1, y, "_Open", () => Choose(WorktreeAction.Open));
        dlg.AddButton(11, y, "_Add...", () => Choose(WorktreeAction.Add));
        var remove = dlg.AddButton(23, y, "_Remove...", () => Choose(WorktreeAction.Remove));
        var prune = dlg.AddButton(38, y, "_Prune", () => Choose(WorktreeAction.Prune));
        dlg.AddButton(49, y, "_Copy Path", () => Choose(WorktreeAction.CopyPath));
        dlg.AddButton(width - 13, y, "Close", () => dlg.Close());

        void UpdateForSelected()
        {
            var w = Selected();
            reason.Text = Text.Dark(WorktreeRows.Reason(w)).ToText();
            open.Enabled = WorktreeRows.CanOpen(w);
            remove.Enabled = WorktreeRows.CanRemove(w);
            prune.Enabled = WorktreeRows.CanPrune(worktrees);
        }

        list.CurrentIndexChange += UpdateForSelected;

        // The buttons' hot keys, on the list too, so the actions are one key away while it has
        // the focus rather than a Tab away
        void ChooseIf(bool isEnabled, WorktreeAction action)
        {
            if (isEnabled)
                Choose(action);
        }
        list.RegisterKeyHandler(Key.Enter, () => ChooseIf(WorktreeRows.CanOpen(Selected()), WorktreeAction.Open));
        list.RegisterKeyHandler(Key.o, () => ChooseIf(WorktreeRows.CanOpen(Selected()), WorktreeAction.Open));
        list.RegisterKeyHandler(Key.a, () => Choose(WorktreeAction.Add));
        list.RegisterKeyHandler(Key.r, () => ChooseIf(WorktreeRows.CanRemove(Selected()), WorktreeAction.Remove));
        list.RegisterKeyHandler(Key.p, () => ChooseIf(WorktreeRows.CanPrune(worktrees), WorktreeAction.Prune));
        list.RegisterKeyHandler(Key.c, () => Choose(WorktreeAction.CopyPath));
        list.RegisterKeyHandler(Key.Esc, () => dlg.Close());

        list.SetCurrentIndex(Math.Clamp(selectedIndex, 0, worktrees.Count - 1));
        UpdateForSelected();

        dlg.Show(list);
        return choice != null ? choice : R.Error();
    }

    Color ColorOf(Repo repo, Worktree w) =>
        w.Branch != "" && repo.BranchByName.TryGetValue(w.Branch, out var branch)
            ? branchColorService.GetColor(repo, branch)
            : Color.White;

    static bool IsUnmerged(Repo repo, Worktree w) =>
        w.Branch != "" && repo.BranchByName.TryGetValue(w.Branch, out var branch) && repo.HasUnmergedCommits(branch);
}
