namespace gmd.Cui.Common;

// Which files the file browser lists for what has been typed, best match first. A file matches when
// its path contains every word typed, case ignored, in any order, so 'cui dlg' finds the dialogs
// under Cui/ and 'common/' a folder's files. The order is what makes a few letters enough, since a
// file is remembered by its name rather than by its folders: first the files whose name starts with
// the first word, then those whose name has it, then the ones whose folders alone do; each with the
// files in fewer folders first, so 'program' finds gmd/Program.cs before
// gmd/Common/ProgramCommands.cs, and then in path order. Plain and with no view, so it is tested
// without a terminal.
static class FileFinder
{
    public static IReadOnlyList<string> Find(IReadOnlyList<string> paths, string text)
    {
        var words = Words(text);
        if (words.Count == 0)
            return paths;

        return paths
            .Where(p => words.All(w => p.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(p => (Path: p, Rank: Rank(Name(p), words[0])))
            .OrderBy(p => p.Rank)
            .ThenBy(p => p.Path.Count(c => c == '/'))
            .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .Select(p => p.Path)
            .ToList();
    }

    // The words typed, which a row marks where they are found in it
    public static IReadOnlyList<string> Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // The last part of a path, the file's name
    public static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];

    static int Rank(string name, string word) =>
        name.StartsWith(word, StringComparison.OrdinalIgnoreCase) ? 0
        : name.Contains(word, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;
}
