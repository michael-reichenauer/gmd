

using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;
using GitStatus = gmd.Utils.Git.Status;

namespace gmd.ViewRepos.Private.Augmented.Private;

class GitRepo
{
    internal GitRepo(
        DateTime timeStamp,
        string path,
        IReadOnlyList<GitCommit> commits,
        IReadOnlyList<GitBranch> branches,
        GitStatus status)
    {
        TimeStamp = timeStamp;
        Path = path;
        Commits = commits;
        Branches = branches;
        Status = status;
    }

    public DateTime TimeStamp { get; }
    public string Path { get; }
    internal IReadOnlyList<GitCommit> Commits { get; }
    public IReadOnlyList<GitBranch> Branches { get; }
    public GitStatus Status { get; }

    public override string ToString() => $"B:{Branches.Count}, C:{Commits.Count}, S:{Status}";
}