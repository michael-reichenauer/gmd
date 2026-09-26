namespace gmd.Server.Private.Augmented.Private;

// The branch names the branch structure pipeline treats specially, shared by several of its stages.
static class WellKnownBranches
{
    // The names the trunk of a repository can have, in the order they are preferred. A branch with
    // one of these names is preferred both when a commit has several possible branches and when the
    // root branch of the repository is selected.
    public static readonly string[] MainNamePriority =
    [
        "origin/main",
        "main",
        "origin/master",
        "master",
        "origin/trunk",
        "trunk",
    ];

    // The names of the branches other branches are started from and merged back into, below the
    // trunk, e.g. git-flow's 'develop'. At a branch point such a branch is the one that goes on.
    static readonly string[] IntegrationNames = ["develop", "development", "dev"];

    // Whether a nice name is an integration branch's: one of the names above, also as the last part
    // of a longer name ('owner/develop'), or ending in '_develop' or '-develop', as a repo with one
    // per major version has them ('v2_develop')
    public static bool IsIntegrationName(string name)
    {
        var last = name[(name.LastIndexOf('/') + 1)..];
        return IntegrationNames.Contains(last)
            || last.EndsWith("_develop")
            || last.EndsWith("-develop")
            || last.EndsWith("_development")
            || last.EndsWith("-development");
    }

    // Whether a name, e.g. from a merge subject, is the trunk's or an integration branch's, also as
    // another remote's, e.g. 'upstream/main', or is a remote's name alone, as 'git merge upstream'
    // writes, which merges that remote's default branch
    public static bool IsTrunkOrIntegrationName(string name)
    {
        var last = name[(name.LastIndexOf('/') + 1)..];
        return last is "main" or "master" or "trunk" || IsIntegrationName(name) || name is "origin" or "upstream";
    }

    // Name of virtual branch in case of truncated repo log
    public const string TruncatedName = "<truncated-branch>";
}
