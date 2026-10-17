namespace gmd.Server.Private;

// The boundary between git's model and gmd's: each record the Git layer reads is converted here
// into the server's own, which is all the layers above see, so that nothing above this layer names
// a gmd.Git type (see Layering in CLAUDE.md). Most are copies, member for member. The enums are
// converted by switches rather than casts, since a cast would keep compiling and start lying the day
// either one gains or reorders a member, and an unmapped member fails loudly instead.
//
// The view repo, which is what the UI draws, is made from these by ViewRepoConverter.
static class GitConverter
{
    public static Status ToStatus(Git.Status s) =>
        new Status(
            s.Modified,
            s.Added,
            s.Deleted,
            s.Conflicted,
            s.Renamed,
            ToOperation(s.Operation),
            s.MergeMessage,
            s.MergeHeadId,
            s.OperationBranchName,
            s.OperationStep,
            s.OperationTotal,
            s.IsFinishedByCommit,
            s.ModifiedFiles,
            s.AddedFiles,
            s.DeletedFiles,
            s.Conflicts.Select(ToConflictedFile).ToArray(),
            s.RenamedSourceFiles,
            s.RenamedTargetFiles
        );

    public static ConflictedFile ToConflictedFile(Git.ConflictedFile f) => new(f.Path, ToConflictKind(f.Kind));

    public static ConflictSides ToConflictSides(Git.ConflictSides s) =>
        new(s.OursBranch, s.OursSubject, s.TheirsSubject);

    public static GitOperation ToOperation(Git.GitOperation operation) =>
        operation switch
        {
            Git.GitOperation.None => GitOperation.None,
            Git.GitOperation.Merge => GitOperation.Merge,
            Git.GitOperation.CherryPick => GitOperation.CherryPick,
            Git.GitOperation.Revert => GitOperation.Revert,
            Git.GitOperation.Rebase => GitOperation.Rebase,
            Git.GitOperation.Am => GitOperation.Am,
            _ => throw Asserter.Fail($"Unknown operation {operation}"),
        };

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
            _ => throw Asserter.Fail($"Unknown conflict kind {kind}"),
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
            _ => throw Asserter.Fail($"Unknown conflict kind {kind}"),
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
            _ => throw Asserter.Fail($"Unknown choice {choice}"),
        };

    // Which worktree is the current one, and the changes of one, are the server's to know
    public static Worktree ToWorktree(Git.Worktree w, bool isCurrent, int changesCount) =>
        new(
            w.Path,
            w.Branch,
            w.HeadId,
            w.IsMain,
            isCurrent,
            w.IsDetached,
            w.IsLocked,
            w.LockReason,
            w.IsPrunable,
            w.PruneReason,
            changesCount
        );

    public static Stash ToStash(Git.Stash s) => new(s.Id, s.Name, s.Branch, s.ParentId, s.IndexId, s.Message);

    public static Tag ToTag(Git.Tag t) => new(t.Name, t.CommitId);

    public static CommitDiff ToCommitDiff(Git.CommitDiff d) =>
        new(d.Id, d.Author, d.Time, d.Message, d.FileDiffs.Select(ToFileDiff).ToList());

    public static CommitDiff[] ToCommitDiffs(Git.CommitDiff[] diffs) => diffs.Select(ToCommitDiff).ToArray();

    public static ChangedFile ToChangedFile(Git.ChangedFile f) =>
        new(f.Kind, f.Path, f.OldPath, f.Added, f.Deleted, f.IsBinary);

    static FileDiff ToFileDiff(Git.FileDiff d) =>
        new(
            d.PathBefore,
            d.PathAfter,
            d.IsRenamed,
            d.IsBinary,
            ToDiffMode(d.DiffMode),
            d.SectionDiffs.Select(ToSectionDiff).ToList()
        );

    static SectionDiff ToSectionDiff(Git.SectionDiff d) =>
        new(
            d.ChangedIndexes,
            d.LeftLine,
            d.LeftCount,
            d.RightLine,
            d.RightCount,
            d.LineDiffs.Select(l => new LineDiff(ToDiffMode(l.DiffMode), l.Line)).ToList()
        )
        {
            Function = d.Function,
        };

    static DiffMode ToDiffMode(Git.DiffMode mode) =>
        mode switch
        {
            Git.DiffMode.DiffModified => DiffMode.DiffModified,
            Git.DiffMode.DiffAdded => DiffMode.DiffAdded,
            Git.DiffMode.DiffRemoved => DiffMode.DiffRemoved,
            Git.DiffMode.DiffSame => DiffMode.DiffSame,
            Git.DiffMode.DiffConflicts => DiffMode.DiffConflicts,
            Git.DiffMode.DiffConflictStart => DiffMode.DiffConflictStart,
            Git.DiffMode.DiffConflictBase => DiffMode.DiffConflictBase,
            Git.DiffMode.DiffConflictSplit => DiffMode.DiffConflictSplit,
            Git.DiffMode.DiffConflictEnd => DiffMode.DiffConflictEnd,
            _ => throw Asserter.Fail($"Unknown diff mode: {mode}"),
        };

    // The Git layer's parsed file, narrowed to what is drawn: the marker lines and the per line
    // terminators are not carried up, since only the writing side has any use for them.
    public static ConflictFile ToConflictFile(Git.ConflictFile f) =>
        new(
            f.Path,
            ToConflictKind(f.Kind),
            f.IsBinary,
            f.Segments.Select(ToConflictSegment).ToList(),
            f.HasOurs,
            f.HasTheirs,
            f.HasBase
        );

    static ConflictSegment ToConflictSegment(Git.ConflictSegment s) =>
        new(ToFileLines(s.Lines), s.Hunk == null ? null : ToConflictHunk(s.Hunk));

    static ConflictHunk ToConflictHunk(Git.ConflictHunk h) =>
        new(
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

    // Git's id of the lines not committed yet is all zeros, 40 or 64 of them as the repository's
    // ids are long, and gmd's is Repo.UncommittedId whatever the repository, which is what routes
    // their diff to the uncommitted changes and finds them in the log
    public static Blame ToBlame(Git.Blame b)
    {
        string IdOf(string id) => b.CommitById.TryGetValue(id, out var c) && c.IsUncommitted ? Repo.UncommittedId : id;

        return new Blame(
            b.Path,
            b.Reference,
            b.Lines.Select(l => new BlameLine(IdOf(l.CommitId), l.LineNbr, l.OriginalLineNbr, l.Text)).ToList(),
            b.CommitById.ToDictionary(
                p => IdOf(p.Key),
                p => new BlameCommit(
                    IdOf(p.Value.Id),
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
            )
        );
    }
}
