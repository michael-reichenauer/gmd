using IOPath = System.IO.Path;

namespace gmd.Git.Private;

[SingleInstance]
internal class Git : IGit
{
    readonly ILogService logService;
    readonly IBranchService branchService;
    readonly IStatusService statusService;
    readonly ICommitService commitService;
    readonly IDiffService diffService;
    readonly IRemoteService remoteService;
    readonly IRepoService repoService;
    readonly ITagService tagService;
    readonly IKeyValueService keyValueService;
    readonly IStashService stashService;
    readonly IBlameService blameService;
    readonly IConflictService conflictService;
    readonly IWorktreeService worktreeService;
    readonly ICmd cmd;

    public Git(
        ILogService logService,
        IBranchService branchService,
        IStatusService statusService,
        ICommitService commitService,
        IDiffService diffService,
        IRemoteService remoteService,
        IRepoService repoService,
        ITagService tagService,
        IKeyValueService keyValueService,
        IStashService stashService,
        IBlameService blameService,
        IConflictService conflictService,
        IWorktreeService worktreeService,
        ICmd cmd
    )
    {
        this.worktreeService = worktreeService;
        this.logService = logService;
        this.branchService = branchService;
        this.statusService = statusService;
        this.commitService = commitService;
        this.diffService = diffService;
        this.remoteService = remoteService;
        this.repoService = repoService;
        this.tagService = tagService;
        this.keyValueService = keyValueService;
        this.stashService = stashService;
        this.blameService = blameService;
        this.conflictService = conflictService;
        this.cmd = cmd;
    }

    public string CurrentAuthor { get; private set; } = "";

    public Result<string> RootPath(string path) => RootPathDir(path);

