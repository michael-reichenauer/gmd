using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Branches;

// What Delete Merged Branches was asked to do: delete the branch picked, or every one it can
record MergedBranchChoice(MergedBranch Branch, bool IsAll);

interface IMergedBranchesDlg
{
    Result<MergedBranchChoice> Show(IReadOnlyList<MergedBranch> branches, int selectedIndex = 0);
}

// The local branches whose work is in main or an integration branch already, one row each, see
// MergedBranchRows, in a ListDlg: it returns what to delete and closes, and the command deletes it
// and shows the rest again.
class MergedBranchesDlg : IMergedBranchesDlg
{
    const int maxWidth = 110;

    public Result<MergedBranchChoice> Show(IReadOnlyList<MergedBranch> branches, int selectedIndex = 0) =>
        new ListDlg<MergedBranch, MergedBranchChoice>(
            "Delete Merged Branches",
            MergedBranchRows.MinWidth,
            maxWidth,
            MergedBranchRows.Header,
            MergedBranchRows.Row,
            MergedBranchRows.Reason
        )
            .Action("_Delete", b => new MergedBranchChoice(b, false), MergedBranchRows.CanDelete)
            .Action(
                "Delete _All ...",
                b => new MergedBranchChoice(b, true),
                _ => branches.Any(MergedBranchRows.CanDelete)
            )
            .Show(branches, selectedIndex);
}
