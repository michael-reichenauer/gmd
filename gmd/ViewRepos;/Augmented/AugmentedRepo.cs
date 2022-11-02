
using gmd.Utils.Git;

namespace gmd.ViewRepos.Augmented;


class AugmentedRepo
{
    public AugmentedRepo(IReadOnlyList<Commit> commits)
    {
        Commits = commits;
    }

    public IReadOnlyList<Commit> Commits { get; }
}