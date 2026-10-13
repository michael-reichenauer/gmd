using System.Reflection;

namespace gmd.Utils;

// Some file utility functions missing in .NET
static class Files
{
    // Whether two paths name the same folder, spelled however: relative or not, with or without
    // a trailing separator, and in either case where the file system does not care about case.
    // Symbolic links are not followed.
    public static bool IsSamePath(string path1, string path2) =>
        string.Equals(NormalizedPath(path1), NormalizedPath(path2), PathComparison);

    static string NormalizedPath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    // A path as a shell writes it, with the home folder as '~', so the part that tells folders apart
    // is what is read: '~/code/gmd' for '/home/me/code/gmd'. A path outside home is left as it is,
    // and so is '/home/meow', which only starts with the same letters.
    public static string ShortHomePath(string path, string home)
    {
        home = home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (home == "" || !path.StartsWith(home, PathComparison))
            return path;

        var rest = path[home.Length..];
        if (rest.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) == "")
            return "~";
        if (rest[0] != Path.DirectorySeparatorChar && rest[0] != Path.AltDirectorySeparatorChar)
            return path;

        return "~" + rest.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    // The user's home folder, '~'
    public static string HomePath => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // A path for a new folder named 'name' in 'parent', numbered '-1', '-2', ... if the name is
    // taken, so a dialog can propose a folder that does not exist yet
    public static string UniqueFolderPath(string parent, string name)
    {
        var newName = name;
        var path = Path.Combine(parent, newName);
        for (int i = 1; i < 50; i++)
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                break;
            }
            newName = $"{name}-{i}";
            path = Path.Combine(parent, newName);
        }

        return path;
    }

    public static bool IsLarger(string path, int maxSize)
    {
        try
        {
            FileInfo fi = new FileInfo(path);
            return fi.Length > maxSize;
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to read the size of {path}: {e.Message}");
            return false;
        }
    }

    public static long FileSize(string path)
    {
        try
        {
            FileInfo fi = new FileInfo(path);
            return fi.Length;
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to read the size of {path}: {e.Message}");
            return 0;
        }
    }

    // Whether a file is text rather than binary, decided as git decides it: binary when there is a
    // NUL byte among the first 8000. Bytes and not characters, since a reader decodes a file that
    // starts with a byte order mark, and so read UTF-16, whose every other byte is a NUL in ASCII
    // text, as text, while git takes it as binary and will not merge it as text. A file that cannot
    // be read is not text.
    public static bool IsText(string path)
    {
        var start = new byte[BinaryCheckLength];
        var read = Result.Catch(() =>
        {
            using var file = File.OpenRead(path);
            return file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
        });

        return read is int length && Array.IndexOf(start, (byte)0, 0, length) == -1;
    }

    const int BinaryCheckLength = 8000;

    // Opens an embedded resource as a stream, for content that is not text or is too large to
    // want as one string (the spell check dictionary). The caller disposes the stream.
    public static Result<Stream> GetEmbeddedFileStream(string name)
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null)
                return new Error($"No embedded file '{name}'");

            return stream;
        }
        catch (Exception e)
        {
            return new Error(e);
        }
    }
}
