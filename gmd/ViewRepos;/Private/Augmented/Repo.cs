
using gmd.Utils.Git;

namespace gmd.ViewRepos.Private.Augmented;


class Repo
{
    internal static readonly string PartialLogCommitID = "ffffffffffffffffffffffffffffffffffffffff";
    public Repo(
        IReadOnlyList<Commit> commits,
        IReadOnlyList<Branch> branches)
    {
        Commits = commits;
        CommitById = commits.ToDictionary(c => c.Id, c => c);
        Branches = branches;
        BranchByName = branches.ToDictionary(b => b.Name, b => b);
    }

    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyDictionary<string, Commit> CommitById { get; }
    public IReadOnlyList<Branch> Branches { get; }
    public IReadOnlyDictionary<string, Branch> BranchByName { get; }
}

public record Commit(
    string Id,
    string Sid,
    string Subject,
    string Message,
    string Author,
    DateTime AuthorTime,

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
    bool IsAmbiguousTip)
{
    public override string ToString() => $"{Sid} {Subject} ({BranchName})";
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

    string ParentBranchName,

    bool IsGitBranch,
    bool IsDetached,
    bool IsSetAsParent,
    bool IsMainBranch,

    int AheadCount,
    int BehindCount,
    bool HasLocalOnly,
    bool HasRemoteOnly,

    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames)
{
    public override string ToString() => IsRemote ? $"{Name}<-{LocalName}" : $"{Name}->{RemoteName}";
}
