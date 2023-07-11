
namespace gmd.Server;

using AugmentedRepo = gmd.Server.Private.Augmented.Repo;


record Repo
{
    internal static readonly string TruncatedLogCommitID =
        gmd.Server.Private.Augmented.Repo.TruncatedLogCommitID;
    internal static readonly string UncommittedId =
        gmd.Server.Private.Augmented.Repo.UncommittedId;
    internal static readonly string UncommittedSid = UncommittedId.Sid();

    private readonly Private.Augmented.Repo repo;

    public Repo(
        DateTime timeStamp,
        AugmentedRepo augRepo,
        IReadOnlyList<Commit> commits,
        IReadOnlyList<Branch> branches,
        IReadOnlyList<Stash> stashes,
        Status status)
    {
        TimeStamp = timeStamp;
        this.repo = augRepo;
        Commits = commits;
        CommitById = commits.ToDictionary(c => c.Id, c => c);
        Branches = branches;
        Stashes = stashes;
        Status = status;
        BranchByName = branches.ToDictionary(b => b.Name, b => b);
    }

    public DateTime TimeStamp { get; }
    public DateTime RepoTimeStamp => repo.TimeStamp;
    public string Path => AugmentedRepo.Path;
    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyList<Branch> Branches { get; }
    public IReadOnlyList<Stash> Stashes { get; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; }
    public Status Status { get; init; }


    internal Private.Augmented.Repo AugmentedRepo => repo;

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status} @{TimeStamp.IsoMilli()} (@{repo.TimeStamp.IsoMilli()})";
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
    int Index,
    int GitIndex,
    string BranchName,
    string BranchCommonName,
    string BranchViewName,
    IReadOnlyList<string> ParentIds,
    IReadOnlyList<string> AllChildIds,
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
    string HeadBranchName,
    string CommonName,
    string HeadBaseName,
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
    bool IsGitBranch,
    bool IsDetached,
    bool IsSetAsParent,
    bool IsMainBranch,

    string ParentBranchName,
    string ParentBranchCommonName,
    string PullMergeParentBranchName,

    bool HasLocalOnly,
    bool HasRemoteOnly,
    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames,
    IReadOnlyList<string> PullMergeBranchNames,

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
    string parentId,
    string indexId,
    string Message
);

public record Status(
    int Modified,
    int Added,
    int Deleted,
    int Conflicted,
    bool IsMerging,
    string MergeMessage,
    string MergeHeadId,
    string[] ModifiedFiles,
    string[] AddedFiles,
    string[] DeletedFiles,
    string[] ConflictsFiles
)
{
    internal bool IsOk => ChangesCount == 0 && !IsMerging;
    internal int ChangesCount => Modified + Added + Deleted + Conflicted;

    public override string ToString() => $"M:{Modified},A:{Added},D:{Deleted},C:{Conflicted}";
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