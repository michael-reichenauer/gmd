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

    // Name of virtual branch in case of truncated repo log
    public const string TruncatedName = "<truncated-branch>";
}
