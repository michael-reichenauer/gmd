namespace gmd.Git;

// Where a working folder keeps its git state. For the main worktree of a repository all of it is
// in '<root>/.git'. For a linked worktree ('git worktree add') '.git' is a *file* holding
// 'gitdir: <path>', and that path is a folder under '<main>/.git/worktrees/' with what is private
// to the checkout: HEAD, the index, a stopped merge or rebase. Everything shared — refs, objects,
// packed-refs, config, stashes — is in the common dir, which the 'commondir' file in the gitdir
// points at. A submodule has the same '.git' file but no 'commondir', so it is its own repository.
internal record GitDirInfo(
    string RootPath, // The working folder, i.e. the folder holding '.git'
    string GitDirPath, // Where HEAD, the index and operation state live
    string CommonDirPath, // Where refs and objects live; the same as GitDirPath unless linked
    bool IsLinkedWorktree
);

internal static class GitDir
{
    const string GitFolder = ".git";
    const string GitDirPrefix = "gitdir:";
    const string CommonDirFile = "commondir";

    // Walks up from a folder to the nearest one holding '.git', which is the root of the working
    // tree the folder is in. A '.git' file counts as well as a '.git' folder, so a linked worktree
    // is found, and found rather than the main repository it may be nested inside.
    public static R<GitDirInfo> Find(string path)
    {
        if (path == "")
        {
            path = Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(path))
        {
            return R.Error($"Folder does not exist: '{path}'");
        }

        var current = path.TrimSuffix("/").TrimSuffix("\\");
        if (path.EndsWith(GitFolder))
        {
            current = Path.GetDirectoryName(path) ?? path;
        }

        while (true)
        {
            var gitPath = Path.Join(current, GitFolder);
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return Resolve(current);
            }
            var parent = Path.GetDirectoryName(current) ?? current;
            if (parent == current)
            {
                // Reached top/root volume folder
                break;
            }
            current = parent;
        }

        return R.Error($"No '.git' folder was found in:\n'{path}'\n or in any parent folders.");
    }

    // Resolves the git dirs of a known working tree root
    public static R<GitDirInfo> Resolve(string rootPath)
    {
        var gitPath = Path.Join(rootPath, GitFolder);
        if (Directory.Exists(gitPath))
        {
            return new GitDirInfo(rootPath, gitPath, gitPath, false);
        }

        if (!File.Exists(gitPath))
        {
            return R.Error($"No '.git' folder was found in:\n'{rootPath}'");
        }

        var pointer = ReadFirstLine(gitPath);
        if (!pointer.StartsWith(GitDirPrefix))
        {
            return R.Error($"Not a git dir pointer: '{gitPath}'");
        }

        // The pointer is relative to the folder holding the '.git' file (a submodule's is)
        var gitDir = FullPath(pointer[GitDirPrefix.Length..].Trim(), rootPath);

        // The common dir is relative to the gitdir, e.g. '../..' for '<main>/.git/worktrees/<name>'
        var commonDirPath = Path.Join(gitDir, CommonDirFile);
        if (!File.Exists(commonDirPath))
        {
            return new GitDirInfo(rootPath, gitDir, gitDir, false);
        }

        var commonDir = FullPath(ReadFirstLine(commonDirPath), gitDir);
        return new GitDirInfo(rootPath, gitDir, commonDir, true);
    }

    static string FullPath(string path, string relativeTo) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Join(relativeTo, path));

    static string ReadFirstLine(string path) =>
        Try(out var text, out var _, () => File.ReadAllText(path)) ? text.Split('\n')[0].Trim() : "";
}
