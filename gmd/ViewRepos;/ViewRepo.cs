
namespace gmd.ViewRepos;

class Repo
{
    internal static readonly string PartialLogCommitID =
        gmd.ViewRepos.Private.Augmented.Repo.PartialLogCommitID;

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
    // Git Properties
    string Id,
    string Sid,
    string Subject,
    string Message,
    string Author,
    DateTime AuthorTime,

    // Augmented properties
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
    More More);



public record Tag(string name);

public enum More
{
    None,
    MergeIn,    // ╮
    BranchOut,  // ╯
}

public record Branch(
    // Git Properties
    string Name,
    string DisplayName,
    string TipID,
    bool IsCurrent,
    bool IsRemote,
    string RemoteName,

    // Augmented properties
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
    IReadOnlyList<string> AmbiguousBranchNames,

    // View properties
    int X,
    bool IsIn,
    bool IsOut);


