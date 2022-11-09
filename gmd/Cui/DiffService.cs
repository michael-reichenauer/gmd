using gmd.ViewRepos;

namespace gmd.Cui;


interface IDiffService
{
    Content CreateRows(CommitDiff diff);
    Content CreateRows(CommitDiff[] commitDiffs);
}

class DiffService : IDiffService
{
    public Content CreateRows(CommitDiff commitDiff)
    {
        return CreateRows(new[] { commitDiff });
    }

    public Content CreateRows(CommitDiff[] commitDiffs)
    {
        var content = new Content();
        commitDiffs.ForEach(diff => AddCommitDiff(diff, content));
        return content;
    }

    void AddCommitDiff(CommitDiff d, Content content)
    {
        content.DarkGray("Commit:  ");
        content.White(d.Id);
        content.EoL();
        content.DarkGray("Author:  ");
        content.White(d.Author);
        content.EoL();
        content.DarkGray("Date:    ");
        content.White(d.Date);
        content.EoL();
        content.DarkGray("Message: ");
        content.White(d.Message);
        content.EoL();
    }
}