    public async Task<Result<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd)
    {
        await SetCurrentAuthorAsync(wd);
        return await logService.GetLogAsync(maxCount, wd);
    }

    public Task<Result<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd) =>
        logService.GetMergeLogAsync(reference, wd);

    public Task<Result<IReadOnlyList<string>>> GetIdsChangingFilesAsync(string pathText, int maxCount, string wd) =>
        logService.GetIdsChangingFilesAsync(pathText, maxCount, wd);

    public Task<Result<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        logService.GetFileAsync(reference, wd);

    public Task<Result<IReadOnlyList<Branch>>> GetBranchesAsync(string wd) => branchService.GetBranchesAsync(wd);

    public Task<Result<Status>> GetStatusAsync(string wd) => statusService.GetStatusAsync(wd);

    public Task<Result<Status>> GetStatusWithoutLocksAsync(string wd) => statusService.GetStatusWithoutLocksAsync(wd);

    public Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
        commitService.CommitAllChangesAsync(message, isAmend, wd);

    public Task<Result<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd) =>
        diffService.GetCommitDiffAsync(commitId, contextLines, wd);

    public Task<Result<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd) =>
        diffService.GetFileDiffAsync(path, contextLines, wd);

    public Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd) =>
        blameService.GetBlameAsync(path, reference, wd);

    public Task<Result<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    ) => diffService.GetRefsDiffAsync(sha1, sha2, message, contextLines, wd);

    public Task<Result<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    ) => diffService.GetDiffRangeAsync(sha1, sha2, message, contextLines, wd);

    public Task<Result> RunDiffToolAsync(string path, string wd) => diffService.RunDiffToolAsync(path, wd);

    public Task<Result> RunMergeToolAsync(string path, string wd) => diffService.RunMergeToolAsync(path, wd);

    public Task<Result<CommitDiff>> GetUncommittedDiff(int contextLines, string wd) =>
        diffService.GetUncommittedDiff(contextLines, wd);

    public Task<Result> FetchAsync(string wd) => remoteService.FetchAsync(wd);

    public Task<Result<string>> GetRemoteUrlAsync(string wd) => remoteService.GetRemoteUrlAsync(wd);

    public Task<Result> PushBranchAsync(string name, string wd) => remoteService.PushBranchAsync(name, wd);

    public Task<Result> PushCurrentBranchAsync(bool isForce, string wd) =>
        remoteService.PushCurrentBranchAsync(isForce, wd);

    public Task<Result> PushRefForceAsync(string name, string wd) => remoteService.PushRefForceAsync(name, wd);

    public Task<Result> PullRefAsync(string name, string wd) => remoteService.PullRefAsync(name, wd);

    public Task<Result> PullCurrentBranchAsync(string wd) => remoteService.PullCurrentBranchAsync(wd);

    public Task<Result> PullBranchAsync(string name, string wd) => remoteService.PullBranchAsync(name, wd);

    public Task<Result> CloneAsync(string uri, string path, string wd) => remoteService.CloneAsync(uri, path, wd);

    public Task<Result> InitRepoAsync(string path, string wd) => repoService.InitAsync(path, false);

    public Task<Result> CheckoutAsync(string name, string wd) => branchService.CheckoutAsync(name, wd);

    public Task<Result> MergeBranchAsync(string name, string wd) => branchService.MergeBranchAsync(name, wd);

    public Task<Result> RebaseBranchAsync(string name, string wd) => branchService.RebaseBranchAsync(name, wd);

    public Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd) =>
        branchService.RebaseOntoAsync(newBase, oldBase, wd);

    public Task<Result> CherryPickAsync(string sha, string wd) => branchService.CherryPickAsync(sha, wd);

    public Task<Result> AbortOperationAsync(string wd) => conflictService.AbortOperationAsync(wd);

    public Task<Result> ContinueOperationAsync(string wd) => conflictService.ContinueOperationAsync(wd);

    public Task<Result> SkipOperationAsync(string wd) => conflictService.SkipOperationAsync(wd);

    public Task<Result<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd) =>
        conflictService.GetLeftoverMarkerPathsAsync(wd);

    public Task<Result<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, string wd) =>
        conflictService.GetConflictFileAsync(path, kind, wd);

    public Task<Result<ConflictFile>> WithBaseAsync(ConflictFile file, string wd) =>
        conflictService.WithBaseAsync(file, wd);

    public Task<Result> WriteConflictFileAsync(ConflictFile file, string wd) => conflictService.WriteAsync(file, wd);

    public Task<Result> ResolveConflictFileAsync(
        string path,
        ConflictKind kind,
        IReadOnlyList<HunkResolution> choices,
        string wd
    ) => conflictService.ResolveAsync(path, kind, choices, wd);

    public Task<Result> MarkResolvedAsync(string path, string wd) => conflictService.MarkResolvedAsync(path, wd);

    public Task<Result> UnresolveAsync(string path, string wd) => conflictService.UnresolveAsync(path, wd);

    public Task<Result> UseWholeFileAsync(string path, bool isOurs, string wd) =>
        conflictService.UseWholeFileAsync(path, isOurs, wd);

    public Task<Result> DeleteConflictedAsync(string path, string wd) =>
        conflictService.DeleteConflictedAsync(path, wd);

    public Task<Result> CreateBranchAsync(string name, bool isCheckout, string wd) =>
        branchService.CreateBranchAsync(name, isCheckout, wd);

    public Task<Result> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd) =>
        branchService.CreateBranchFromCommitAsync(name, sha, isCheckout, wd);

    public Task<Result> RenameBranchAsync(string oldName, string newName, string wd) =>
        branchService.RenameBranchAsync(oldName, newName, wd);

    public Task<Result> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        branchService.DeleteLocalBranchAsync(name, isForced, wd);

    public Task<Result> DeleteRemoteBranchAsync(string name, string wd) =>
        remoteService.DeleteRemoteBranchAsync(name, wd);

    public Task<Result<IReadOnlyList<Tag>>> GetTagsAsync(string wd) => tagService.GetTagsAsync(wd);

    public Task<Result> UndoAllUncommittedChangesAsync(string wd) => commitService.UndoAllUncommittedChangesAsync(wd);

    public Task<Result> UndoUncommittedFileAsync(string path, string wd) =>
        commitService.UndoUncommittedFileAsync(path, wd);

    public Task<Result> CleanWorkingFolderAsync(string wd) => commitService.CleanWorkingFolderAsync(wd);

    public Task<Result> UndoCommitAsync(string id, int parentIndex, string wd) =>
        commitService.UndoCommitAsync(id, parentIndex, wd);

    public Task<Result> UncommitLastCommitAsync(string wd) => commitService.UncommitLastCommitAsync(wd);

    public Task<Result> UncommitUntilCommitAsync(string id, string wd) =>
        commitService.UncommitUntilCommitAsync(id, wd);

    public Task<Result<string>> GetValueAsync(string key, string wd) => keyValueService.GetValueAsync(key, wd);

    public Task<Result> SetValueAsync(string key, string value, string wd) =>
        keyValueService.SetValueAsync(key, value, wd);

    public Task<Result> PushValueAsync(string key, string wd) => keyValueService.PushValueAsync(key, wd);

    public Task<Result> PullValueAsync(string key, string wd) => keyValueService.PullValueAsync(key, wd);

    public Task<Result> StashAsync(string message, string wd) => stashService.StashAsync(message, wd);

    public Task<Result> StashPopAsync(string name, string wd) => stashService.PopAsync(name, wd);

    public Task<Result> StashDropAsync(string name, string wd) => stashService.DropAsync(name, wd);

    public Task<Result<IReadOnlyList<Stash>>> GetStashesAsync(string wd) => stashService.ListAsync(wd);

    public Task<Result<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd) =>
        diffService.GetStashDiffAsync(name, contextLines, wd);

    public Task<Result> AddTagAsync(string name, string commitId, string wd) =>
        tagService.AddTagAsync(name, commitId, wd);

    public Task<Result> AddAnnotatedTagAsync(string name, string message, string commitID, string wd) =>
        tagService.AddAnnotatedTagAsync(name, message, commitID, wd);

    public Task<Result> RemoveTagAsync(string name, string wd) => tagService.RemoveTagAsync(name, wd);

    public Task<Result> ResetHardUntilCommitAsync(string id, string wd) =>
        commitService.ResetHardUntilCommitAsync(id, wd);

    public Task<Result> PushTagAsync(string name, string wd) => remoteService.PushTagAsync(name, wd);

    public Task<Result> DeleteRemoteTagAsync(string name, string wd) => remoteService.DeleteRemoteTagAsync(name, wd);

    public Task<Result<IReadOnlyList<Worktree>>> GetWorktreesAsync(string wd) => worktreeService.ListAsync(wd);

    public Task<Result> AddWorktreeAsync(
        string path,
        string branchName,
        bool isNewBranch,
        string startPoint,
        string wd
    ) =>
        isNewBranch
            ? worktreeService.AddNewBranchAsync(path, branchName, startPoint, wd)
            : worktreeService.AddAsync(path, branchName, wd);

    public Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd) =>
        worktreeService.RemoveAsync(path, isForce, wd);

    public Task<Result> PruneWorktreesAsync(string wd) => worktreeService.PruneAsync(wd);

    public Task<Result<IReadOnlyList<string>>> GetIgnoredPathsAsync(IReadOnlyList<string> paths, string wd) =>
        worktreeService.GetIgnoredAsync(paths, wd);

    public async Task<Result<string>> Version()
    {
        var result = await cmd.RunAsync("git", "version", "", true, true);
        if (result is not string output)
            return result.Error;

        return output.TrimPrefix("git version ");
    }

    private async Task SetCurrentAuthorAsync(string path)
    {
        if (await cmd.RunAsync("git", "config user.name", path, true, true) is not string output)
            return;
        Log.Info($"user {output}");
        CurrentAuthor = output.Trim();
    }

    // The root of the working tree a folder is in. A linked worktree is a root of its own, even
    // when nested inside the main repository's folder — see GitDir.
    public static Result<string> RootPathDir(string path)
    {
        var found = GitDir.Find(path);
        if (found is not GitDirInfo gitDir)
            return found.Error;
        return gitDir.RootPath;
    }
}
