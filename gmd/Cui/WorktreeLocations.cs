namespace gmd.Cui;

// Where a new worktree can be put, as the create dialog offers them
enum WorktreeLocation
{
    Sibling, // Beside the repository: '<parent>/<repo>-<branch>'
    Claude, // Where Claude Code puts its own: '<repo>/.claude/worktrees/<name>'
    Local, // Inside the repository: '<repo>/.worktrees/<name>'
}

// The folder each location means for a branch. The two inside the repository have to be ignored
// by git, or the main worktree shows the whole checkout as untracked files, which is what the
// dialog's .gitignore checkbox is for.
static class WorktreeLocations
{
    public const string ClaudeFolder = ".claude/worktrees";
    public const string LocalFolder = ".worktrees";

    public static readonly IReadOnlyList<string> IgnoredFolders = [ClaudeFolder, LocalFolder];

    public static string PathFor(WorktreeLocation location, string mainRoot, string branchName)
    {
        var name = FolderName(branchName);
        var root = mainRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var repoName = Path.GetFileName(root);
        return location switch
        {
            WorktreeLocation.Sibling => Path.Join(Path.GetDirectoryName(root) ?? root, $"{repoName}-{name}"),
            // Claude names its branch 'worktree-<name>' for the folder '<name>', so a branch named
            // that way lands where Claude would have put it
            WorktreeLocation.Claude => Path.Join(root, ".claude", "worktrees", name.TrimPrefix("worktree-")),
            WorktreeLocation.Local => Path.Join(root, ".worktrees", name),
            _ => throw Asserter.FailFast($"Unknown location {location}"),
        };
    }

    // The folder to add to .gitignore for a location, empty for one outside the repository
    public static string IgnoreFolder(WorktreeLocation location) =>
        location switch
        {
            WorktreeLocation.Claude => ClaudeFolder,
            WorktreeLocation.Local => LocalFolder,
            _ => "",
        };

    // A branch name as a folder name: the '/' of 'feature/login' would be a folder level
    public static string FolderName(string branchName)
    {
        var name = branchName.Trim().Replace('/', '-').Replace('\\', '-');
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '-');
        return name;
    }

    // The remembered pick, which is a name in the config so that a reordering of the enum cannot
    // change what it means
    public static WorktreeLocation Parse(string name) =>
        Enum.TryParse<WorktreeLocation>(name, out var location) ? location : WorktreeLocation.Sibling;
}
