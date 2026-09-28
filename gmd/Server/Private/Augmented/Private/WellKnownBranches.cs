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

    // Whether a nice name is an integration branch's: one of the names above or of the repo's own
    // (configured per repo, e.g. 'staging'), also as the last part of a longer name ('owner/develop'),
    // or ending in '_develop' or '-develop', as a repo with one per major version has them ('v2_develop')
    public static bool IsIntegrationName(string name, IReadOnlyCollection<string>? repoNames = null)
    {
        var last = name[(name.LastIndexOf('/') + 1)..];
        return IntegrationNames.Contains(last)
            || repoNames?.Contains(name) == true
            || repoNames?.Contains(last) == true
            || last.EndsWith("_develop")
            || last.EndsWith("-develop")
            || last.EndsWith("_development")
            || last.EndsWith("-development");
    }

    // Whether a nice name is a git-flow release, hotfix or support branch's, which git-flow starts
    // from develop or the trunk and which others are started from in turn: 'release/1.0' or
    // 'hotfix/x', also after an owner ('owner/release/1.0'). Not a name ending in 'release': a
    // 'v1_release' is a trunk of its own, one per major version, rather than started from develop,
    // and 'prepare-release' is a feature.
    public static bool IsReleaseName(string name) =>
        name.Split('/')[..^1].Any(p => p is "release" or "hotfix" or "support");

    // How senior a branch is by its name, among the branches below the trunk: an integration branch is
    // started from the trunk, a release or hotfix branch from it or the trunk, and any other from any
    // of them. Not a deleted branch recovered with a trunk's name, which is as often a line of another
    // name, e.g. an imported project's main, or the local side of a pull merge, as an old trunk.
    public static int NameTier(string niceName, IReadOnlyCollection<string>? repoIntegrationNames = null) =>
        IsIntegrationName(niceName, repoIntegrationNames) ? 2
        : IsReleaseName(niceName) ? 1
        : 0;

    // Whether a nice name is a trunk's, also as another remote's or owner's ('upstream/main')
    public static bool IsTrunkName(string name) => name[(name.LastIndexOf('/') + 1)..] is "main" or "master" or "trunk";

    // Whether a name, e.g. from a merge subject, is the trunk's or an integration branch's, also as
    // another remote's, e.g. 'upstream/main', or is a remote's name alone, as 'git merge upstream'
    // writes, which merges that remote's default branch
    public static bool IsTrunkOrIntegrationName(string name, IReadOnlyCollection<string>? repoIntegrationNames = null)
    {
        return IsTrunkName(name) || IsIntegrationName(name, repoIntegrationNames) || name is "origin" or "upstream";
    }

    // Name of virtual branch in case of truncated repo log
    public const string TruncatedName = "<truncated-branch>";
}
