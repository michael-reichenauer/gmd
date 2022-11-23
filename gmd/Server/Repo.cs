
namespace gmd.Server;

using AugmentedRepo = gmd.Server.Private.Augmented.Repo;


record Repo
{
    internal static readonly string PartialLogCommitID =
        gmd.Server.Private.Augmented.Repo.PartialLogCommitID;
    internal static readonly string UncommittedId =
        gmd.Server.Private.Augmented.Repo.UncommittedId;
    internal static readonly string UncommittedSid = UncommittedId.Substring(0, 6);

    private readonly Private.Augmented.Repo repo;

    public Repo(
        DateTime timeStamp,
        AugmentedRepo augRepo,
        IReadOnlyList<Commit> commits,
        IReadOnlyList<Branch> branches,
        Status status)
    {
        TimeStamp = timeStamp;
        this.repo = augRepo;
        Commits = commits;
        CommitById = commits.ToDictionary(c => c.Id, c => c);
        Branches = branches;
        Status = status;
        BranchByName = branches.ToDictionary(b => b.Name, b => b);
    }

    public DateTime TimeStamp { get; }
    public string Path => AugmentedRepo.Path;
    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyList<Branch> Branches { get; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; }
    public Status Status { get; init; }


    internal Private.Augmented.Repo AugmentedRepo => repo;

    public override string ToString() => $"b:{Branches.Count}, c:{Commits.Count}, S:{Status}";
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
    string BranchName,
    string BranchCommonName,
    IReadOnlyList<string> ParentIds,
    IReadOnlyList<string> ChildIds,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<string> BranchTips,

    bool IsCurrent,
    bool IsUncommitted,
    bool IsConflicted,
    bool IsAhead,
    bool IsBehind,
    bool IsPartialLogCommit,
    bool IsAmbiguous,
    bool IsAmbiguousTip,

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
    string CommonName,
    string DisplayName,
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
    string PullMergeBranchName,

    int AheadCount,
    int BehindCount,
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

public record Status(
    int Modified,
    int Added,
    int Deleted,
    int Conflicted,
    bool IsMerging,
    string MergeMessage,
    string[] ModifiedFiles,
    string[] AddedFiles,
    string[] DeleteddFiles,
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
    string Date,
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