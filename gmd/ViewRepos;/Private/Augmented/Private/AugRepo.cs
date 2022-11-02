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

    // Augmented properties
    public string BranchName { get; set; } = "";
    public List<string> ChildIds { get; } = new List<string>();
    public List<Tag> Tags { get; } = new List<Tag>();
    public List<string> BranchTips { get; } = new List<string>();
    public bool IsCurrent { get; set; }
    public bool IsUncommitted { get; set; }
    public bool IsLocalOnly { get; set; }
    public bool IsRemoteOnly { get; set; }
    public bool IsPartialLogCommit { get; set; }
    public bool IsAmbiguous { get; set; }
    public bool IsAmbiguousTip { get; set; }

    public AugCommit(GitCommit gitCommit)
    {
        this.C = gitCommit;
    }
}

internal class AugBranch
{
    public readonly GitBranch B;

    // Augmented properties
    public bool IsGitBranch { get; set; }
    public bool IsAmbiguousBranch { get; set; }
    public bool IsSetAsParent { get; set; }
    public bool IsMainBranch { get; set; }
    public bool HasLocalOnly { get; set; }
    public bool HasRemoteOnly { get; set; }

    public string AmbiguousTipId { get; set; } = "";
    public List<string> AmbiguousBranchNames { get; } = new List<string>();

    public AugBranch(GitBranch gitBranch)
    {
        this.B = gitBranch;
    }
}

