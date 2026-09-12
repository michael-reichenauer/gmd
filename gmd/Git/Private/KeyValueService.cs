namespace gmd.Git.Private;

interface IKeyValueService
{
    Task<Result<string>> GetValueAsync(string key, string wd);
    Task<Result> SetValueAsync(string key, string value, string wd);
    Task<Result> PushValueAsync(string key, string wd);
    Task<Result> PullValueAsync(string key, string wd);
}

class KeyValueService : IKeyValueService
{
    readonly ICmd cmd;

    internal KeyValueService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result<string>> GetValueAsync(string key, string wd)
    {
        var result = await cmd.RunAsync("git", $"cat-file -p {KeyRef(key)}", wd, true, true);
        if (result is not string output)
            return result.Error;
        return output;
    }

    public async Task<Result> SetValueAsync(string key, string value, string wd)
    {
        var path = TmpFilePath(wd);
        try
        {
            // Store the temp file with key value in the git database (returns an object id)
            if (Result.Catch(() => File.WriteAllText(path, value)) is Error writeError)
                return writeError;
            var hashed = await cmd.RunAsync("git", $"hash-object -w \"{path}\"", wd, true, true);
            if (hashed is not string objectId)
                return hashed.Error;
            objectId = objectId.Trim();

            // Add a ref pointer to the stored object for easier retrieval
            if (await cmd.RunAsync("git", $"update-ref {KeyRef(key)} {objectId}", wd, true) is Error updateError)
                return updateError;
        }
        finally
        {
            if (Result.Catch(() => File.Delete(path)) is Error e)
                Log.Warn($"{e}");
        }

        return Result.Ok;
    }

    public async Task<Result> PushValueAsync(string key, string wd)
    {
        var refKey = KeyRef(key);
        string refs = $"{refKey}:{refKey}";
        var args = $"push --porcelain origin --set-upstream --force {refs}";
        return await cmd.RunAsync("git", args, wd, true, false);
    }

    public async Task<Result> PullValueAsync(string key, string wd)
    {
        var refKey = KeyRef(key);
        string refs = $"{refKey}:{refKey}";
        var args = $"fetch origin {refs}";
        return await cmd.RunAsync("git", args, wd, true, true);
    }

    string KeyRef(string key) => $"refs/gmd-metadata-key-value/{key}";

    // A scratch file inside the git dir, which git ignores. In a linked worktree '.git' is a file,
    // so the dir has to be resolved rather than joined; the per-worktree dir is fine for this, and
    // keeps two gmds on one repository from ever sharing a scratch folder.
    string TmpFilePath(string wd)
    {
        var name = Path.GetRandomFileName();
        var gitDir = GitDir.Resolve(wd) is GitDirInfo info ? info.GitDirPath : Path.Join(wd, ".git");
        return Path.Join(gitDir, $"gmd.tmp.{name}");
    }
}
