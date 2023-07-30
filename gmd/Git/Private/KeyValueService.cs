namespace gmd.Git.Private;

interface IKeyValueService
{
    Task<R<string>> GetValueAsync(string key, string wd);
    Task<R> SetValueAsync(string key, string value, string wd);
    Task<R> PushValueAsync(string key, string wd);
    Task<R> PullValueAsync(string key, string wd);
}

class KeyValueService : IKeyValueService
{
    readonly ICmd cmd;

    internal KeyValueService(ICmd cmd)
    {
        this.cmd = cmd;
    }


    public async Task<R<string>> GetValueAsync(string key, string wd)
    {
        if (!Try(out var output, out var e, await cmd.RunAsync(
            "git", $"cat-file -p {KeyRef(key)}", wd, true, true))) return e;
        return output;
    }


    public async Task<R> SetValueAsync(string key, string value, string wd)
    {
        var path = TmpFilePath(wd);
        try
        {
            // Store the temp file with key value in the git database (returns an object id)
            if (!Try(out var e, () => File.WriteAllText(path, value))) return e;
            if (!Try(out var objectId, out e, await cmd.RunAsync(
                "git", $"hash-object -w \"{path}\"", wd, true, true))) return e;
            objectId = objectId.Trim();

            // Add a ref pointer to the stored object for easier retrieval
            if (!Try(out e, await cmd.RunAsync("git", $"update-ref {KeyRef(key)} {objectId}", wd, true))) return e;
        }
        finally
        {
            if (!Try(out var e, () => File.Delete(path))) Log.Warn($"{e}");
        }

        return R.Ok;
    }


    public async Task<R> PushValueAsync(string key, string wd)
    {
        var refKey = KeyRef(key);
        string refs = $"{refKey}:{refKey}";
        var args = $"push --porcelain origin --set-upstream --force {refs}";
        return await cmd.RunAsync("git", args, wd, true, false);
    }

    public async Task<R> PullValueAsync(string key, string wd)
    {
        var refKey = KeyRef(key);
        string refs = $"{refKey}:{refKey}";
        var args = $"fetch origin {refs}";
        return await cmd.RunAsync("git", args, wd, true, true);
    }


    string KeyRef(string key) => $"refs/gmd-metadata-key-value/{key}";

    string TmpFilePath(string wd)
    {
        var name = Path.GetRandomFileName();
        return Path.Join(wd, ".git", $"gmd.tmp.{name}");
    }
}
