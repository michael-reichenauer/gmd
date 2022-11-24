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
}
