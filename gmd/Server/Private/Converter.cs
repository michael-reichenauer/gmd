namespace gmd.Server.Private;

interface IConverter
{
    IReadOnlyList<Commit> ToViewCommits(IEnumerable<Commit> commits);

    CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff);
    CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs);
    Repo ToViewRepo(DateTime timeStamp,
        IReadOnlyList<Commit> viewCommits, IReadOnlyList<Branch> viewBranches,
        string filter, Repo repo);
}


class Converter : IConverter
{
    public IReadOnlyList<Commit> ToViewCommits(IEnumerable<Commit> commits) =>
       commits.Select((c, i) => c with { IsInView = true, ViewIndex = i }).ToList();


    public CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs) =>
        gitCommitDiffs.Select(ToCommitDiff).ToArray();

    public CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff)
    {
        var d = gitCommitDiff;
        return new CommitDiff(d.Id, d.Author, d.Time, d.Message, ToFileDiffs(d.FileDiffs));
    }


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

    public Repo ToViewRepo(
        DateTime timeStamp,
        IReadOnlyList<Commit> viewCommits,
        IReadOnlyList<Branch> viewBranches,
        string filter,
        Repo repo)
    {
        // Copy and ensure commits and repo are by default not in view
        var allCommits = repo.AllCommits.Select(c => c with { IsInView = false, ViewIndex = -1 }).ToList();
        var allBranches = repo.AllBranches.Select(b => b with { IsInView = false }).ToList();

        // Crate index lookup for commits and branches
        var commitIndexById = new Dictionary<string, int>();
        var branchIndexByName = new Dictionary<string, int>();
        allCommits.ForEach((c, i) => commitIndexById[c.Id] = i);
        allBranches.ForEach((b, i) => branchIndexByName[b.Name] = i);

        // Set IsInView and ViewIndex for commits and branches in view and update commitsById and branchByName
        viewCommits = viewCommits.Select((c, i) => c with { IsInView = true, ViewIndex = i }).ToList();
        viewCommits.ForEach(c => allCommits[commitIndexById[c.Id]] = c);

        viewBranches = viewBranches.Select((b, i) => b with { IsInView = true }).ToList();
        viewBranches.ForEach(b => allBranches[branchIndexByName[b.Name]] = b);

        return new Repo(
              repo.Path,
              timeStamp,
              repo.TimeStamp,
              viewCommits,
              viewBranches,
              allCommits,
              allBranches,
              repo.Stashes,
              repo.Status,
              filter);
    }
}