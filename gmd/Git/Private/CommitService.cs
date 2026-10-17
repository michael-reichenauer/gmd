namespace gmd.Git.Private;

interface ICommitService
{
    Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<Result> CommitFilesAsync(string message, bool isAmend, IReadOnlyList<string> paths, string wd);
    Task<Result> UndoAllUncommittedChangesAsync(string wd);
    Task<Result> UndoUncommittedFileAsync(string path, string wd);
    Task<Result> CleanWorkingFolderAsync(string wd);
    Task<Result<IReadOnlyList<string>>> GetFilesToCleanAsync(string wd);
    Task<Result> AutosquashAsync(string baseId, string wd);
    Task<Result> DropCommitAsync(string id, string wd);
    Task<Result<string>> CommitTreeAsync(string filesOfId, string parentId, string message, string wd);
    Task<Result> RebaseOntoKeepingMergesAsync(string onto, string upstream, string wd);
    Task<Result<bool>> IsMergedByContentAsync(string ours, string theirs, string wd);
    Task<Result> ReorderCommitsAsync(string baseId, IReadOnlyList<string> idsOldestFirst, string wd);
    Task<Result<IReadOnlyList<string>>> GetRefsContainingAsync(string id, string wd);
    Task<Result> UndoCommitAsync(string id, int parentIndex, string wd);
    Task<Result> UncommitLastCommitAsync(string wd);
    Task<Result> UncommitUntilCommitAsync(string id, string wd);
    Task<Result> ResetBranchAsync(string name, string toId, string fromId, bool isKeep, string wd);
}

// cSpell:ignore pathspec
class CommitService : ICommitService
{
    readonly ICmd cmd;

