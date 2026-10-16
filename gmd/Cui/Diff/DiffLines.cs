using gmd.Server;

namespace gmd.Cui.Diff;

// The changed lines of a file a command acts on, as the server is given them
record FileLines(string Path, IReadOnlyList<ChosenLine> Lines)
{
    public override string ToString() => $"{Path}: {Lines.Count} lines";
}

// Which changed lines of the diff a command acts on: the rows selected, or else the change the
// cursor is on, which is the run of changed rows around it, its removed and added lines paired as
// the view shows them side by side and as git is given them (FilePatch). Index math with no view, so
// it is unit testable.
static class DiffLines
{
    // The rows of the change at the row, the run of changed rows around it, none on a row of no change
    public static (int First, int Count) ChangeAt(IReadOnlyList<DiffRow> rows, int index)
    {
        if (index < 0 || index >= rows.Count || !rows[index].IsChange)
            return (index, 0);

        var (first, last) = (index, index);
        while (first > 0 && rows[first - 1].IsChange)
            first--;
        while (last < rows.Count - 1 && rows[last + 1].IsChange)
            last++;
        return (first, last - first + 1);
    }

    // The changed lines of the rows, by the file they are in, in the order they are shown
    public static IReadOnlyList<FileLines> Of(DiffRows rows, int first, int count)
    {
        List<(string Path, ChosenLine Line)> lines = [];
        for (var i = first; i < first + count && i < rows.Count; i++)
        {
            var row = rows.Rows[i];
            if (!row.IsChange)
                continue;

            var path = rows.AnchorAt(i).FilePath;
            if (row.RemovedText is string removed)
                lines.Add((path, new ChosenLine(false, row.LeftLineNbr, removed)));
            if (row.AddedText is string added)
                lines.Add((path, new ChosenLine(true, row.RightLineNbr, added)));
        }

        return lines.GroupBy(l => l.Path).Select(g => new FileLines(g.Key, g.Select(l => l.Line).ToList())).ToList();
    }
}
