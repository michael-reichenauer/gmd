using System.Diagnostics;
using gmd.ViewRepos;

namespace gmd.Cui;


interface IDiffService
{
    DiffRows CreateRows(CommitDiff diff);
    DiffRows CreateRows(CommitDiff[] commitDiffs);
}

class DiffRows
{
    readonly List<DiffRow> rows = new List<DiffRow>();

    public int RowCount => rows.Count;
    public IReadOnlyList<DiffRow> Rows => rows;

    internal void Add(Text oneRow) =>
        rows.Add(new DiffRow(oneRow, Text.None, DiffRowMode.Left));

    internal void Add(Text left, Text right) =>
        rows.Add(new DiffRow(left, right, DiffRowMode.LeftRight));

    internal void AddLine(Text line) =>
       rows.Add(new DiffRow(line, Text.None, DiffRowMode.Line));
}

record DiffRow(Text Left, Text Right, DiffRowMode Mode);
enum DiffRowMode
{
    LeftRight,
    Left,
    Line,
}


class DiffService : IDiffService
{
    public DiffRows CreateRows(CommitDiff commitDiff)
    {
        return CreateRows(new[] { commitDiff });
    }

    public DiffRows CreateRows(CommitDiff[] commitDiffs)
    {
        DiffRows rows = new DiffRows();
        commitDiffs.ForEach(diff => AddCommitDiff(diff, rows));
        return rows;
    }

    void AddCommitDiff(CommitDiff d, DiffRows rows)
    {
        rows.AddLine(Text.New.Yellow("═"));
        rows.Add(Text.New.DarkGray("Commit:  ").White(d.Id));
        rows.Add(Text.New.DarkGray("Author:  ").White(d.Author));
        rows.Add(Text.New.DarkGray("Date:    ").White(d.Date));
        rows.Add(Text.New.DarkGray("Message: ").White(d.Message));
        rows.Add(Text.None);

        rows.Add(Text.New.White($"{d.FileDiffs.Count} Files:"));
        GetFileNames(d).ForEach(txt => rows.Add(txt));
    }

    IReadOnlyList<Text> GetFileNames(CommitDiff commitDiff)
    {
        return commitDiff.FileDiffs.Select(fd => fd.IsRenamed
            ? ToColorText($"  {ToDiffModeText(fd.DiffMode),-12} {fd.PathBefore} => {fd.PathAfter}", fd.DiffMode)
            : ToColorText($"  {ToDiffModeText(fd.DiffMode),-12} {fd.PathAfter}", fd.DiffMode))
        .ToList();
    }

    Text ToColorText(string text, DiffMode diffMode)
    {
        switch (diffMode)
        {
            case DiffMode.DiffModified:
                return Text.New.White(text);
            case DiffMode.DiffAdded:
                return Text.New.Green(text);
            case DiffMode.DiffRemoved:
                return Text.New.Red(text);
            case DiffMode.DiffConflicts:
                return Text.New.BrightYellow(text);
        }

        throw (Asserter.FailFast($"Unknown diffMode {diffMode}"));
    }


    string ToDiffModeText(DiffMode diffMode)
    {
        switch (diffMode)
        {
            case DiffMode.DiffModified:
                return "Modified:";
            case DiffMode.DiffAdded:
                return "Added:";
            case DiffMode.DiffRemoved:
                return "Removed:";
            case DiffMode.DiffConflicts:
                return "Conflicted:";
        }

        throw (Asserter.FailFast($"Unknown diffMode {diffMode}"));
    }

}
