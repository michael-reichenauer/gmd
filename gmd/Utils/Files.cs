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
            Log.Warn($"Failed to read {e}");
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
            Log.Warn($"Failed to read {e}");
            return 0;
        }
    }

    public static bool IsText(string path)
    {
        if (!Try(out var isBinary, out var _, IsBinary(path)))
            return false;
        return !isBinary;
    }

    public static R<string> GetEmbeddedFileContentText(string name)
    {
        if (!Try(out var stream, out var e, GetEmbeddedFileStream(name)))
            return e;

        try
        {
            using (stream)
            {
                using StreamReader reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    // Opens an embedded resource as a stream, for content that is not text or is too large to
    // want as one string (the spell check dictionary). The caller disposes the stream.
    public static R<Stream> GetEmbeddedFileStream(string name)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            if (assembly == null)
                return R.Error("No GetExecutingAssembly");

            // var names = asm.GetManifestResourceNames();
            var stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
                return R.Error($"Embedded file '{name}'");

            return stream;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }

    // Returns true if the file seems to be a binary file.
    // The file is considered binary if it contains at least one consecutive
    // sequence of 1 or more NUL characters within the first 8000 characters.
    static R<bool> IsBinary(string path)
    {
        try
        {
            const int requiredConsecutiveNul = 1;
            const int charsToCheck = 8000;
            const char nulChar = '\0';

            int nulCount = 0;

            using var streamReader = new StreamReader(path);
            for (var i = 0; i < charsToCheck; i++)
            {
                if (streamReader.EndOfStream)
                    return false;

                if ((char)streamReader.Read() == nulChar)
                {
                    nulCount++;

                    if (nulCount >= requiredConsecutiveNul)
                        return true;
                }
                else
                {
                    nulCount = 0;
                }
            }

            return false;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }
}
