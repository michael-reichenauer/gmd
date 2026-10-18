namespace gmd.Cui.Common;

// Which folders the folder browser lists for what has been typed. A path, one starting with '~' or
// at the root of the file system, browses: the folders in the folder it names up to its last
// separator, those whose name starts with what follows first and then those whose name has it, as a
// shell completes a path; with nothing after the separator, that folder itself comes first, so that
// Enter picks it. Anything else is words to find among the folders listed so far, ranked as
// FileFinder ranks files: every word in the path as shown ('~/code/gmd'), case ignored, in any order,
// the folders whose name starts with the first word first, then those whose name has it, then those
// whose folders alone do; each with the folders less deep first, then in path order. Plain, with no
// view or disk, so it is tested without either.
static class FolderFinder
{
    static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    // Whether what is typed is a path to browse rather than words to find
    public static bool IsPath(string text) => text == "~" || IsHomePath(text) || Path.IsPathRooted(text);

    // The folder a path typed names up to its last separator, '~' being home, and the start of a
    // name typed after it: '~/co' is '/home/me/' and 'co', and '~/code/' is '/home/me/code/' and ''
    public static (string Folder, string Name) Split(string text, string home)
    {
        var path =
            text == "~" ? home
            : IsHomePath(text) ? home.TrimEnd(Separators) + text[1..]
            : text;
        var last = path.LastIndexOfAny(Separators);
        return last < 0 ? ("", path) : (path[..(last + 1)], path[(last + 1)..]);
    }

    // The folders a path typed lists, given the folders in its folder in name order, or null when it
    // is no folder that can be read
    public static IReadOnlyList<string> InFolder(string folder, string name, IReadOnlyList<string>? folders)
    {
        if (folders == null)
            return [];
        if (name == "")
            return [Path.TrimEndingDirectorySeparator(folder), .. folders];

        var starting = folders.Where(f => FolderTree.Name(f).StartsWith(name, StringComparison.OrdinalIgnoreCase));
        var having = folders.Where(f =>
            !FolderTree.Name(f).StartsWith(name, StringComparison.OrdinalIgnoreCase)
            && FolderTree.Name(f).Contains(name, StringComparison.OrdinalIgnoreCase)
        );
        return [.. starting, .. having];
    }

    // The folders listed whose path as shown, with home as '~', has every word typed, best first
    public static IReadOnlyList<string> Find(IReadOnlyList<string> paths, string text, string home)
    {
        var words = FileFinder.Words(text);
        if (words.Count == 0)
            return paths;

        return paths
            .Select(p => (Path: p, Shown: Files.ShortHomePath(p, home)))
            .Where(p => words.All(w => p.Shown.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => Rank(FolderTree.Name(p.Path), words[0]))
            .ThenBy(p => p.Shown.Count(c => Separators.Contains(c)))
            .ThenBy(p => p.Shown, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Shown, StringComparer.Ordinal)
            .Select(p => p.Path)
            .ToList();
    }

    static bool IsHomePath(string text) => text.Length > 1 && text[0] == '~' && Separators.Contains(text[1]);

    static int Rank(string name, string word) =>
        name.StartsWith(word, StringComparison.OrdinalIgnoreCase) ? 0
        : name.Contains(word, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;
}
