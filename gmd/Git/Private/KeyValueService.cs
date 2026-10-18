namespace gmd.Git.Private;

// Values shared through origin under a ref of their own, pushed and fetched like a branch. The caller
// keeps its own value where it likes and pushes it from there, and origin's is fetched into a record
// of it, as last fetched or pushed, so that a caller merging the two can tell which side changed what
// since. A value of the repository's own, under the ref origin has it under, is only read and deleted:
// it is where values were kept before the caller kept them.
interface IKeyValueService
{
    Task<Result<string>> GetValueAsync(string key, string wd);

    // Removes the repository's own value: done also when there is none
    Task<Result> DeleteValueAsync(string key, string wd);

    // Origin's value as last fetched or pushed
    Task<Result<string>> GetOriginValueAsync(string key, string wd);

    // Pushes the value over origin's as last fetched or pushed, and records it as origin's: true, or
    // false when origin's has changed since, which is then to be fetched and merged first
    Task<Result<bool>> PushValueAsync(string key, string value, string wd);

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

    public async Task<Result> DeleteValueAsync(string key, string wd) =>
        await cmd.RunAsync("git", $"update-ref -d {KeyRef(key)}", wd, true, true);

    public Task<Result<string>> GetOriginValueAsync(string key, string wd) =>
        cmd.RunAsync("git", $"cat-file -p {OriginRef(key)}", wd, true, true);

    // Pushed by its object id, stored in the git database for it, with no ref of the repository's own
    // pointing at it: the record of origin's does, once it is pushed. Forced, since the value is a merge
    // of the two rather than a fast forward of origin's, but leased on origin's value as last fetched or
    // pushed, or on there being none, when there is no record of it: a value another clone pushed
    // since is refused rather than pushed over, which lost what was chosen there, and the push says
    // so, false.
    public async Task<Result<bool>> PushValueAsync(string key, string value, string wd)
    {
        var stored = await StoreAsync(value, wd);
        if (stored is not string id)
            return stored.Error;
        var lastOrigin = await cmd.RunAsync("git", $"rev-parse --verify --quiet {OriginRef(key)}", wd, true, true);
        var expected = lastOrigin is string last ? last.Trim() : "";

        var args = $"push --porcelain origin --force-with-lease={KeyRef(key)}:{expected} {id}:{KeyRef(key)}";
        var pushed = await cmd.RunAsync("git", args, wd, true, false);
        if (pushed is CmdError e && (e.Output.Contains("stale info") || e.ErrorOutput.Contains("stale info")))
            return false;
        if (pushed is Error pushError)
            return pushError;

        if (await cmd.RunAsync("git", $"update-ref {OriginRef(key)} {id}", wd, true, true) is Error recordError)
            return recordError;
        return true;
    }

    // Into the record of origin's rather than over the repository's own value: a fetch over it lost a
    // change made here that origin did not have yet (git updates a ref to a blob without asking
    // whether it is a fast forward). When origin has none, e.g. it was deleted there, the record goes
    // too: a push leased on a value origin no longer has was refused, every time.
    public async Task<Result> FetchValueAsync(string key, string wd)
    {
        var fetched = await cmd.RunAsync("git", $"fetch origin +{KeyRef(key)}:{OriginRef(key)}", wd, true, true);
        if (fetched is CmdError e && e.ErrorOutput.Contains("couldn't find remote ref"))
        {
            if (await cmd.RunAsync("git", $"update-ref -d {OriginRef(key)}", wd, true, true) is Error deleteError)
                return deleteError;
        }
        return fetched is Error fetchError ? fetchError : Result.Ok;
    }

    // The value in the git database, by a temp file, and its object id
    async Task<Result<string>> StoreAsync(string value, string wd)
    {
        var path = TmpFilePath(wd);
        try
        {
            if (Result.Catch(() => File.WriteAllText(path, value)) is Error writeError)
                return writeError;
            var hashed = await cmd.RunAsync("git", $"hash-object -w {Cmd.Quote(path)}", wd, true, true);
            if (hashed is not string objectId)
                return hashed.Error;
            return objectId.Trim();
        }
        finally
        {
            if (Result.Catch(() => File.Delete(path)) is Error e)
                Log.Warn($"{e}");
        }
    }

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
