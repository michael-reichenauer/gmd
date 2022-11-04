
namespace gmd.ViewRepos;

using AugmentedRepo = gmd.ViewRepos.Private.Augmented.Repo;


class Repo
{
    internal static readonly string PartialLogCommitID =
        gmd.ViewRepos.Private.Augmented.Repo.PartialLogCommitID;

    private readonly Private.Augmented.Repo repo;

    public Repo(
        AugmentedRepo augRepo,
        IReadOnlyList<Commit> commits,
        IReadOnlyList<Branch> branches)
    {
        this.repo = augRepo;
        Commits = commits;
        CommitById = commits.ToDictionary(c => c.Id, c => c);
        Branches = branches;
        BranchByName = branches.ToDictionary(b => b.Name, b => b);
    }

    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyList<Branch> Branches { get; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; }

    public override string ToString() => $"b:{Branches.Count}, c:{Commits.Count}";
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
    IReadOnlyList<string> ParentIds,
    IReadOnlyList<string> ChildIds,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<string> BranchTips,

    bool IsCurrent,
    bool IsUncommitted,
    bool IsLocalOnly,
    bool IsRemoteOnly,
    bool IsPartialLogCommit,
    bool IsAmbiguous,
    bool IsAmbiguousTip,

    // View properties
    More More)
{
    public override string ToString() => $"{Sid} {Subject} ({BranchName})";
}



public record Tag(string name);

public enum More
{
    None,
    MergeIn,    // ╮
    BranchOut,  // ╯
}

public record Branch(
    string Name,
    string DisplayName,
    string TipId,
    string BottomId,
    bool IsCurrent,
    bool IsRemote,
    string RemoteName,
    string LocalName,

    // Augmented properties
    bool IsGitBranch,
    bool IsDetached,
    bool IsSetAsParent,
    bool IsMainBranch,

    string ParentBranchName,

    int AheadCount,
    int BehindCount,
    bool HasLocalOnly,
    bool HasRemoteOnly,
    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames,

    // View properties
    int X,
    bool IsIn,
    bool IsOut)
{
    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}
