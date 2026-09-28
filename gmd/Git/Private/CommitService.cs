namespace gmd.Git.Private;

interface ICommitService
{
    Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<Result> UndoAllUncommittedChangesAsync(string wd);
    Task<Result> UndoUncommittedFileAsync(string path, string wd);
    Task<Result> CleanWorkingFolderAsync(string wd);
    Task<Result> UndoCommitAsync(string id, int parentIndex, string wd);
    Task<Result> UncommitLastCommitAsync(string wd);
    Task<Result> UncommitUntilCommitAsync(string id, string wd);
    Task<Result> ResetHardUntilCommitAsync(string id, string wd);
}

// cSpell:ignore pathspec
class CommitService : ICommitService
{
    private readonly ICmd cmd;

    public CommitService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd)
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
            if (await cmd.RunAsync("git", "add .", wd) is Error e)
                return e;
        }

        var amendText = isAmend ? " --amend" : "";
        var allText = isOperationInProgress ? "" : "a";
        var result = await cmd.RunAsync("git", $"commit{amendText} -{allText}m \"{message}\"", wd);

        // Staging nothing means git now refuses a commit that it used to make, so say what to do
        // about it in gmd's own words rather than passing on 'error: Committing is not possible
        // because you have unmerged files' with four lines of git hints under it.
        if (result is CmdError cmdError && IsUnmergedFiles(cmdError))
            return new Error(
                "Cannot commit while there are unresolved conflicts.\n\n"
                    + "Resolve each conflicted file and mark it resolved, then commit.",
                cmdError
            );

        return result;
    }

    static bool IsUnmergedFiles(CmdError error) =>
        error.ErrorOutput.Contains("unmerged files") || error.ErrorOutput.Contains("unresolved conflict");

    public async Task<Result> UndoAllUncommittedChangesAsync(string wd)
    {
        if (await cmd.RunAsync("git", "reset --hard", wd) is Error e)
            return e;

        return await cmd.RunAsync("git", "clean -fd", wd);
    }

    // Back to the last commit, in the index as well as in the working tree, which is what the
    // question before it says. From the index alone, as this used to, a change staged with another
    // tool stayed, and a new file staged there was not deleted: the diff used to unstage everything
    // before this was picked, but no longer touches the index (DiffService.GetUncommittedDiff).
    public async Task<Result> UndoUncommittedFileAsync(string path, string wd)
    {
        if (await cmd.RunAsync("git", $"checkout --force HEAD -- \"{path}\"", wd) is not Error e)
            return Result.Ok;
        if (!IsFileUnknown(e, path) && !e.Message.Contains("invalid reference: HEAD"))
            return new Error("Failed to reset", e);

        // Not in the last commit, or there is no commit yet, so a new file: out of the index, if it
        // was staged, and deleted
        if (await cmd.RunAsync("git", $"rm --cached --ignore-unmatch -q -- \"{path}\"", wd) is Error rmError)
            return new Error("Failed to reset", rmError);
        var fullPath = Path.Combine(wd, path);
        if (Result.Catch(() => File.Delete(fullPath)) is Error deleteError)
            return new Error("Failed to reset", deleteError);
        Log.Info($"File '{path}' (new/added) was removed");
        return Result.Ok;
    }

    public async Task<Result> CleanWorkingFolderAsync(string wd)
    {
        if (await cmd.RunAsync("git", "reset --hard", wd) is Error e)
            return e;

        return await cmd.RunAsync("git", "clean -fxd", wd);
    }

    public async Task<Result> UndoCommitAsync(string id, int parentIndex, string wd)
    {
        var parent = parentIndex == 0 ? "" : $"-m {parentIndex}";
        return ConflictError.ToConflict(
            await cmd.RunAsync("git", $"revert {parent} --no-commit {id}", wd),
            "The revert stopped on conflicts"
        );
    }

    public async Task<Result> UncommitLastCommitAsync(string wd)
    {
        return await cmd.RunAsync("git", "reset HEAD~1", wd);
    }

    public async Task<Result> UncommitUntilCommitAsync(string id, string wd)
    {
        return await cmd.RunAsync("git", $"reset --soft {id}", wd);
    }

    public async Task<Result> ResetHardUntilCommitAsync(string id, string wd)
    {
        return await cmd.RunAsync("git", $"reset --hard {id}", wd);
    }

    static bool IsFileUnknown(Error error, string path)
    {
        var msg = $"error: pathspec '{path}' did not match any file(s) known";
        return error.Message.StartsWith(msg);
    }
}
