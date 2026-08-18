namespace gmd.Cui.RepoView;

// The hoover is the branch, or the commit, that the mouse pointer or the cursor is currently on.
// It is what most keys act on: with a branch hoovered, 'm' opens the menu of that branch, 'e'
// merges it and 's' switches to it, while with no branch hoovered the same keys act on the current
// commit instead.
//
// This class is that state and the index math of moving it, i.e. no drawing and no commands, so it
// can be tested without a terminal. The view side is in RepoViewInput, which redraws whenever one
// of the mutating methods here reports that the hoover actually moved.
class Hoover
{
    // The branch the hoover is on, or "" when the hoover is on a commit. The primary name is used
    // since a branch and its remote are drawn as one branch in the graph.
    public string BranchPrimaryName { get; private set; } = "";
    public string BranchName { get; private set; } = "";

    // The hoovered position: the row is a commit index, the column is the graph x of the branch,
    // or just right of the graph when a commit is hoovered.
    public int RowIndex { get; private set; } = -1;
    public int ColumnIndex { get; private set; }

    // The current row at the time the hoover was set, so that the current row having moved away
    // from under the hoover can be noticed while drawing, see FollowCurrentIndex().
    public int CurrentCommitIndex { get; private set; } = -1;

    public bool IsBranch => BranchPrimaryName != "";

    // Moves the hoover to a branch. Returns true if it moved, i.e. if the view needs a redraw.
    public bool SetBranch(GraphBranch branch, int rowIndex, int currentCommitIndex)
    {
        if (BranchPrimaryName == branch.B.PrimaryName && rowIndex == RowIndex && branch.X * 2 == ColumnIndex)
            return false;

        BranchPrimaryName = branch.B.PrimaryName;
        BranchName = branch.B.Name;
        RowIndex = rowIndex;
        ColumnIndex = branch.X * 2;
        CurrentCommitIndex = currentCommitIndex;
        return true;
    }

    // Moves the hoover to a commit, i.e. off any branch.
    public void SetCommit(int rowIndex, int columnIndex, int currentCommitIndex)
    {
        BranchPrimaryName = "";
        BranchName = "";
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        CurrentCommitIndex = currentCommitIndex;
    }

    // Clears the hoover. Returns true if there was one, i.e. if the view needs a redraw.
    public bool Clear()
    {
        if (BranchPrimaryName == "" && RowIndex == -1 && CurrentCommitIndex == -1 && ColumnIndex == -1)
            return false;

        BranchPrimaryName = "";
        BranchName = "";
        RowIndex = -1;
        ColumnIndex = -1;
        CurrentCommitIndex = -1;
        return true;
    }

    // The column of the hoovered branch among the branches of a row, or -1 if it is not one of
    // them (or no branch is hoovered).
    public int ColumnOf(IReadOnlyList<GraphBranch> rowBranches) =>
        IsBranch ? rowBranches.FindIndexBy(b => b.B.PrimaryName == BranchPrimaryName) : -1;

    // The branch to hoover when moving left along a row: the branch left of the hoovered one, the
    // right most branch when no branch is hoovered, and null when the left side is reached, since
    // moving left off the graph does nothing.
    public GraphBranch? NextLeft(IReadOnlyList<GraphBranch> rowBranches)
    {
        var column = ColumnOf(rowBranches);
        if (column < 0)
            return rowBranches[^1]; // No hoover branch, or not found, select right most branch
        if (column > 0)
            return rowBranches[column - 1]; // Hoover branch found, move to the left on this row

        return null; // Reached left side on this row
    }

    // The branch to hoover when moving right along a row, or null when the right side is reached
    // or the hoovered branch is gone, since moving right off the graph selects the commit instead.
    // The last column of the branch is used, so a branch drawn twice on a row is left behind.
    public GraphBranch? NextRight(IReadOnlyList<GraphBranch> rowBranches)
    {
        var column = rowBranches.FindLastIndexBy(b => b.B.PrimaryName == BranchPrimaryName);
        if (column == -1)
            return null; // Hoover branch not found, clear hoover and select commit
        if (column < rowBranches.Count - 1)
            return rowBranches[column + 1]; // Hoover branch found, move to the right on this row

        return null; // Reached right side
    }

    // The branch to hoover on the row moved to when moving up or down: the same branch if it is on
    // that row as well, otherwise whichever branch is closest to the column the hoover had, so the
    // hoover keeps following a straight line down the graph.
    public static GraphBranch Locate(IReadOnlyList<GraphBranch> rowBranches, string branchPrimaryName, int column)
    {
        var branch = rowBranches.FirstOrDefault(b => b.B.PrimaryName == branchPrimaryName);
        return branch ?? rowBranches[Math.Max(0, Math.Min(column, rowBranches.Count - 1))];
    }

    // Called while drawing, when the current row may have moved without the hoover following it,
    // e.g. because a command scrolled the view. The hoovered branch is given up if it is not on
    // the new row, but the hoover row follows either way, so the new row stays hoovered. Returns
    // true if the branch was given up, i.e. if the view needs a redraw.
    public bool FollowCurrentIndex(int currentIndex, IReadOnlyList<GraphBranch> rowBranches)
    {
        if (!IsBranch || CurrentCommitIndex == currentIndex)
            return false;

        var isCleared = false;
        if (!rowBranches.Any(b => b.B.PrimaryName == BranchPrimaryName))
            isCleared = Clear();

        RowIndex = currentIndex;
        return isCleared;
    }

    public override string ToString() =>
        IsBranch ? $"({ColumnIndex},{RowIndex}) {BranchPrimaryName}" : $"({ColumnIndex},{RowIndex}) <commit>";
}
