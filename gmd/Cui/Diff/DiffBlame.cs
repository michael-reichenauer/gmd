using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Diff;

// Blames a file at a revision with the cursor on a line, which the blame view does when b is pressed
// in a diff. The blame view is in Blame/, above Diff/ in the one way order of the folders, so the diff
// names it by this, which Blame/ implements (Blame/DiffBlame.cs).
interface IDiffBlame
{
    // Says what failed itself, and returns once the blame is closed
    Task BlameAsync(Repo repo, BlameAt at);
}

// A file to blame, at a revision as git names it, "" for the working tree, and the line to put the
// cursor on
record BlameAt(string Path, string Reference, int LineNbr);

// What b blames in a diff: the file the cursor is in, in the version the cursor's line is of, at that
// line. A line the commit added or left as it was is of the commit's version, and a line it removed of
// the version before it, its first parent, which is what the diff is against; the path is the one the
// file had in that version, before or after a rename. A row that is no line, a file's header or the
// '⋯' between two parts, blames from the next line of its file. Plain, with no view, so it is unit
// testable.
static class DiffBlameTarget
{
    public static Result<BlameAt> Of(DiffRows rows, IReadOnlyList<CommitDiff> diffs, DiffSides? sides, int index)
    {
        var anchor = rows.AnchorAt(index);
        if (anchor.FilePath == "")
            return new Notice("No file here to blame: b blames the file the cursor is in");

        // A diff of one commit has no row with its id, a file or a line history a row per commit
        var commitId =
            anchor.CommitId != "" ? anchor.CommitId
            : diffs.Count == 1 ? diffs[0].Id
            : "";
        var file = diffs
            .FirstOrDefault(d => d.Id == commitId)
            ?.FileDiffs.FirstOrDefault(f => f.PathAfter == anchor.FilePath);
        if (file == null)
            return new Notice("No file here to blame: b blames the file the cursor is in");
        if (file.IsBinary)
            return new Notice($"'{Path.GetFileName(file.PathAfter)}' is binary, with no lines to blame");

        var revisions = commitId != "" ? new DiffSides($"{commitId}^", commitId) : sides;
        if (revisions == null)
            return new Notice("This diff is of no commit or working tree to blame a file in");

        if (NextLine(rows, index) is not DiffRow row)
            return new BlameAt(file.PathAfter, revisions.New, 1);
        var isRemoved = row.RightLineNbr == 0;
        return isRemoved
            ? new BlameAt(file.PathBefore, revisions.Old, row.LeftLineNbr)
            : new BlameAt(file.PathAfter, revisions.New, row.RightLineNbr);
    }

    // The row of the cursor's line, or of the next line of its file, null when there is none before
    // the next file or commit
    static DiffRow? NextLine(DiffRows rows, int index)
    {
        for (var i = index; i < rows.Count; i++)
        {
            var row = rows.Rows[i];
            if (i > index && (row.FilePath != "" || row.CommitId != ""))
                return null;
            if (row.LineNbr != 0)
                return row;
        }
        return null;
    }
}
