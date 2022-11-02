
using gmd.Utils.Git;

namespace gmd.ViewRepos;

class ViewRepo
{
    public ViewRepo(IReadOnlyList<Commit> commits)
    {
        Commits = commits;
    }

    public IReadOnlyList<Commit> Commits { get; }
}