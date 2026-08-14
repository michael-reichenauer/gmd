namespace gmd.Git.Private;

interface ICommitService
{
    Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<R> UndoAllUncommittedChangesAsync(string wd);
    Task<R> UndoUncommittedFileAsync(string path, string wd);
    Task<R> CleanWorkingFolderAsync(string wd);
    Task<R> UndoCommitAsync(string id, int parentIndex, string wd);
    Task<R> UncommitLastCommitAsync(string wd);
    Task<R> UncommitUntilCommitAsync(string id, string wd);
    Task<R> ResetHardUntilCommitAsync(string id, string wd);
}

// cSpell:ignore pathspec
class CommitService : ICommitService
{
    private readonly ICmd cmd;

    public CommitService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd)
    {
        // Encode '"' chars
        message = message.Replace("\"", "\\\"");

        // While an operation is in progress its result is already staged, and both of the ways of
        // staging here would stage an unmerged path with the conflict markers as its content —
        // 'git commit -am' on a conflicted merge succeeds and commits '<<<<<<<' into history. So
        // stage nothing and commit what the operation and the user have already staged.
        //
        // The test used to be for .git/MERGE_MSG, which a rebase with the --apply backend and
        // 'git am' do not write, so those went down the staging path.
        var isOperationInProgress = StatusService.IsOperationInProgress(wd);
        if (!isOperationInProgress)
        {
            if (!Try(out var _, out var e, await cmd.RunAsync("git", "add .", wd)))
                return e;
        }

        var amendText = isAmend ? " --amend" : "";
        var allText = isOperationInProgress ? "" : "a";
        var result = await cmd.RunAsync("git", $"commit{amendText} -{allText}m \"{message}\"", wd);

        // Staging nothing means git now refuses a commit that it used to make, so say what to do
        // about it in gmd's own words rather than passing on 'error: Committing is not possible
        // because you have unmerged files' with four lines of git hints under it.
        if (result.IsResultError && IsUnmergedFiles(result))
            return R.Error(
                "Cannot commit while there are unresolved conflicts.\n\n"
                    + "Resolve each conflicted file and mark it resolved, then commit.",
                result
            );

        return result;
    }

    static bool IsUnmergedFiles(CmdResult result) =>
        result.ErrorOutput.Contains("unmerged files") || result.ErrorOutput.Contains("unresolved conflict");

    public async Task<R> UndoAllUncommittedChangesAsync(string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", "reset --hard", wd)))
            return e;

        return await cmd.RunAsync("git", "clean -fd", wd);
    }

    public async Task<R> UndoUncommittedFileAsync(string path, string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", $"checkout --force \"{path}\"", wd)))
        {
            // Some error while restore file
            if (IsFileUnknown(e, path))
            {
                // Was an unknown (new/added) file, we just remove it
                var fullPath = Path.Combine(wd, path);
                if (!Try(out e, () => File.Delete(fullPath)))
                    return R.Error("Failed to reset", e);
                Log.Info($"File '{path}' (new/added) was removed");
                return R.Ok;
            }

            return R.Error("Failed to reset", e);
        }

        return R.Ok;
    }

    public async Task<R> CleanWorkingFolderAsync(string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", "reset --hard", wd)))
            return e;

        return await cmd.RunAsync("git", "clean -fxd", wd);
    }

    public async Task<R> UndoCommitAsync(string id, int parentIndex, string wd)
    {
        var parent = parentIndex == 0 ? "" : $"-m {parentIndex}";
        return await cmd.RunAsync("git", $"revert {parent} --no-commit {id}", wd);
    }

    public async Task<R> UncommitLastCommitAsync(string wd)
    {
        return await cmd.RunAsync("git", "reset HEAD~1", wd);
    }

    public async Task<R> UncommitUntilCommitAsync(string id, string wd)
    {
        return await cmd.RunAsync("git", $"reset --soft {id}", wd);
    }

    public async Task<R> ResetHardUntilCommitAsync(string id, string wd)
    {
        return await cmd.RunAsync("git", $"reset --hard {id}", wd);
    }

    static bool IsFileUnknown(ErrorResult error, string path)
    {
        var msg = $"error: pathspec '{path}' did not match any file(s) known";
        return error.ErrorMessage.StartsWith(msg);
    }
}
