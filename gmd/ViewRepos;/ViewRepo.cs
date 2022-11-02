
namespace gmd.ViewRepos;

class Repo
{
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
    More More,

    bool IsCurrent,
    bool IsUncommitted,
    bool IsLocalOnly,
    bool IsRemoteOnly,
    bool IsPartialLogCommit,
    bool IsAmbiguous,
    bool IsAmbiguousTip);



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
    string TipID,
    bool IsCurrent,
    bool IsRemote,
    string RemoteName,


    bool IsGitBranch,
    bool IsDetached,
    bool IsAmbiguousBranch,
    bool IsSetAsParent,
    bool IsMainBranch,

    int AheadCount,
    int BehindCount,
    bool HasLocalOnly,
    bool HasRemoteOnly,

    int X,
    string AmbiguousTipId,
    IReadOnlyList<string> AmbiguousBranchNames,

    bool IsIn,
    bool IsOut);


