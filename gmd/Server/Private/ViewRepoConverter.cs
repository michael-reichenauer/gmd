namespace gmd.Server.Private;

// Converts into the view repo the UI renders, i.e. the augmented repo narrowed to the commits
// and branches the user chose to show. See WorkRepoConverter for the step before this one.
interface IViewRepoConverter
{
    IReadOnlyList<Commit> ToViewCommits(IEnumerable<Commit> commits);

    CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff);
    CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs);
    Blame ToBlame(Git.Blame gitBlame);
    ConflictFile ToConflictFile(Git.ConflictFile gitFile);
    Repo ToViewRepo(
        DateTime timeStamp,
        IReadOnlyList<Commit> viewCommits,
        IReadOnlyList<Branch> viewBranches,
        string filter,
        Repo repo
    );
}

class ViewRepoConverter : IViewRepoConverter
{
    public IReadOnlyList<Commit> ToViewCommits(IEnumerable<Commit> commits) =>
        commits.Select((c, i) => c with { IsInView = true, ViewIndex = i }).ToList();

    public CommitDiff[] ToCommitDiffs(Git.CommitDiff[] gitCommitDiffs) => gitCommitDiffs.Select(ToCommitDiff).ToArray();

    public CommitDiff ToCommitDiff(Git.CommitDiff gitCommitDiff)
    {
        var d = gitCommitDiff;
        return new CommitDiff(d.Id, d.Author, d.Time, d.Message, ToFileDiffs(d.FileDiffs));
    }

    // The Git layer's parsed file, narrowed to what is drawn: the marker lines and the per line
    // terminators are not carried up, since only the writing side has any use for them.
    public ConflictFile ToConflictFile(Git.ConflictFile f) =>
        new ConflictFile(
            f.Path,
            ToConflictKind(f.Kind),
            f.IsBinary,
            f.Segments.Select(ToConflictSegment).ToList(),
            f.HasOurs,
            f.HasTheirs,
            f.HasBase
        );

    static ConflictSegment ToConflictSegment(Git.ConflictSegment s) =>
        new ConflictSegment(ToFileLines(s.Lines), s.Hunk == null ? null : ToConflictHunk(s.Hunk));

    static ConflictHunk ToConflictHunk(Git.ConflictHunk h) =>
        new ConflictHunk(
            h.Index,
            h.OursLabel,
            h.BaseLabel,
            h.TheirsLabel,
            ToFileLines(h.Ours),
            ToFileLines(h.Base),
            ToFileLines(h.Theirs)
        );

    static IReadOnlyList<FileLine> ToFileLines(IReadOnlyList<Git.FileLine> lines) =>
        lines.Select(l => new FileLine(l.Text)).ToList();

    public static ConflictKind ToConflictKind(Git.ConflictKind kind) =>
        kind switch
        {
            Git.ConflictKind.BothModified => ConflictKind.BothModified,
            Git.ConflictKind.BothAdded => ConflictKind.BothAdded,
            Git.ConflictKind.BothDeleted => ConflictKind.BothDeleted,
            Git.ConflictKind.AddedByUs => ConflictKind.AddedByUs,
            Git.ConflictKind.AddedByThem => ConflictKind.AddedByThem,
            Git.ConflictKind.DeletedByThem => ConflictKind.DeletedByThem,
            Git.ConflictKind.DeletedByUs => ConflictKind.DeletedByUs,
            _ => throw Asserter.FailFast($"Unknown conflict kind {kind}"),
        };

    public static Git.ConflictKind ToGitConflictKind(ConflictKind kind) =>
        kind switch
        {
            ConflictKind.BothModified => Git.ConflictKind.BothModified,
            ConflictKind.BothAdded => Git.ConflictKind.BothAdded,
            ConflictKind.BothDeleted => Git.ConflictKind.BothDeleted,
            ConflictKind.AddedByUs => Git.ConflictKind.AddedByUs,
            ConflictKind.AddedByThem => Git.ConflictKind.AddedByThem,
            ConflictKind.DeletedByThem => Git.ConflictKind.DeletedByThem,
            ConflictKind.DeletedByUs => Git.ConflictKind.DeletedByUs,
            _ => throw Asserter.FailFast($"Unknown conflict kind {kind}"),
        };

    public static Git.HunkChoice ToGitChoice(HunkChoice choice) =>
        choice switch
        {
            HunkChoice.None => Git.HunkChoice.None,
            HunkChoice.Ours => Git.HunkChoice.Ours,
            HunkChoice.Theirs => Git.HunkChoice.Theirs,
            HunkChoice.OursThenTheirs => Git.HunkChoice.OursThenTheirs,
            HunkChoice.TheirsThenOurs => Git.HunkChoice.TheirsThenOurs,
            HunkChoice.Base => Git.HunkChoice.Base,
            HunkChoice.Manual => Git.HunkChoice.Manual,
            _ => throw Asserter.FailFast($"Unknown choice {choice}"),
        };

    public Blame ToBlame(Git.Blame gitBlame)
    {
        var b = gitBlame;
        return new Blame(b.Path, b.Reference, ToBlameLines(b.Lines), ToBlameCommits(b.CommitById));
    }

    static IReadOnlyList<BlameLine> ToBlameLines(IReadOnlyList<Git.BlameLine> lines) =>
        lines.Select(l => new BlameLine(l.CommitId, l.LineNbr, l.OriginalLineNbr, l.Text)).ToList();

    static IReadOnlyDictionary<string, BlameCommit> ToBlameCommits(
        IReadOnlyDictionary<string, Git.BlameCommit> commits
    ) =>
        commits.ToDictionary(
            p => p.Key,
            p => new BlameCommit(
                p.Value.Id,
                p.Value.Sid,
                p.Value.Author,
                p.Value.AuthorMail,
                p.Value.AuthorTime,
                p.Value.Subject,
                p.Value.IsUncommitted,
                p.Value.IsBoundary,
                p.Value.PreviousId,
                p.Value.PreviousPath,
                p.Value.Path
            )
        );

    static IReadOnlyList<FileDiff> ToFileDiffs(IReadOnlyList<Git.FileDiff> fileDiffs) =>
        fileDiffs
            .Select(d => new FileDiff(
                d.PathBefore,
                d.PathAfter,
                d.IsRenamed,
                d.IsBinary,
                ToDiffMode(d.DiffMode),
                ToSectionDiffs(d.SectionDiffs)
            ))
            .ToList();

    private static IReadOnlyList<SectionDiff> ToSectionDiffs(IReadOnlyList<Git.SectionDiff> sectionDiffs) =>
        sectionDiffs
            .Select(d => new SectionDiff(
                d.ChangedIndexes,
                d.LeftLine,
                d.LeftCount,
                d.RightLine,
                d.RightCount,
                ToLineDiffs(d.LineDiffs)
            ))
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
            case Git.DiffMode.DiffConflictBase:
                return DiffMode.DiffConflictBase;
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
        Repo repo
    )
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
            filter
        );
    }
}
