namespace gmd.Cui.Start;

// The folder Init proposes, and the ones it refuses. Enter is pressed out of habit, so what is
// proposed has to be safe to take as it is: the start folder is, as the start page says, except the
// home folder, which gmd started there proposed making a repo of whole, with everything in it. A
// folder in it is proposed then, still to be named, and the log view's Init proposes one in the
// folder the recent repos are in, whose parent Enter made a repo of, holding all of them.
static class InitPath
{
    public const string WhyNotUnnamed = "Name the folder to init, after the last '/'";
    public const string WhyNotHome = "Not the home folder, which holds all else: name a folder in it";

    // The start folder, or a folder in it to name when it is the home folder
    public static string Proposed(string startFolder, string home) =>
        IsHome(startFolder, home) ? InFolder(startFolder) : startFolder;

    // A path ending in a separator, i.e. the folder of one still to be named
    public static bool IsUnnamed(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar);

    public static bool IsHome(string path, string home) =>
        home != ""
        && path.Trim() != ""
        && string.Equals(
            Full(path),
            Full(home),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
        );

    public static string InFolder(string folder) => IsUnnamed(folder) ? folder : folder + Path.DirectorySeparatorChar;

    static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
}
