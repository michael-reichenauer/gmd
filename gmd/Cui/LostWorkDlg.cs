using gmd.Cui.Common;
using gmd.Server;

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

// The lines of work no branch has any more, one row each, see LostWorkRows, in a ListDlg: it returns
// what was picked and closes, and the command acts on it and shows it again.
class LostWorkDlg : ILostWorkDlg
{
    const int maxWidth = 110;

    public Result<LostWorkChoice> Show(IReadOnlyList<LostWork> works, int selectedIndex = 0) =>
        new ListDlg<LostWork, LostWorkChoice>(
            "Recover Lost Commits",
            LostWorkRows.MinWidth,
            maxWidth,
            LostWorkRows.Header,
            LostWorkRows.Row,
            LostWorkRows.Reason
        )
            .Action("_Diff", w => new LostWorkChoice(LostWorkAction.Diff, w))
            .Action("Create _Branch ...", w => new LostWorkChoice(LostWorkAction.CreateBranch, w))
            .Show(works, selectedIndex);
}
