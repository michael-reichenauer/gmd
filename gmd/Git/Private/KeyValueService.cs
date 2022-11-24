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
    readonly IRemoteService remoteService;
    readonly ICmd cmd;

    internal KeyValueService(IRemoteService remoteService, ICmd cmd)
    {
        this.remoteService = remoteService;
        this.cmd = cmd;
    }


    public async Task<R<string>> GetValueAsync(string key, string wd)
    {
        if (!Try(out var output, out var e, await cmd.RunAsync("git", $"cat-file -p {KeyRef(key)}", wd))) return e;
        return output;
    }


    public async Task<R> SetValueAsync(string key, string value, string wd)
    {
        var path = TmpFilePath(wd);
        try
        {
            // Store the temp file with key value in the git database (returns an object id)
            if (!Try(out var e, Files.WriteAllText(path, value))) return e;
            if (!Try(out var objectId, out e, await cmd.RunAsync("git", $"hash-object -w \"{path}\"", wd))) return e;
            objectId = objectId.Trim();

            // Add a ref pointer to the stored object for easier retrieval
            if (!Try(out e, await cmd.RunAsync("git", $"update-ref {KeyRef(key)} {objectId}", wd))) return e;
        }
        finally
        {
            if (!Try(out var e, Files.Delete(path))) Log.Warn($"{e}");
        }

        return R.Ok;
    }


    public Task<R> PushValueAsync(string key, string wd) =>
        remoteService.PushRefForceAsync(KeyRef(key), wd);

    public Task<R> PullValueAsync(string key, string wd) =>
        remoteService.PullRefAsync(KeyRef(key), wd);



    string KeyRef(string key) => $"refs/gmd-metadata-key-value/{key}";

    string TmpFilePath(string wd)
    {
        var name = Path.GetRandomFileName();
        return Path.Join(wd, ".git", $"gmd.tmp.{name}");
    }

}
