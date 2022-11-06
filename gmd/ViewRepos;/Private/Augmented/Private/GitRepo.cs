

using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;
using GitStatus = gmd.Utils.Git.Status;

namespace gmd.ViewRepos.Private.Augmented.Private;

class GitRepo
{
    internal GitRepo(
        IReadOnlyList<GitCommit> commits,
        IReadOnlyList<GitBranch> branches,
        GitStatus status)
    {
        Commits = commits;
        Branches = branches;
        Status = status;
    }

    internal IReadOnlyList<GitCommit> Commits { get; }
    public IReadOnlyList<GitBranch> Branches { get; }
    public GitStatus Status { get; }
}