    public CommitService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd)
    {
        // While an operation is in progress its result is already staged, and both of the ways of
        // staging here would stage an unmerged path with the conflict markers as its content —
        // 'git commit -am' on a conflicted merge succeeds and commits '<<<<<<<' into history. So
        // stage nothing and commit what the operation and the user have already staged.
        //
        // The test used to be for .git/MERGE_MSG, which a rebase with the --apply backend and
        // 'git am' do not write, so those went down the staging path.
        var isOperationInProgress = OperationState.Read(wd).IsInProgress;
        if (!isOperationInProgress)
        {
            if (await cmd.RunAsync("git", "add .", wd) is Error e)
                return e;
        }

        var amendText = isAmend ? " --amend" : "";
        var allText = isOperationInProgress ? "" : "a";
        var result = await cmd.RunAsync("git", $"commit{amendText} -{allText}m {Cmd.Quote(message)}", wd);

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

    // Commits these files only, as they are in the working tree, and leaves every other change as it
    // is, staged or not: what the commit dialog's checklist leaves ticked. They are staged first
    // ('add -A'), since a commit of paths takes only what git already tracks, so a new file would
    // be refused and a deleted one left. The paths go in a file rather than on the command line,
    // which a long list would overflow, and are literal, so a path with a '*' is not a pattern.
    // Not for an operation in progress: git refuses a commit of some paths during a merge, which is
    // committed whole.
    //
    // With no paths it commits none, i.e. a commit with no changes, which is what the amend of an
    // older commit with no file ticked makes to carry the new message (AmendOlderCommitAsync). An
    // empty pathspec file is no pathspec at all to git, which would stage and commit everything.
    public async Task<Result> CommitFilesAsync(string message, bool isAmend, IReadOnlyList<string> paths, string wd)
    {
        if (paths.Count == 0)
        {
            var amend = isAmend ? " --amend" : "";
            return await cmd.RunAsync("git", $"commit{amend} --only --allow-empty -m {Cmd.Quote(message)}", wd);
        }

        var pathsFile = Path.GetTempFileName();
        try
        {
            if (Result.Catch(() => File.WriteAllText(pathsFile, string.Join('\0', paths))) is Error writeError)
                return writeError;

            var pathspec = $"--pathspec-from-file={Cmd.Quote(pathsFile)} --pathspec-file-nul";
            var literal = new Dictionary<string, string> { ["GIT_LITERAL_PATHSPECS"] = "1" };
            if (await cmd.RunAsync("git", $"add -A {pathspec}", wd, environment: literal) is Error addError)
                return addError;

            var amendText = isAmend ? " --amend" : "";
            return await cmd.RunAsync(
                "git",
                $"commit{amendText} -m {Cmd.Quote(message)} {pathspec}",
                wd,
                environment: literal
            );
        }
        finally
        {
            Result.Catch(() => File.Delete(pathsFile));
        }
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
    // before this was picked, but no longer touches the index (DiffService.GetUncommittedDiffAsync).
    public async Task<Result> UndoUncommittedFileAsync(string path, string wd)
    {
        if (await cmd.RunAsync("git", $"checkout --force HEAD -- {Cmd.Quote(path)}", wd) is not Error e)
            return Result.Ok;
        if (!IsFileUnknown(e, path) && !e.Message.Contains("invalid reference: HEAD"))
            return new Error("Failed to reset", e);

        // Not in the last commit, or there is no commit yet, so a new file: out of the index, if it
        // was staged, and deleted
        if (await cmd.RunAsync("git", $"rm --cached --ignore-unmatch -q -- {Cmd.Quote(path)}", wd) is Error rmError)
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

    // What CleanWorkingFolderAsync would delete, as git's dry run of the same clean lists it: every
    // file git does not track, the ignored ones too, a folder named once rather than its files. A
    // path is quoted when it needs it (QuotedPath).
    public async Task<Result<IReadOnlyList<string>>> GetFilesToCleanAsync(string wd)
    {
        var result = await cmd.RunAsync("git", "clean -n -x -d", wd);
        if (result is not string output)
            return result.Error;

        const string Prefix = "Would remove ";
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith(Prefix))
            .Select(l => QuotedPath.Unquote(l[Prefix.Length..]))
            .ToList();
    }

    // Folds the 'amend!' and 'fixup!' commits on the current branch into the commits they name: a
    // rebase of the commits after the base, or of all of them with no base, in the order git's own
    // --autosquash puts them. Interactive only in name, since gmd never opens an editor
    // (Cmd.NeverOpenAnEditor), so git's plan is taken as it writes it. The changes not committed are
    // put aside and back (--autostash), and a later commit the fold leaves with nothing to change is
    // dropped. A conflict stops it part way, as any rebase, to be continued or aborted.
    public async Task<Result> AutosquashAsync(string baseId, string wd)
    {
        var onto = baseId == "" ? "--root" : baseId;
        return ConflictError.ToConflict(
            await cmd.RunAsync("git", $"rebase -i --autosquash --autostash --empty=drop {onto}", wd),
            "The amend stopped on conflicts"
        );
    }

    // Takes a commit out of the current branch: the commits after it are replayed onto its parent
    public async Task<Result> DropCommitAsync(string id, string wd) =>
        ConflictError.ToConflict(
            await cmd.RunAsync("git", $"rebase --empty=drop --onto {id}^ {id}", wd),
            "The drop stopped on conflicts"
        );

    // A new commit with the files of a commit on the given parent, made without moving a branch or
    // touching the index or the working tree, e.g. the commits from that parent up to that commit
    // made one; its id. No hook runs, as for the commits a rebase replays. It is signed when
    // commit.gpgsign says so, as 'git commit' would sign it: commit-tree does not read that setting
    // (since git 2.9), so it has to be asked for, and a branch that requires signed commits refuses
    // an unsigned one on the push.
    public async Task<Result<string>> CommitTreeAsync(string filesOfId, string parentId, string message, string wd)
    {
        var signResult = await IsSigningCommitsAsync(wd);
        if (signResult is not bool isSign)
            return signResult.Error;
        var signText = isSign ? " -S" : "";
        var args = $"commit-tree{signText} {filesOfId}^{{tree}} -p {parentId} -m {Cmd.Quote(message)}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;
        return output.Trim();
    }

    // Whether git signs the commits it makes, commit.gpgsign in any of the config files. Git exits
    // with 1 when it is not set, which is an answer, and fails on a value that is no boolean, as
    // 'git commit' then does too.
    async Task<Result<bool>> IsSigningCommitsAsync(string wd)
    {
        var result = await cmd.RunRawAsync("git", "config --type=bool --get commit.gpgsign", wd, skipLogError: true);
        if (result.ExitCode == 1)
            return false;
        if (result.ToResult() is not string output)
            return result.ToResult().Error;
        return output.Trim() == "true";
    }

    // Moves the commits of the current branch after 'upstream' onto 'onto', keeping the merges among
    // them, which a plain rebase leaves out, putting the commits they merged in on the line instead
    public async Task<Result> RebaseOntoKeepingMergesAsync(string onto, string upstream, string wd) =>
        ConflictError.ToConflict(
            await cmd.RunAsync("git", $"rebase --rebase-merges --onto {onto} {upstream}", wd),
            "The rebase stopped on conflicts"
        );

    // An interactive rebase with gmd's own todo list, one 'pick' per commit in the order given. The
    // list is written to a file of its own, and the sequence editor, which git starts through its
    // shell (bundled on Windows) with the path of the list it made, copies gmd's over it, which also
    // leaves out whatever config would have changed in git's (rebase.autoSquash). It has to be the
    // environment, set for this command on top of the 'true' that Cmd.NeverOpenAnEditor sets for every
    // other. Changes in the working tree are put aside and back (--autostash). Git has read the list
    // before it can stop on a conflict, so the file is deleted either way.
    public async Task<Result> ReorderCommitsAsync(string baseId, IReadOnlyList<string> idsOldestFirst, string wd)
    {
        var todo = Path.Join(Path.GetTempPath(), $"gmd-todo-{Guid.NewGuid():N}");
        var lines = string.Concat(idsOldestFirst.Select(id => $"pick {id}\n"));
        if (Result.Catch(() => File.WriteAllText(todo, lines)) is Error writeError)
            return writeError;
        try
        {
            var quoted = todo.Replace('\\', '/').Replace("'", "'\\''");
            var environment = new Dictionary<string, string> { ["GIT_SEQUENCE_EDITOR"] = $"cp '{quoted}'" };
            return ConflictError.ToConflict(
                await cmd.RunAsync("git", $"rebase -i --autostash {baseId}", wd, environment: environment),
                "The move stopped on conflicts"
            );
        }
        finally
        {
            Result.Catch(() => File.Delete(todo));
        }
    }

    // Whether merging 'theirs' into 'ours' would change nothing: what 'theirs' changed since the two
    // parted is in 'ours' already, as it is once squash merged or rebase merged, which git does not
    // count as merged, since the commits that went in are copies. 'git merge-tree' merges with no
    // branch, index or working tree touched, and gives the merge's tree, which is then ours. A
    // conflict means not; a git older than 2.38, which has no '--write-tree', fails. The trees of a
    // result unlike ours are written to the repository, as objects nothing refers to, which a gc
    // removes in time.
    public async Task<Result<bool>> IsMergedByContentAsync(string ours, string theirs, string wd)
    {
        var merged = await cmd.RunRawAsync(
            "git",
            $"merge-tree --write-tree --no-messages {ours} {theirs}",
            wd,
            skipLogError: true
        );
        if (merged.ExitCode == 1)
            return false; // Conflicts, which the output lists after the tree
        var mergedResult = merged.ToResult();
        if (mergedResult is not string output)
            return mergedResult.Error;

        var treeResult = await cmd.RunAsync("git", $"rev-parse {ours}^{{tree}}", wd);
        if (treeResult is not string oursTree)
            return treeResult.Error;
        return output.Split('\n')[0].Trim() == oursTree.Trim();
    }

    // The branches, remote branches and tags that have the commit, i.e. are on it or after it, as
    // full ref names, e.g. 'refs/heads/main', 'refs/remotes/origin/main' and 'refs/tags/v1'
    public async Task<Result<IReadOnlyList<string>>> GetRefsContainingAsync(string id, string wd)
    {
        var args = $"for-each-ref --contains {id} --format=%(refname) refs/heads refs/remotes refs/tags";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public async Task<Result> UndoCommitAsync(string id, int parentIndex, string wd)
    {
        var parent = parentIndex == 0 ? "" : $"-m {parentIndex}";
        return ConflictError.ToConflict(
            await cmd.RunAsync("git", $"revert {parent} --no-commit {id}", wd),
            "The revert stopped on conflicts"
        );
    }

    public async Task<Result> UncommitLastCommitAsync(string wd) => await cmd.RunAsync("git", "reset HEAD~1", wd);

    public async Task<Result> UncommitUntilCommitAsync(string id, string wd) =>
        await cmd.RunAsync("git", $"reset --soft {id}", wd);

    // Moves the current branch back to where it was, as Undo does: '--keep' moves the files too,
    // and refuses rather than overwrite a file with uncommitted changes, while '--mixed' leaves the
    // files as they are, so what the branch no longer has shows up as uncommitted changes
    // A reset has no old value to check, as update-ref has, so HEAD is read first, the branch it is
    // on and where that is: with a repo read before a switch, or before a commit made outside gmd,
    // the reset would move another branch, or take what was added since back with it
    public async Task<Result> ResetBranchAsync(string name, string toId, string fromId, bool isKeep, string wd)
    {
        var head = await cmd.RunAsync("git", "rev-parse HEAD --symbolic-full-name HEAD", wd);
        if (head is not string output)
            return head.Error;
        if (
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            is not [var id, var refName]
        )
            return new Error($"Failed to read HEAD: '{output}'");
        if (refName != $"refs/heads/{name}")
            return new Error($"'{name}' is no longer checked out, so it was left where it is");
        if (id != fromId)
            return new Error($"'{name}' has moved since it was read, so it was left where it is");

        return await cmd.RunAsync("git", $"reset {(isKeep ? "--keep" : "--mixed")} {toId}", wd);
    }

    static bool IsFileUnknown(Error error, string path)
    {
        var msg = $"error: pathspec '{path}' did not match any file(s) known";
        return error.Message.StartsWith(msg);
    }
}
