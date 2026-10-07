using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

interface IDeletedBranchesDlg
{
    Result<DeletedBranch> Show(IReadOnlyList<DeletedBranch> branches);
}

// The branches gmd deleted that can be brought back, one row each, see DeletedBranchRows, in a
// ListDlg, as the lost commits are: it returns the branch picked and closes, and the command restores it.
class DeletedBranchesDlg : IDeletedBranchesDlg
{
    const int maxWidth = 100;

    public Result<DeletedBranch> Show(IReadOnlyList<DeletedBranch> branches) =>
        new ListDlg<DeletedBranch, DeletedBranch>(
            "Restore Deleted Branch",
            DeletedBranchRows.MinWidth,
            maxWidth,
            DeletedBranchRows.Header,
            DeletedBranchRows.Row,
            DeletedBranchRows.Reason
        )
            .Action("_Restore", b => b)
            .Show(branches);
}
