
using gmd.Utils.Git;

namespace gmd.ViewRepos.Private.Augmented;


class Repo
{
    public Repo(IReadOnlyList<Commit> commits)
    {
        Commits = commits;
    }

    public IReadOnlyList<Commit> Commits { get; }
}