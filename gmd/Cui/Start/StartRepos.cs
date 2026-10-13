using gmd.Common;
using gmd.Git;

namespace gmd.Cui.Start;

// A repository the start page lists: by its folder's name, which is what tells repositories apart,
// and its path as a shell writes it, '~/code/gmd'. Recent if it was opened before, otherwise found
// in the folder gmd was started in.
record StartRepo(string Path, string Name, string ShownPath, bool IsRecent);

// What the start page lists: the repositories opened before, most recent first, and the ones in the
// folder gmd was started in, which is where someone who keeps their repositories in '~/code' starts
// it. Only looked at on disk, a '.git' per folder and no git process, so the page is shown at once
// however many it lists.
//
// A recent folder that is gone, or is no longer a repository, is left out rather than listed to fail
// when opened. So is a folder of the start folder that is already a recent one, and a dot folder,
// since started in home those are tools' folders ('.nvm', '.oh-my-zsh'), not the user's work.
record StartRepos(
    string Folder, // The folder gmd was started in
    string ShownFolder, // ... as '~/...'
    bool IsFolderInRepo, // Started inside a repository, which only '-m' shows the start page for
    IReadOnlyList<StartRepo> Recent,
    IReadOnlyList<StartRepo> InFolder,
    int MoreInFolder // The repositories in the folder beyond the ones listed
)
{
    // The folder's repositories listed at most, sorted by name so each keeps its number from one day
    // to the next, and the folders looked at at most, so a start in '/' or a large share is instant
    public const int MaxInFolder = 10;
    const int maxFoldersScanned = 500;

    public static StartRepos Empty { get; } = new("", "", false, [], [], 0);

    public static StartRepos Of(IEnumerable<string> recentFolders, string folder, string home)
    {
        var t = Timing.Start();
        var recent = RecentOf(recentFolders, home);
        var found = ReposIn(folder).Where(path => !recent.Any(r => Files.IsSamePath(r.Path, path))).ToList();
        var inFolder = found.Take(MaxInFolder).Select(path => Repo(path, home, false)).ToList();
        var isFolderInRepo = folder != "" && GitDir.Find(folder) is GitDirInfo;
        Log.Info($"Start page lists {recent.Count} recent and {found.Count} in '{folder}' {t}");

        return new StartRepos(
            folder,
            Files.ShortHomePath(folder, home),
            isFolderInRepo,
            recent,
            inFolder,
            found.Count - inFolder.Count
        );
    }

    // The recent repositories alone, as the open menu of the log view lists them too
    public static IReadOnlyList<StartRepo> RecentOf(IEnumerable<string> recentFolders, string home)
    {
        List<string> paths = [];
        foreach (var path in recentFolders.Where(GitDir.IsRoot))
        {
            if (!paths.Any(p => Files.IsSamePath(p, path)))
                paths.Add(path);
        }

        return paths.Take(Config.MaxRecentFolders).Select(path => Repo(path, home, true)).ToList();
    }

    static IReadOnlyList<string> ReposIn(string folder)
    {
        if (folder == "" || !Directory.Exists(folder))
            return [];

        var foldersResult = Result.Catch(() => Directory.EnumerateDirectories(folder).Take(maxFoldersScanned).ToList());
        if (foldersResult is not List<string> folders)
        {
            Log.Warn($"Failed to list '{folder}': {foldersResult.Error.AllMessages()}");
            return [];
        }

        return folders
            .Where(path => !NameOf(path).StartsWith('.') && GitDir.IsRoot(path))
            .OrderBy(NameOf, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static StartRepo Repo(string path, string home, bool isRecent) =>
        new(path, NameOf(path), Files.ShortHomePath(path, home), isRecent);

    // The folder's own name, or the path itself for a root, which has none
    static string NameOf(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name != "" ? name : path;
    }
}
