
namespace gmd.Server;


record Repo
{
    public static readonly string TruncatedLogCommitID = "ffffffffffffffffffffffffffffffffffffffff";
    public static readonly string UncommittedId = "0000000000000000000000000000000000000000";
    public static readonly string EmptyRepoCommit = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    public static readonly string UncommittedSid = UncommittedId.Sid();



    public Repo(
        string path,
        DateTime timeStamp,
        DateTime repoTimeStamp,
        IReadOnlyList<Commit> viewCommits,
        IReadOnlyList<Branch> viewBranches,
        IReadOnlyDictionary<string, Commit> commitById,
        IReadOnlyDictionary<string, Branch> branchByName,
        IReadOnlyList<Stash> stashes,
        Status status,
        string filter)
    {
        Path = path;
        TimeStamp = timeStamp;
        RepoTimeStamp = repoTimeStamp;
        ViewCommits = viewCommits;
        ViewBranches = viewBranches;
        CommitById = viewCommits.ToDictionary(c => c.Id, c => c);
        BranchByName = viewBranches.ToDictionary(b => b.Name, b => b);
        AllBranches = branchByName.Values.ToList();
        Stashes = stashes;
        Status = status;
        Filter = filter;
    }

    public string Path { get; }
    public DateTime TimeStamp { get; }
    public DateTime RepoTimeStamp { get; }
    public IReadOnlyList<Commit> ViewCommits { get; }
    public IReadOnlyList<Branch> ViewBranches { get; }
    public IReadOnlyList<Branch> AllBranches { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; }
    public IReadOnlyList<Stash> Stashes { get; }
    public Status Status { get; init; }
    public string Filter { get; }

    public static Repo Empty => new Repo(
        "",
        DateTime.UtcNow,
        DateTime.UtcNow,
        new List<Commit>(),
        new List<Branch>(),
        new Dictionary<string, Commit>(),
        new Dictionary<string, Branch>(),
        new List<Stash>(),
        new Status(0, 0, 0, 0, 0, false, "", "", new string[0], new string[0], new string[0], new string[0], new string[0], new string[0]),
        "");


    public override string ToString() => $"B:{ViewBranches.Count}, C:{ViewCommits.Count}, S:{Status} @{TimeStamp.IsoMs()} (@{RepoTimeStamp.IsoMs()})";
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
    More More)
{
    public override string ToString() => $"{Sid} {Subject} ({BranchName})";
}


public enum More
{
    None,
    MergeIn,    // ╮
    BranchOut,  // ╯
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
    bool IsPrimary,     // True if this is the primary branch (remote if local/remote pair or the local if only local) 
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
    bool IsOut)
{
    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}

public record Tag(string Name, string CommitId);

public record Stash(
    string Id,
    string Name,
    string Branch,
    string ParentId,
    string IndexId,
    string Message
);

public record Status(
    int Modified,
    int Added,
    int Deleted,
    int Conflicted,
    int Renamed,
    bool IsMerging,
    string MergeMessage,
    string MergeHeadId,
    string[] ModifiedFiles,
    string[] AddedFiles,
    string[] DeletedFiles,
    string[] ConflictsFiles,
    string[] RenamedSourceFiles,
    string[] RenamedTargetFiles
)
{
    internal bool IsOk => ChangesCount == 0 && !IsMerging;
    internal int ChangesCount => Modified + Added + Deleted + Conflicted + Renamed;

    public override string ToString() => $"M:{Modified},A:{Added},D:{Deleted},C:{Conflicted},R:{Renamed}";
}


record CommitDiff(
    string Id,
    string Author,
    DateTime Time,
    string Message,
    IReadOnlyList<FileDiff> FileDiffs
)
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

enum DiffMode
{
    DiffModified,
    DiffAdded,
    DiffRemoved,
    DiffSame,
    DiffConflicts,
    DiffConflictStart,
    DiffConflictSplit,
    DiffConflictEnd,
}