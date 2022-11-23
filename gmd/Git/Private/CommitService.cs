namespace gmd.Git.Private;

interface ICommitService
{
    Task<R> CommitAllChangesAsync(string message, string wd);
    Task<R> UndoAllUncommittedChangesAsync(string wd);
    Task<R> UndoUncommittedFileAsync(string path, string wd);
    Task<R> CleanWorkingFolderAsync(string wd);
    Task<R> UndoCommitAsync(string id, string wd);
    Task<R> UncommitLastCommitAsync(string wd);
}

class CommitService : ICommitService
{
    private readonly ICmd cmd;

    public CommitService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> CommitAllChangesAsync(string message, string wd)
    {
        // Encode '"' chars
        message = message.Replace("\"", "\\\"");

        if (!StatusService.IsMergeInProgress(wd))
        {
            if (!Try(out var _, out var e, await cmd.RunAsync("git", "add .", wd))) return e;
        }

        return await cmd.RunAsync("git", $"commit -am \"{message}\"", wd);
    }


    public async Task<R> UndoAllUncommittedChangesAsync(string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", "reset --hard", wd))) return e;

        return await cmd.RunAsync("git", "clean -fd", wd);
    }

    public async Task<R> UndoUncommittedFileAsync(string path, string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", $"checkout --force \"{path}\"", wd)))
        {
            // Some error while restore file
            if (IsFileUnknown(e, path))
            {
                // Was an unkown (new/added) file, we just remove it
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    return R.Error("Failed to reset", ex);
                }

                return R.Ok;
            }

            return R.Error("Failed to reset", e);
        }

        return R.Ok;
    }

    public async Task<R> CleanWorkingFolderAsync(string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", "reset --hard", wd))) return e;

        return await cmd.RunAsync("git", "clean -fxd", wd);
    }

    public async Task<R> UndoCommitAsync(string id, string wd)
    {
        return await cmd.RunAsync("git", "revert --no-commit", wd);
    }

    public async Task<R> UncommitLastCommitAsync(string wd)
    {
        return await cmd.RunAsync("git", "reset HEAD~1", wd);
    }

    bool IsFileUnknown(ErrorResult error, string path)
    {
        var msg = $"error: pathspec '{path}' did not match any file(s) known";
        return error.ErrorMessage.StartsWith(msg);
    }
}
