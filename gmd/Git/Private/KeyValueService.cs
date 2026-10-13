namespace gmd.Git.Private;

// Values kept in the repository under a ref of their own, which can be pushed to origin and fetched
// from it like a branch. Beside the repository's own value is a record of origin's, as last fetched
// or pushed, so that a caller merging the two can tell which side changed what since.
interface IKeyValueService
{
    Task<Result<string>> GetValueAsync(string key, string wd);
    Task<Result> SetValueAsync(string key, string value, string wd);

    // Origin's value as last fetched or pushed
    Task<Result<string>> GetOriginValueAsync(string key, string wd);

    // Pushes the value over origin's, and records it as origin's
    Task<Result> PushValueAsync(string key, string wd);

    // Fetches origin's value into the record of it, leaving the repository's own as it is
    Task<Result> FetchValueAsync(string key, string wd);
}

class KeyValueService : IKeyValueService
{
    readonly ICmd cmd;

    internal KeyValueService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public Task<Result<string>> GetValueAsync(string key, string wd) =>
        cmd.RunAsync("git", $"cat-file -p {KeyRef(key)}", wd, true, true);

    public async Task<Result> SetValueAsync(string key, string value, string wd)
    {
        var path = TmpFilePath(wd);
        try
        {
            // Store the temp file with key value in the git database (returns an object id)
            if (Result.Catch(() => File.WriteAllText(path, value)) is Error writeError)
                return writeError;
            var hashed = await cmd.RunAsync("git", $"hash-object -w {Cmd.Quote(path)}", wd, true, true);
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

    public Task<Result<string>> GetOriginValueAsync(string key, string wd) =>
        cmd.RunAsync("git", $"cat-file -p {OriginRef(key)}", wd, true, true);

    // Pushed by its object id, resolved first, so that a value set while the push runs is not recorded
    // as origin's. Forced, since the value is a merge of the two rather than a fast forward of origin's.
    public async Task<Result> PushValueAsync(string key, string wd)
    {
        var resolved = await cmd.RunAsync("git", $"rev-parse --verify {KeyRef(key)}", wd, true, true);
        if (resolved is not string output)
            return resolved.Error;
        var id = output.Trim();

        var args = $"push --porcelain origin --force {id}:{KeyRef(key)}";
        if (await cmd.RunAsync("git", args, wd, true, false) is Error pushError)
            return pushError;
        return await cmd.RunAsync("git", $"update-ref {OriginRef(key)} {id}", wd, true, true);
    }

    // Into the record of origin's rather than over the repository's own value: a fetch over it lost a
    // change made here that origin did not have yet (git updates a ref to a blob without asking
    // whether it is a fast forward)
    public async Task<Result> FetchValueAsync(string key, string wd) =>
        await cmd.RunAsync("git", $"fetch origin +{KeyRef(key)}:{OriginRef(key)}", wd, true, true);

    string KeyRef(string key) => $"refs/gmd-metadata-key-value/{key}";

    string OriginRef(string key) => $"refs/gmd-metadata-key-value-origin/{key}";

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
