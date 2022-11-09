

using gmd.ViewRepos;

namespace gmd.Cui;


record DiffRows(IReadOnlyList<DiffRow> Rows);
record DiffRow(string Left, string Right);



interface IDiffService
{
    DiffRows CreateRows(CommitDiff diff);
    DiffRows CreateRows(CommitDiff[] commitDiffs);
}

class DiffService : IDiffService
{
    public DiffRows CreateRows(CommitDiff commitDiff)
    {
        return CreateRows(new[] { commitDiff });
    }

    public DiffRows CreateRows(CommitDiff[] commitDiffs)
    {
        return new DiffRows(commitDiffs.SelectMany(d => ToCommitRows(d)).ToList());
    }

    IReadOnlyList<DiffRow> ToCommitRows(CommitDiff d)
    {
        var rows = new List<DiffRow>();

        rows.Add(new DiffRow($"Commit:  {d.Id}", d.Id));
        rows.Add(new DiffRow($"Author:  {d.Author}", ""));
        rows.Add(new DiffRow($"Date:    {d.Date}", ""));
        rows.Add(new DiffRow($"Message: {d.Message}", ""));

        return rows;
    }
}
