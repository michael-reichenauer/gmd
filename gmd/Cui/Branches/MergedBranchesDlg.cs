using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Branches;

// What Delete Merged Branches was asked to do: delete the branch picked, or every one it can
record MergedBranchChoice(MergedBranch Branch, bool IsAll);

interface IMergedBranchesDlg
{
    Result<MergedBranchChoice> Show(Repo repo, IReadOnlyList<MergedBranch> branches, int selectedIndex = 0);
}

// The branches whose work is in main or an integration branch already, one row each, see
// MergedBranchRows, in a ListDlg: it returns what to delete and closes, and the command deletes it
// and shows the rest again. A branch's name is in its color in the log.
class MergedBranchesDlg : IMergedBranchesDlg
{
    const int maxWidth = 130;

    readonly IBranchColorService branchColorService;

    public MergedBranchesDlg(IBranchColorService branchColorService)
    {
        this.branchColorService = branchColorService;
    }

    public Result<MergedBranchChoice> Show(Repo repo, IReadOnlyList<MergedBranch> branches, int selectedIndex = 0) =>
        new ListDlg<MergedBranch, MergedBranchChoice>(
            "Delete Merged Branches",
            MergedBranchRows.MinWidth,
            maxWidth,
            MergedBranchRows.Header,
            (b, width) => MergedBranchRows.Row(b, ColorOf(repo, b), width),
            MergedBranchRows.Reason
        )
            .Action("_Delete", b => new MergedBranchChoice(b, false), MergedBranchRows.CanDelete)
            .Action(
                "Delete _All ...",
                b => new MergedBranchChoice(b, true),
                _ => branches.Any(MergedBranchRows.CanDelete)
            )
            .Show(branches, selectedIndex);

    // The color of the branch in the log, origin's when it has one, as the pair's is drawn by it
    Color ColorOf(Repo repo, MergedBranch merged) =>
        repo.BranchByName.TryGetValue(merged.IsOnOrigin ? merged.RemoteName : merged.LocalName, out var branch)
            ? branchColorService.GetColor(repo, branch)
            : Color.Cyan;
}
