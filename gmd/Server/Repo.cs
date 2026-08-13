namespace gmd.Server;

record Repo
{
    public static readonly string TruncatedLogCommitId = "ffffffffffffffffffffffffffffffffffffffff";
    public static readonly string UncommittedId = "0000000000000000000000000000000000000000";
    public static readonly string EmptyRepoCommitId = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    public static readonly string UncommittedSid = UncommittedId.Sid();

    public Repo(
        string path,
        DateTime timeStamp,
        DateTime repoTimeStamp,
        IReadOnlyList<Commit> viewCommits,
        IReadOnlyList<Branch> viewBranches,
        IReadOnlyList<Commit> allCommits,
        IReadOnlyList<Branch> allBranches,
        IReadOnlyList<Stash> stashes,
        Status status,
        string filter
    )
    {
        Path = path;
        TimeStamp = timeStamp;
        RepoTimeStamp = repoTimeStamp;
        ViewCommits = viewCommits;
        ViewBranches = viewBranches;
        CommitById = allCommits.ToDictionary(c => c.Id, c => c);
        AllCommits = allCommits;
        BranchByName = allBranches.ToDictionary(b => b.Name, b => b);
        AllBranches = allBranches;
        Stashes = stashes;
        Status = status;
        Filter = filter;
    }

    public string Path { get; }
    public DateTime TimeStamp { get; }
    public DateTime RepoTimeStamp { get; }
    public IReadOnlyList<Commit> ViewCommits { get; init; }
    public IReadOnlyList<Branch> ViewBranches { get; init; }
    public IReadOnlyList<Commit> AllCommits { get; init; }
    public IReadOnlyList<Branch> AllBranches { get; init; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; init; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; init; }
    public IReadOnlyList<Stash> Stashes { get; }
    public Status Status { get; init; }
    public string Filter { get; }

    public static Repo Empty { get; } =
        new Repo(
            "",
            DateTime.MinValue,
            DateTime.MinValue,
            new List<Commit>(),
            new List<Branch>(),
            new List<Commit>(),
            new List<Branch>(),
            new List<Stash>(),
            Status.Empty,
            ""
        );

    public override string ToString() =>
        $"B:{ViewBranches.Count}/{AllBranches.Count}, C:{ViewCommits.Count}/{AllCommits.Count}, S:{Status} @{TimeStamp.IsoMs()} (@{RepoTimeStamp.IsoMs()})";
}

public record Commit(
    // Git Properties
    string Id,
    string Sid,
    string Subject,
    string Message,
    string Author,
    DateTime AuthorTime,
    // Augmented properties
    bool IsInView,
    int ViewIndex,
    int GitIndex,
    string BranchName,
    string BranchPrimaryName,
    string BranchNiceUniqueName,
    IReadOnlyList<string> ParentIds,
    IReadOnlyList<string> AllChildIds,
    IReadOnlyList<string> FirstChildIds,
    IReadOnlyList<string> MergeChildIds,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<string> BranchTips,
    bool IsCurrent,
    bool IsDetached,
    bool IsUncommitted,
    bool IsConflicted,
    bool IsAhead,
    bool IsBehind,
    bool IsTruncatedLogCommit,
    bool IsAmbiguous,
    bool IsAmbiguousTip,
    bool IsBranchSetByUser,
    bool HasStash,
    // View properties
    More More
)
{
    public override string ToString() => $"{Sid} {Subject} ({BranchName})";
}

public enum More
{
    None,
    MergeIn, // ╮
    BranchOut, // ╯
}

public record Branch(
    string Name,
    string PrimaryName,
    string PrimaryBaseName,
    string NiceName,
    string NiceNameUnique,
    string TipId,
    string BottomId,
    bool IsCurrent,
    bool IsLocalCurrent,
    bool IsRemote,
    string RemoteName,
    string LocalName,
    // Augmented properties
    bool IsInView,
    bool IsGitBranch,
    bool IsDetached,
    bool IsPrimary, // True if this is the primary branch (remote if local/remote pair or the local if only local)
    bool IsMainBranch,
    string ParentBranchName,
    string PullMergeParentBranchName,
    bool HasLocalOnly,
    bool HasRemoteOnly,
    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames,
    IReadOnlyList<string> PullMergeBranchNames,
    IReadOnlyList<string> AncestorNames,
    IReadOnlyList<string> RelatedBranchNames,
    bool IsCircularAncestors,
    // View properties
    int X,
    bool IsIn,
    bool IsOut
)
{
    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}

public record Tag(string Name, string CommitId);

public record Stash(string Id, string Name, string Branch, string ParentId, string IndexId, string Message);

// The Git layer's GitOperation / ConflictKind / ConflictedFile, as the UI sees them. Same shape,
// converted 1:1 by ViewRepoConverter, so nothing above this layer names a gmd.Git type.
public enum GitOperation
{
    None,
    Merge,
    CherryPick,
    Revert,
    Rebase,
    Am,
}

public enum ConflictKind
{
    BothModified, // UU
    BothAdded, // AA
    BothDeleted, // DD
    AddedByUs, // AU
    AddedByThem, // UA
    DeletedByThem, // UD
    DeletedByUs, // DU
}

// What is conflicted right now and what git is in the middle of, which is one status read rather
// than two — the operation is what names the resolver and says which side is which.
public record ConflictState(GitOperation Operation, IReadOnlyList<ConflictedFile> Files)
{
    public static readonly ConflictState None = new ConflictState(GitOperation.None, []);

    public override string ToString() => $"{Operation}: {Files.Count} conflicts";
}

public record ConflictedFile(string Path, ConflictKind Kind)
{
    public override string ToString() => $"{Kind} {Path}";
}

