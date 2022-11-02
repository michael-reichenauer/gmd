namespace gmd.ViewRepos.Private.Augmented.Private;

using GitCommit = gmd.Utils.Git.Commit;
using GitBranch = gmd.Utils.Git.Branch;

class AugRepo
{
    public AugRepo(GitRepo gitRepo)
    {
        Commits = gitRepo.Commits.Select(c => new AugCommit(c)).ToList();
        Branches = gitRepo.Branches.Select(b => new AugBranch(b)).ToList();
    }


    internal IReadOnlyList<AugCommit> Commits { get; }
    internal IReadOnlyList<AugBranch> Branches { get; }
}

internal class AugCommit
{
    public readonly GitCommit C;

    public AugCommit(GitCommit gitCommit)
    {
        this.C = gitCommit;
    }
}

internal class AugBranch
{
    public readonly GitBranch B;

    public AugBranch(GitBranch gitBranch)
    {
        this.B = gitBranch;
    }
}

