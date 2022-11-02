
using gmd.Utils.Git;

namespace gmd.ViewRepos.Private.Augmented;


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
     string[] ParentIds,
     string Subject,
     string Message,
     string Author,
     DateTime AuthorTime,
     DateTime CommitTime);

public record Branch(
    string Name,
    string DisplayName,
    string TipID,
    bool IsCurrent,
    bool IsRemote,
    string RemoteName,
    bool IsDetached,
    int AheadCount,
    int BehindCount,
    bool IsRemoteMissing);