public record Status(
    int Modified,
    int Added,
    int Deleted,
    int Conflicted,
    int Renamed,
    GitOperation Operation,
    string MergeMessage,
    string MergeHeadId,
    string OperationBranchName,
    int OperationStep,
    int OperationTotal,
    string[] ModifiedFiles,
    string[] AddedFiles,
    string[] DeletedFiles,
    ConflictedFile[] Conflicts,
    string[] RenamedSourceFiles,
    string[] RenamedTargetFiles
)
{
    internal bool IsOk => ChangesCount == 0 && !IsMerging;
    internal int ChangesCount => Modified + Added + Deleted + Conflicted + Renamed;

    // See the note on the Git layer's Status: this is any operation in progress, not just a merge
    public bool IsMerging => Operation != GitOperation.None;

    public string[] ConflictsFiles => Conflicts.Select(c => c.Path).ToArray();

    public static Status Empty { get; } =
        new Status(0, 0, 0, 0, 0, GitOperation.None, "", "", "", 0, 0, [], [], [], [], [], []);

    public override string ToString() => $"M:{Modified},A:{Added},D:{Deleted},C:{Conflicted},R:{Renamed}";
}

// A conflicted file as the resolver shows it. Narrower than the Git layer's own model: the marker
// lines, the BOM and the per line terminators of the file are write side concerns that stay down
// there, so nothing here can get them wrong. What comes up is what is drawn.
public record FileLine(string Text)
{
    public override string ToString() => Text;
}

// What the user chose for one conflict. None means it is still a conflict.
public enum HunkChoice
{
    None,
    Ours,
    Theirs,
    OursThenTheirs,
    TheirsThenOurs,
    Base,
    Manual,
}

// One conflict region. The labels are the names git wrote into the markers, e.g. 'HEAD' and
// 'topic', which is what the view titles its columns with — during a rebase 'ours' and 'theirs'
// mean the opposite of what is expected, and these do not.
public record ConflictHunk(
    int Index,
    string OursLabel,
    string BaseLabel,
    string TheirsLabel,
    IReadOnlyList<FileLine> Ours,
    IReadOnlyList<FileLine> Base,
    IReadOnlyList<FileLine> Theirs
)
{
    public bool HasBase => Base.Count > 0;

    public override string ToString() => $"{Index}: {OursLabel} vs {TheirsLabel}";
}

// Either text that is not in dispute, or a conflict
public record ConflictSegment(IReadOnlyList<FileLine> Lines, ConflictHunk? Hunk)
{
    public override string ToString() => Hunk?.ToString() ?? $"{Lines.Count} lines";
}

public record ConflictFile(
    string Path,
    ConflictKind Kind,
    bool IsBinary,
    IReadOnlyList<ConflictSegment> Segments,
    // Which sides the index holds. A file one side deleted has only the other, and a rename against
    // a rename leaves a path with neither — so what the resolver can offer is decided from these
    // rather than guessed from the kind.
    bool HasOurs,
    bool HasTheirs,
    // Whether the file has a common ancestor at all, which is only answered when one is asked for
    // (GetConflictFileAsync with isWithBase). False from such a call means both sides created the
    // file — not the same as a hunk's HasBase, which is whether that one region had lines in it.
    bool HasBase = false
)
{
    public IReadOnlyList<ConflictHunk> Hunks => Segments.Select(s => s.Hunk).OfType<ConflictHunk>().ToList();

    public override string ToString() => $"{Path} ({Kind}): {Hunks.Count} conflicts";
}

// What the user decided for one conflict, which is all that goes back down. The file itself is not
// sent back: the Git layer re-reads it and applies these by position, so there is nothing to
// convert in that direction and a file that changed on disk meanwhile is caught rather than
// silently resolved against the wrong conflicts.
public record HunkResolution(int Index, HunkChoice Choice, string ManualText = "")
{
    public override string ToString() => $"{Index}: {Choice}";
}

record CommitDiff(string Id, string Author, DateTime Time, string Message, IReadOnlyList<FileDiff> FileDiffs)
{
    public override string ToString() => $"Files: {FileDiffs.Count}";
};

record FileDiff(
    string PathBefore,
    string PathAfter,
    bool IsRenamed,
    bool IsBinary,
    DiffMode DiffMode,
    IReadOnlyList<SectionDiff> SectionDiffs
);

record SectionDiff(
    string ChangedIndexes,
    int LeftLine,
    int LeftCount,
    int RightLine,
    int RightCount,
    IReadOnlyList<LineDiff> LineDiffs
);

record LineDiff(DiffMode DiffMode, string Line);

// The blame of one file, i.e. which commit last changed each line. The commits are kept in a
// dictionary the lines reference by id, since a commit typically covers many consecutive lines.
record Blame(
    string Path,
    string Reference,
    IReadOnlyList<BlameLine> Lines,
    IReadOnlyDictionary<string, BlameCommit> CommitById
)
{
    public override string ToString() => $"{Path}: {Lines.Count} lines, {CommitById.Count} commits";
};

record BlameLine(string CommitId, int LineNbr, int OriginalLineNbr, string Text);

record BlameCommit(
    string Id,
    string Sid,
    string Author,
    string AuthorMail,
    DateTime AuthorTime,
    string Subject,
    bool IsUncommitted,
    bool IsBoundary, // The first commit of the history, so it has no previous version to blame
    string PreviousId, // The commit and path to blame to see the version before this commit
    string PreviousPath,
    string Path // The path the file had in this commit, which differs from Blame.Path if renamed
);

enum DiffMode
{
    DiffModified,
    DiffAdded,
    DiffRemoved,
    DiffSame,
    DiffConflicts,
    DiffConflictStart,
    DiffConflictBase,
    DiffConflictSplit,
    DiffConflictEnd,
}
