namespace gmd.Server.Private.Augmented;


record Repo
{
    public Repo(
        DateTime timeStamp,
        string path,
        IReadOnlyList<Commit> commits,
        IReadOnlyDictionary<string, Branch> branches,
        IReadOnlyList<Stash> stashes,
        Status status)
    {
        TimeStamp = timeStamp;
        Path = path;
        Commits = commits;
        CommitById = commits.ToDictionary(c => c.Id, c => c);
        Stashes = stashes;
        Status = status;
        Branches = branches;
    }

    public DateTime TimeStamp { get; }
    public string Path { get; }
    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyList<Stash> Stashes { get; }
    public IReadOnlyDictionary<string, Branch> Branches { get; }
    public Status Status { get; init; }

    public static Repo Empty => new Repo(
        DateTime.UtcNow,
        "",
        new List<Commit>(),
        new Dictionary<string, Branch>(),
        new List<Stash>(),
        new Status(0, 0, 0, 0, 0, false, "", "", new string[0], new string[0], new string[0], new string[0], new string[0], new string[0]));

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status} @{TimeStamp.IsoMs()}";
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
    bool HasStash)
{
    public override string ToString() => $"{Sid} {Subject} ({BranchName})";
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

    string ParentBranchName,
    string PullMergeParentBranchName,

    bool IsGitBranch,
    bool IsDetached,
    bool IsPrimary,
    bool IsMainBranch,
    bool IsCircularAncestors,

    bool HasAheadCommits,
    bool HasBehindCommits,

    string AmbiguousTipId,
    IReadOnlyList<string> RelatedBranchNames,
    IReadOnlyList<string> AmbiguousBranchNames,
    IReadOnlyList<string> PullMergeBranchNames,
    IReadOnlyList<string> AncestorNames)
{
    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}
