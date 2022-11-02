

using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;

namespace gmd.ViewRepos.Private.Augmented.Private;

class GitRepo
{
    internal GitRepo(
        IReadOnlyList<GitCommit> commits,
        IReadOnlyList<GitBranch> branches)
    {
        Commits = commits;
        Branches = branches;
    }

    internal IReadOnlyList<GitCommit> Commits { get; }
    public IReadOnlyList<GitBranch> Branches { get; }
}