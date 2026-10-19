using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

// Where the metadata is kept on this machine: a file of its own in the repository's common git dir,
// beside .gmdconfig, so that every worktree of the repository shares it, as they share the branches
// it is about. Inside the git dir, git never sends it anywhere, and no git command or tool lists it,
// as they list a ref. Only the part to share is pushed, and only when sharing is on (MetaDataService).
interface IMetaDataFile
{
    bool Exists(string wd);
    Task<Result<string>> ReadAsync(string wd);
    Task<Result> WriteAsync(string wd, string text);
}

// Read afresh every time, since another gmd, e.g. one in another worktree, may have written it since.
// Written beside it and renamed over it, so that it is never read half written; and read allowing the
// rename meanwhile, which Windows refuses over a file open without FileShare.Delete.
class MetaDataFile : IMetaDataFile
{
    internal const string FileName = "gmd-metadata.json";

    public bool Exists(string wd) => File.Exists(FilePath(wd));

    public async Task<Result<string>> ReadAsync(string wd)
    {
        try
        {
            var share = FileShare.ReadWrite | FileShare.Delete;
            using var stream = new FileStream(FilePath(wd), FileMode.Open, FileAccess.Read, share);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Failed to read '{FilePath(wd)}'", e);
        }
    }

    public async Task<Result> WriteAsync(string wd, string text)
    {
        var path = FilePath(wd);
        // Named for this write alone, since two gmds may write at once
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, text);
            File.Move(tempPath, path, overwrite: true);
            return Result.Ok;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Result.Catch(() => File.Delete(tempPath));
            return new Error($"Failed to write '{path}'", e);
        }
    }

    // In a linked worktree '.git' is a file, so the dir has to be resolved rather than joined
    static string FilePath(string wd)
    {
        var gitDir = GitDir.Resolve(wd) is GitDirInfo info ? info.CommonDirPath : Path.Join(wd, ".git");
        return Path.Join(gitDir, FileName);
    }
}
