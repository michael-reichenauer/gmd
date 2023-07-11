namespace gmd.Server.Private.Augmented;


record Repo
{
    internal static readonly string TruncatedLogCommitID = "ffffffffffffffffffffffffffffffffffffffff";
    internal static readonly string UncommittedId = "0000000000000000000000000000000000000000";

    public Repo(
        DateTime timeStamp,
        string path,
        IReadOnlyList<Commit> commits,
        IReadOnlyList<Branch> branches,
        IReadOnlyList<Stash> stashes,
        Status status)
    {
        TimeStamp = timeStamp;
        Path = path;
        Commits = commits;
        CommitById = commits.ToDictionary(c => c.Id, c => c);
        Branches = branches;
        Stashes = stashes;
        Status = status;
        BranchByName = branches.ToDictionary(b => b.Name, b => b);
    }

    public DateTime TimeStamp { get; }
    public string Path { get; }
    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyList<Branch> Branches { get; }
    public IReadOnlyList<Stash> Stashes { get; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; }
    public Status Status { get; init; }

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status} @{TimeStamp.IsoMilli()}";
}

public record Commit(
    string Id,
    string Sid,
    string Subject,
    string Message,
    string Author,
    DateTime AuthorTime,
    int GitIndex,

    string BranchName,
    string BranchCommonName,
    string BranchViewName,
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
    bool IsBranchSetByUser)
{
    public override string ToString() => $"{Sid} {Subject} ({BranchName})";
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

    string ParentBranchName,
    string ParentBranchCommonName,
    string PullMergeParentBranchName,

    bool IsGitBranch,
    bool IsDetached,
    bool IsSetAsParent,
    bool IsMainBranch,

    bool HasAheadCommits,
    bool HasBehindCommits,

    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames,
    IReadOnlyList<string> PullMergeBranchNames)
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