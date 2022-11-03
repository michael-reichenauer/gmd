
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
        Branches = branches;
    }

    public IReadOnlyList<Commit> Commits { get; }
    public IReadOnlyList<Branch> Branches { get; }
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
    bool IsAmbiguousTip);



public record Branch(
    string Name,
    string DisplayName,
    string TipID,
    bool IsCurrent,
    bool IsRemote,
    string RemoteName,
    string LocalName,

    bool IsGitBranch,
    bool IsDetached,
    bool IsAmbiguousBranch,
    bool IsSetAsParent,
    bool IsMainBranch,

    int AheadCount,
    int BehindCount,
    bool HasLocalOnly,
    bool HasRemoteOnly,

    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames);
