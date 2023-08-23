namespace gmd.Server.Private;

interface IConverter
{
    IReadOnlyList<Commit> ToCommits(IEnumerable<Commit> commits);

    CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff);
    CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs);
}


class Converter : IConverter
{
    public IReadOnlyList<Commit> ToCommits(IEnumerable<Commit> commits) =>
       commits.Select(ToCommit).ToList();


    public CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs) =>
        gitCommitDiffs.Select(ToCommitDiff).ToArray();

    public CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff)
    {
        var d = gitCommitDiff;
        return new CommitDiff(d.Id, d.Author, d.Time, d.Message, ToFileDiffs(d.FileDiffs));
    }

    Commit ToCommit(Commit c, int index = -1) => c with
    {
        IsView = true,
        ViewIndex = index != -1 ? index : c.GitIndex
    };

    static IReadOnlyList<FileDiff> ToFileDiffs(IReadOnlyList<Git.FileDiff> fileDiffs) =>
        fileDiffs
            .Select(d => new FileDiff(d.PathBefore, d.PathAfter, d.IsRenamed, d.IsBinary,
                ToDiffMode(d.DiffMode), ToSectionDiffs(d.SectionDiffs)))
            .ToList();


    private static IReadOnlyList<SectionDiff> ToSectionDiffs(IReadOnlyList<Git.SectionDiff> sectionDiffs) =>
        sectionDiffs
            .Select(d => new SectionDiff(d.ChangedIndexes, d.LeftLine, d.LeftCount,
                d.RightLine, d.RightCount, ToLineDiffs(d.LineDiffs)))
            .ToList();

    private static IReadOnlyList<LineDiff> ToLineDiffs(IReadOnlyList<Git.LineDiff> lineDiffs) =>
        lineDiffs.Select(d => new LineDiff(ToDiffMode(d.DiffMode), d.Line)).ToList();


    private static DiffMode ToDiffMode(Git.DiffMode diffMode)
    {
        switch (diffMode)
        {
            case Git.DiffMode.DiffAdded:
                return DiffMode.DiffAdded;
            case Git.DiffMode.DiffConflictEnd:
                return DiffMode.DiffConflictEnd;
            case Git.DiffMode.DiffConflicts:
                return DiffMode.DiffConflicts;
            case Git.DiffMode.DiffConflictSplit:
                return DiffMode.DiffConflictSplit;
            case Git.DiffMode.DiffConflictStart:
                return DiffMode.DiffConflictStart;
            case Git.DiffMode.DiffModified:
                return DiffMode.DiffModified;
            case Git.DiffMode.DiffRemoved:
                return DiffMode.DiffRemoved;
            case Git.DiffMode.DiffSame:
                return DiffMode.DiffSame;
        }

        Asserter.FailFast($"Unknown diff mode: {diffMode}");
        return DiffMode.DiffModified;
    }
}