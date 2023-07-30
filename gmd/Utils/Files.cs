using System.Reflection;

namespace gmd.Utils;


// Some file utility functions missing in .NET
static class Files
{
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


    public static bool IsText(string path)
    {
        if (!Try(out var isBinary, out var e, IsBinary(path))) return false;
        return !isBinary;
    }


    public static R<string> GetEmbeddedFileContentText(string name)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            if (assembly == null) return R.Error("No GetExecutingAssembly");

            // var names = asm.GetManifestResourceNames();
            var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) return R.Error($"Embedded file '{name}'");

            using (stream)
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
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

            using (var streamReader = new StreamReader(path))
            {
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
            }

            return false;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }
}
