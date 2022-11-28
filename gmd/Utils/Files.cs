namespace gmd.Utils;

static class Files
{
    public static R WriteAllText(string path, string text)
    {
        try
        {
            File.WriteAllText(path, text);
            return R.Ok;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }

    }

    public static R<string> ReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }


    public static R Delete(string path)
    {
        try
        {
            File.Delete(path);
            return R.Ok;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }

    public static bool Exists(string path) => File.Exists(path);



    public static bool IsLarger(string path, int maxSixe)
    {
        try
        {
            FileInfo fi = new FileInfo(path);
            return fi.Length > maxSixe;
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


    private static R<bool> IsBinary(string path)
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
