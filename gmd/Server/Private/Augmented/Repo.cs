namespace gmd.Server.Private.Augmented;


record Repo
{
    internal static readonly string PartialLogCommitID = "ffffffffffffffffffffffffffffffffffffffff";
    internal static readonly string UncommittedId = "0000000000000000000000000000000000000000";

    public Repo(
        DateTime timeStamp,
        string path,
        IReadOnlyList<Commit> commits,
        IReadOnlyList<Branch> branches,
        Status status)
    {
        TimeStamp = timeStamp;
        Path = path;
        Commits = commits;
        CommitById = commits.ToDictionary(c => c.Id, c => c);
        Branches = branches;
        Status = status;
        BranchByName = branches.ToDictionary(b => b.Name, b => b);
    }

    public DateTime TimeStamp { get; }
    public string Path { get; }
    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyList<Branch> Branches { get; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; }
    public Status Status { get; init; }

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status}";
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
    bool IsBranchSetByUser)
{
    public override string ToString() => $"{Sid} {Subject} ({BranchName})";
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

    string ParentBranchName,
    string PullMergeBranchName,

    bool IsGitBranch,
    bool IsDetached,
    bool IsSetAsParent,
    bool IsMainBranch,

    int AheadCount,
    int BehindCount,
    bool HasAheadCommits,
    bool HasBehindCommits,

    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames,
    IReadOnlyList<string> PullMergeBranchNames)
{
    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}

public record Tag(string Name, string CommitId);