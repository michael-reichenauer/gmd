using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Recovery;

enum DiscardedAction
{
    Diff,
    Restore,
}

// What the user picked in the discarded changes dialog: an action, and the discard it is for
record DiscardedChoice(DiscardedAction Action, DiscardedChange Change);

interface IDiscardedDlg
{
    Result<DiscardedChoice> Show(IReadOnlyList<DiscardedChange> changes, int selectedIndex = 0);
}

// What the discards threw away, and the stashes dropped, one row each, see DiscardedRows, in a
// ListDlg, as the lost commits are: it returns what was picked and closes, and the command acts on
// it, and shows the list again after a diff.
class DiscardedDlg : IDiscardedDlg
{
    const int maxWidth = 100;

    public Result<DiscardedChoice> Show(IReadOnlyList<DiscardedChange> changes, int selectedIndex = 0) =>
        new ListDlg<DiscardedChange, DiscardedChoice>(
            "Restore Discarded Changes",
            DiscardedRows.MinWidth,
            maxWidth,
            DiscardedRows.Header,
            DiscardedRows.Row,
            DiscardedRows.Reason
        )
            .Action("_Diff", c => new DiscardedChoice(DiscardedAction.Diff, c))
            .Action("_Restore", c => new DiscardedChoice(DiscardedAction.Restore, c))
            .Show(changes, selectedIndex);
}
