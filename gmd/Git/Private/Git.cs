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
    readonly IReflogService reflogService;
    readonly IPatchService patchService;
    readonly ISnapshotService snapshotService;
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
        IReflogService reflogService,
        IPatchService patchService,
        ISnapshotService snapshotService,
        ICmd cmd
    )
    {
        this.worktreeService = worktreeService;
        this.reflogService = reflogService;
        this.patchService = patchService;
        this.snapshotService = snapshotService;
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

    // The root of the working tree a folder is in. A linked worktree is a root of its own, even
    // when nested inside the main repository's folder — see GitDir.
    public Result<string> RootPath(string path)
    {
        var found = GitDir.Find(path);
        return found is GitDirInfo gitDir ? gitDir.RootPath : found.Error;
    }

    public Task<Result<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd) =>
        logService.GetLogAsync(maxCount, wd);

    public Task<Result<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd) =>
        logService.GetMergeLogAsync(reference, wd);

    public Task<Result<IReadOnlyList<Commit>>> GetUnreachableCommitsAsync(
        IReadOnlyList<string> ids,
        IReadOnlyList<string> alsoReached,
        string wd
    ) => logService.GetUnreachableCommitsAsync(ids, alsoReached, wd);

    public Task<Result<IReadOnlySet<string>>> GetExistingCommitIdsAsync(IReadOnlyList<string> ids, string wd) =>
        logService.GetExistingCommitIdsAsync(ids, wd);

    public Task<Result<IReadOnlyList<string>>> GetIdsChangingFilesAsync(string pathText, int maxCount, string wd) =>
        logService.GetIdsChangingFilesAsync(pathText, maxCount, wd);

    public Task<Result<IReadOnlyList<string>>> GetIdsChangingTextAsync(string text, int maxCount, string wd) =>
        logService.GetIdsChangingTextAsync(text, maxCount, wd);

    public Task<Result<IReadOnlyList<string>>> GetFilePathsAsync(string reference, string wd) =>
        logService.GetFilePathsAsync(reference, wd);

    public Task<Result<IReadOnlyList<Branch>>> GetBranchesAsync(string wd) => branchService.GetBranchesAsync(wd);

    public Task<Result<IReadOnlyList<string>>> GetMergedBranchNamesAsync(string id, string wd) =>
        branchService.GetMergedBranchNamesAsync(id, wd);

    public Task<Result<Status>> GetStatusAsync(string wd) => statusService.GetStatusAsync(wd);

    public Task<Result<Status>> GetStatusWithoutLocksAsync(string wd) => statusService.GetStatusWithoutLocksAsync(wd);

    public Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
        commitService.CommitAllChangesAsync(message, isAmend, wd);

    public Task<Result> CommitFilesAsync(string message, bool isAmend, IReadOnlyList<string> paths, string wd) =>
        commitService.CommitFilesAsync(message, isAmend, paths, wd);

    public Task<Result<CommitDiff>> GetCommitDiffAsync(string commitId, DiffOptions options, string wd) =>
        diffService.GetCommitDiffAsync(commitId, options, wd);

    public Task<Result<IReadOnlyList<ChangedFile>>> GetChangedFilesAsync(string commitId, string wd) =>
        diffService.GetChangedFilesAsync(commitId, wd);

    public Task<Result<CommitDiff[]>> GetFileDiffAsync(string path, DiffOptions options, string wd) =>
        diffService.GetFileDiffAsync(path, options, wd);

    public Task<Result<CommitDiff[]>> GetLineHistoryAsync(
        string path,
        int firstLine,
        int lastLine,
        string reference,
        DiffOptions options,
        string wd
    ) => diffService.GetLineHistoryAsync(path, firstLine, lastLine, reference, options, wd);

    public Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd) =>
        blameService.GetBlameAsync(path, reference, wd);

    public Task<Result<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        DiffOptions options,
        string wd
    ) => diffService.GetRefsDiffAsync(sha1, sha2, message, options, wd);

    public Task<Result<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        DiffOptions options,
        string wd
    ) => diffService.GetDiffRangeAsync(sha1, sha2, message, options, wd);

    public Task<Result<FilePatch>> GetUncommittedPatchAsync(string path, string wd) =>
        patchService.GetUncommittedPatchAsync(path, wd);

    public Task<Result> ApplyPatchToIndexAsync(FilePatch patch, string indexPath, string wd) =>
        patchService.ApplyToIndexAsync(patch, indexPath, wd);

    public Task<Result> ApplyPatchReversedAsync(FilePatch patch, string wd) =>
        patchService.ApplyReversedAsync(patch, wd);

    public Task<Result> CommitPatchesAsync(
        string message,
        bool isAmend,
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, FilePatch> patches,
        string wd
    ) => patchService.CommitPatchesAsync(message, isAmend, paths, patches, wd);

    public Task<Result> RunDiffToolAsync(string path, string wd) => diffService.RunDiffToolAsync(path, wd);

    public Task<Result> RunMergeToolAsync(string path, string wd) => diffService.RunMergeToolAsync(path, wd);

    public Task<Result<CommitDiff>> GetUncommittedDiffAsync(DiffOptions options, string wd) =>
        diffService.GetUncommittedDiffAsync(options, wd);

    public Task<Result> FetchAsync(string wd) => remoteService.FetchAsync(wd);

    public Task<Result<string>> GetRemoteUrlAsync(string wd) => remoteService.GetRemoteUrlAsync(wd);

    public Task<Result> PushBranchAsync(string name, string wd) => remoteService.PushBranchAsync(name, wd);

    public Task<Result> PushForceAsync(string name, string remoteName, string expectedId, string wd) =>
        remoteService.PushForceAsync(name, remoteName, expectedId, wd);

    public Task<Result> PushRestoreAsync(string name, string oldId, string expectedId, string wd) =>
        remoteService.PushRestoreAsync(name, oldId, expectedId, wd);

    public Task<Result> PullCurrentBranchAsync(string wd) => remoteService.PullCurrentBranchAsync(wd);

    public Task<Result<bool>> IsPullWayConfiguredAsync(string branchName, string wd) =>
        remoteService.IsPullWayConfiguredAsync(branchName, wd);

    public Task<Result> SetPullRebaseAsync(bool isRebase, string wd) => remoteService.SetPullRebaseAsync(isRebase, wd);

    public Task<Result> PullBranchAsync(string name, string wd) => remoteService.PullBranchAsync(name, wd);

    public Task<Result> CloneAsync(string uri, string path, string wd) => remoteService.CloneAsync(uri, path, wd);

    public Task<Result> InitRepoAsync(string path, string wd) => repoService.InitAsync(path);

    public bool HasCommitGraph(string wd) => repoService.HasCommitGraph(wd);

    public Task<Result> WriteCommitGraphAsync(string wd) => repoService.WriteCommitGraphAsync(wd);

    public Task<Result> CheckoutAsync(string name, string wd) => branchService.CheckoutAsync(name, wd);

    public Task<Result> MergeBranchAsync(string name, string wd) => branchService.MergeBranchAsync(name, wd);

    public Task<Result> RebaseBranchAsync(string name, string wd) => branchService.RebaseBranchAsync(name, wd);

    public Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd) =>
        branchService.RebaseOntoAsync(newBase, oldBase, wd);

    public Task<Result> RebaseOntoRemoteAsync(string remoteName, string forkPointId, string wd) =>
        branchService.RebaseOntoRemoteAsync(remoteName, forkPointId, wd);

    public Task<Result> CherryPickCommitsAsync(IReadOnlyList<string> idsOldestFirst, string wd) =>
        branchService.CherryPickCommitsAsync(idsOldestFirst, wd);

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

    public Task<Result<ConflictSides>> GetConflictSidesAsync(string wd) => conflictService.GetConflictSidesAsync(wd);

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

    public Task<Result> MoveBranchAsync(string name, string toId, string fromId, string message, string wd) =>
        branchService.MoveBranchAsync(name, toId, fromId, message, wd);

    public Task<Result> SetUpstreamAsync(string name, string remoteName, string wd) =>
        branchService.SetUpstreamAsync(name, remoteName, wd);

    public Task<Result> DeleteRemoteBranchAsync(string name, string wd) =>
        remoteService.DeleteRemoteBranchAsync(name, wd);

    public Task<Result<IReadOnlyList<Tag>>> GetTagsAsync(string wd) => tagService.GetTagsAsync(wd);

    public Task<Result> UndoAllUncommittedChangesAsync(string wd) => commitService.UndoAllUncommittedChangesAsync(wd);

    public Task<Result> UndoUncommittedFileAsync(string path, string wd) =>
        commitService.UndoUncommittedFileAsync(path, wd);

    public Task<Result> CleanWorkingFolderAsync(string wd) => commitService.CleanWorkingFolderAsync(wd);

    public Task<Result<IReadOnlyList<string>>> GetIgnoredFilesAsync(string wd) =>
        commitService.GetIgnoredFilesAsync(wd);

    public Task<Result> AutosquashAsync(string baseId, string wd) => commitService.AutosquashAsync(baseId, wd);

    public Task<Result> DropCommitAsync(string id, string wd) => commitService.DropCommitAsync(id, wd);

    public Task<Result<string>> CommitTreeAsync(string filesOfId, string parentId, string message, string wd) =>
        commitService.CommitTreeAsync(filesOfId, parentId, message, wd);

    public Task<Result> RebaseOntoKeepingMergesAsync(string onto, string upstream, string wd) =>
        commitService.RebaseOntoKeepingMergesAsync(onto, upstream, wd);

    public Task<Result> ReorderCommitsAsync(string baseId, IReadOnlyList<string> idsOldestFirst, string wd) =>
        commitService.ReorderCommitsAsync(baseId, idsOldestFirst, wd);

    public Task<Result<bool>> IsMergedByContentAsync(string ours, string theirs, string wd) =>
        commitService.IsMergedByContentAsync(ours, theirs, wd);

    public Task<Result<IReadOnlyList<string>>> GetRefsContainingAsync(string id, string wd) =>
        commitService.GetRefsContainingAsync(id, wd);

    public Task<Result> UndoCommitAsync(string id, int parentIndex, string wd) =>
        commitService.UndoCommitAsync(id, parentIndex, wd);

    public Task<Result> UncommitLastCommitAsync(string wd) => commitService.UncommitLastCommitAsync(wd);

    public Task<Result> UncommitUntilCommitAsync(string id, string wd) =>
        commitService.UncommitUntilCommitAsync(id, wd);

    public Task<Result<string>> GetValueAsync(string key, string wd) => keyValueService.GetValueAsync(key, wd);

    public Task<Result> DeleteValueAsync(string key, string wd) => keyValueService.DeleteValueAsync(key, wd);

    public Task<Result<string>> GetOriginValueAsync(string key, string wd) =>
        keyValueService.GetOriginValueAsync(key, wd);

    public Task<Result<bool>> PushValueAsync(string key, string value, string wd) =>
        keyValueService.PushValueAsync(key, value, wd);

    public Task<Result> FetchValueAsync(string key, string wd) => keyValueService.FetchValueAsync(key, wd);

    public Task<Result> StashAsync(string message, string wd) => stashService.StashAsync(message, wd);

    public Task<Result> StashPopAsync(string name, string wd) => stashService.PopAsync(name, wd);

    public Task<Result> StashApplyAsync(string name, string wd) => stashService.ApplyAsync(name, wd);

    public Task<Result> StashDropAsync(string name, string wd) => stashService.DropAsync(name, wd);

    public Task<Result> StashStoreAsync(string id, string message, string wd) =>
        stashService.StoreAsync(id, message, wd);

    public Task<Result<string>> WriteWorkingTreeAsync(string wd) => snapshotService.WriteWorkingTreeAsync(wd);

    public Task<Result<string>> WriteCommitAsync(
        string tree,
        IReadOnlyList<string> parents,
        string message,
        string wd
    ) => snapshotService.WriteCommitAsync(tree, parents, message, wd);

    public Task<Result> LogRefAsync(string refName, string id, string message, string wd) =>
        snapshotService.LogRefAsync(refName, id, message, wd);

    public Task<Result<IReadOnlyList<RefLogRecord>>> GetRefLogAsync(string refName, string wd) =>
        snapshotService.GetRefLogAsync(refName, wd);

    public Task<Result> ApplyDiffAsync(string fromId, string toId, string wd) =>
        snapshotService.ApplyDiffAsync(fromId, toId, wd);

    public Task<Result<IReadOnlyList<Stash>>> GetStashesAsync(string wd) => stashService.ListAsync(wd);

    public Task<Result<CommitDiff>> GetStashDiffAsync(string name, DiffOptions options, string wd) =>
        diffService.GetStashDiffAsync(name, options, wd);

    public Task<Result> AddTagAsync(string name, string commitId, string wd) =>
        tagService.AddTagAsync(name, commitId, wd);

    public Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, string wd) =>
        tagService.AddAnnotatedTagAsync(name, message, commitId, wd);

    public Task<Result> RemoveTagAsync(string name, string wd) => tagService.RemoveTagAsync(name, wd);

    public Task<Result> ResetBranchAsync(string name, string toId, string fromId, bool isKeep, string wd) =>
        commitService.ResetBranchAsync(name, toId, fromId, isKeep, wd);

    public Task<Result> PushTagAsync(string name, string wd) => remoteService.PushTagAsync(name, wd);

    public Task<Result<IReadOnlyDictionary<string, string>>> GetTrackedRemoteTagsAsync(string wd) =>
        tagService.GetTrackedRemoteTagsAsync(wd);

    public Task<Result> DeleteRemoteTagAsync(string name, string wd) => remoteService.DeleteRemoteTagAsync(name, wd);

    public Task<Result<IReadOnlyList<Worktree>>> GetWorktreesAsync(string wd) => worktreeService.ListAsync(wd);

    public Task<Result<IReadOnlyList<ReflogEntry>>> GetReflogAsync(string wd) => reflogService.GetReflogAsync(wd);

    public Task<Result<IReadOnlyList<ReflogEntry>>> GetRefReflogsAsync(IReadOnlyList<string> refs, string wd) =>
        reflogService.GetRefReflogsAsync(refs, wd);

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

    public async Task<Result<string>> GetVersionAsync()
    {
        var result = await cmd.RunAsync("git", "version", "", true, true);
        if (result is not string output)
            return result.Error;

        return output.TrimPrefix("git version ");
    }

    // Git exits with 1 when user.name is not set, which is an answer
    public async Task<Result<string>> GetCurrentAuthorAsync(string wd)
    {
        var result = await cmd.RunRawAsync("git", "config user.name", wd, true, true);
        if (result.ExitCode == 1)
            return "";
        if (result.ToResult() is not string name)
            return result.ToResult().Error;

        return name.Trim();
    }
}
