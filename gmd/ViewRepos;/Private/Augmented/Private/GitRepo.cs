
using gmd.Utils.Git;

namespace gmd.ViewRepos.Private.Augmented.Private;

class GitRepo
{
    internal GitRepo(IReadOnlyList<Commit> commits)
    {
        Commits = commits;
    }

    internal IReadOnlyList<Commit> Commits { get; }
}