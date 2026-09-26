using gmd.Git;

namespace gmdTest.Fixtures;

// A double for IGit, the git area services the server layer calls. Only the members the pipeline
// needs are implemented; every other member throws, so a test that starts depending on git fails
// loudly rather than silently working on an empty result.
class FakeGit : IGit
{
    readonly Status status;

    public FakeGit(Status status) => this.status = status;

    // For tests that only use the key/value storage below, where the status is irrelevant
    public FakeGit()
        : this(new Status(0, 0, 0, 0, 0, GitOperation.None, "", "", "", 0, 0, true, [], [], [], [], [], [])) { }

    public string CurrentAuthor => "Test Author";

    // The status of the repo, or of another worktree's folder when one is set for that path
    public Task<Result<Status>> GetStatusAsync(string wd) =>
        Task.FromResult<Result<Status>>(StatusByPath.TryGetValue(wd, out var s) ? s : status);

    // The lock-free read the other worktrees get; which folders were read that way is recorded
    public Task<Result<Status>> GetStatusWithoutLocksAsync(string wd)
    {
        StatusWithoutLocksPaths.Add(wd);
        return GetStatusAsync(wd);
    }

    public Dictionary<string, Status> StatusByPath { get; } = [];
    public List<string> StatusWithoutLocksPaths { get; } = [];

    // The worktrees 'git worktree list' would report, and every worktree write made, in order
    public List<Worktree> Worktrees { get; } = [];

    public List<string> WorktreeCalls { get; } = [];

    public Task<Result<IReadOnlyList<Worktree>>> GetWorktreesAsync(string wd) =>
        Task.FromResult<Result<IReadOnlyList<Worktree>>>(Worktrees.ToList());

    public Task<Result> AddWorktreeAsync(string path, string branchName, bool isNewBranch, string startPoint, string wd)
    {
        WorktreeCalls.Add($"add {path} {branchName} {(isNewBranch ? "new" : "existing")} {startPoint}".TrimEnd());
        return Task.FromResult(Result.Ok);
    }

    public Task<Result> RemoveWorktreeAsync(string path, bool isForce, string wd)
    {
        WorktreeCalls.Add($"remove {path}{(isForce ? " --force" : "")}");
        return Task.FromResult(Result.Ok);
    }

    public Task<Result> PruneWorktreesAsync(string wd)
    {
        WorktreeCalls.Add("prune");
        return Task.FromResult(Result.Ok);
    }

    // The git key/value storage, i.e. the 'refs/gmd-metadata-key-value/<key>' refs MetaDataService
    // stores the user's branch choices in. Values is what this repo has, RemoteValues what the
    // remote server has: a pull copies remote to local and a push copies local to remote, which is
    // all MetaDataService needs from git.
    public Dictionary<string, string> Values { get; } = [];

    public Dictionary<string, string> RemoteValues { get; } = [];

    // Every key/value call made, in order, so tests can assert what was read, written and synced
    public List<string> ValueCalls { get; } = [];

    public Task<Result<string>> GetValueAsync(string key, string wd)
    {
        ValueCalls.Add($"get {key}");
        return Task.FromResult<Result<string>>(
            Values.TryGetValue(key, out var value)
                ? value
                // The message git gives for a ref that does not exist, which MetaDataService reads
                // as 'no local value yet' rather than as a failure
                : new Error($"fatal: Not a valid object name refs/gmd-metadata-key-value/{key}")
        );
    }

    public Task<Result> SetValueAsync(string key, string value, string wd)
    {
        ValueCalls.Add($"set {key}");
        Values[key] = value;
        return Task.FromResult(Result.Ok);
    }

    public Task<Result> PushValueAsync(string key, string wd)
    {
        ValueCalls.Add($"push {key}");
        if (!Values.TryGetValue(key, out var value))
            return Task.FromResult<Result>(new Error("error: src refspec does not match any"));

        RemoteValues[key] = value;
        return Task.FromResult(Result.Ok);
    }

    public Task<Result> PullValueAsync(string key, string wd)
    {
        ValueCalls.Add($"pull {key}");
        if (!RemoteValues.TryGetValue(key, out var value))
            return Task.FromResult<Result>(new Error($"fatal: couldn't find remote ref {key}"));

        Values[key] = value;
        return Task.FromResult(Result.Ok);
    }

    // The rest of IGit is not reachable from the pipeline the tests drive
    public Result<string> RootPath(string path) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<string>>> GetIgnoredPathsAsync(IReadOnlyList<string> paths, string wd) =>
        throw new NotSupportedException();

    public Task<Result<string>> Version() => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd) =>
        throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd) =>
        throw new NotSupportedException();

    public Task<Result<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Branch>>> GetBranchesAsync(string wd) => throw new NotSupportedException();

    public Task<Result> CommitAllChangesAsync(string message, bool isAmend, string wd) =>
        throw new NotSupportedException();

    public Task<Result<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd) =>
        throw new NotSupportedException();

    public Task<Result<CommitDiff>> GetUncommittedDiff(int contextLines, string wd) =>
        throw new NotSupportedException();

    public Task<Result<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd) =>
        throw new NotSupportedException();

    public Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd) =>
        throw new NotSupportedException();

    public Task<Result<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    ) => throw new NotSupportedException();

    public Task<Result<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    ) => throw new NotSupportedException();

    public Task<Result> RunDiffToolAsync(string path, string wd) => throw new NotSupportedException();

    public Task<Result> RunMergeToolAsync(string path, string wd) => throw new NotSupportedException();

    public Task<Result> FetchAsync(string wd) => throw new NotSupportedException();

    // The commits each path of a search changed, as 'git log -- <path>' would list them, and every
    // path asked about, in order
    public Dictionary<string, IReadOnlyList<string>> IdsChangingFiles { get; } = [];
    public List<string> IdsChangingFilesCalls { get; } = [];

    public Task<Result<IReadOnlyList<string>>> GetIdsChangingFilesAsync(string pathText, int maxCount, string wd)
    {
        IdsChangingFilesCalls.Add(pathText);
        IReadOnlyList<string> ids = IdsChangingFiles.TryGetValue(pathText, out var found) ? found : [];
        return Task.FromResult<Result<IReadOnlyList<string>>>(ids.ToList());
    }

    public Task<Result<string>> GetRemoteUrlAsync(string wd) => throw new NotSupportedException();

    public Task<Result> PushBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> PushCurrentBranchAsync(bool isForce, string wd) => throw new NotSupportedException();

    public Task<Result> PullCurrentBranchAsync(string wd) => throw new NotSupportedException();

    public Task<Result<bool>> IsPullWayConfiguredAsync(string branchName, string wd) =>
        throw new NotSupportedException();

    public Task<Result> SetPullRebaseAsync(bool isRebase, string wd) => throw new NotSupportedException();

    public Task<Result> PullBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> PushRefForceAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> PullRefAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> CloneAsync(string uri, string path, string wd) => throw new NotSupportedException();

    public Task<Result> InitRepoAsync(string path, string wd) => throw new NotSupportedException();

    public Task<Result> CheckoutAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> MergeBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> RebaseBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> RebaseOntoAsync(string newBase, string oldBase, string wd) => throw new NotSupportedException();

    public Task<Result> CherryPickAsync(string sha, string wd) => throw new NotSupportedException();

    public Task<Result> AbortOperationAsync(string wd) => throw new NotSupportedException();

    public Task<Result> ContinueOperationAsync(string wd) => throw new NotSupportedException();

    public Task<Result> SkipOperationAsync(string wd) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd) =>
        throw new NotSupportedException();

    public Task<Result<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, string wd) =>
        throw new NotSupportedException();

    public Task<Result<ConflictFile>> WithBaseAsync(ConflictFile file, string wd) => throw new NotSupportedException();

    public Task<Result> WriteConflictFileAsync(ConflictFile file, string wd) => throw new NotSupportedException();

    public Task<Result> ResolveConflictFileAsync(
        string path,
        ConflictKind kind,
        IReadOnlyList<HunkResolution> choices,
        string wd
    ) => throw new NotSupportedException();

    public Task<Result> MarkResolvedAsync(string path, string wd) => throw new NotSupportedException();

    public Task<Result> UnresolveAsync(string path, string wd) => throw new NotSupportedException();

    public Task<Result> UseWholeFileAsync(string path, bool isOurs, string wd) => throw new NotSupportedException();

    public Task<Result> DeleteConflictedAsync(string path, string wd) => throw new NotSupportedException();

    public Task<Result> CreateBranchAsync(string name, bool isCheckout, string wd) => throw new NotSupportedException();

    // Recorded rather than thrown, so a test can check what creating a branch remembered
    public List<string> CreateBranchCalls { get; } = [];

    public Task<Result> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd)
    {
        CreateBranchCalls.Add($"{name} at {sha.Sid()}");
        return Task.FromResult(Result.Ok);
    }

    // Recorded rather than thrown, so a test can check what the rename did to the branch choices
    public List<string> RenameCalls { get; } = [];

    public Task<Result> RenameBranchAsync(string oldName, string newName, string wd)
    {
        RenameCalls.Add($"{oldName} -> {newName}");
        return Task.FromResult(Result.Ok);
    }

    public Task<Result> DeleteLocalBranchAsync(string name, bool isForced, string wd) =>
        throw new NotSupportedException();

    public Task<Result> DeleteRemoteBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Tag>>> GetTagsAsync(string wd) => throw new NotSupportedException();

    public Task<Result> UndoAllUncommittedChangesAsync(string wd) => throw new NotSupportedException();

    public Task<Result> UndoUncommittedFileAsync(string path, string wd) => throw new NotSupportedException();

    public Task<Result> CleanWorkingFolderAsync(string wd) => throw new NotSupportedException();

    public Task<Result> UndoCommitAsync(string id, int parentIndex, string wd) => throw new NotSupportedException();

    public Task<Result> UncommitLastCommitAsync(string wd) => throw new NotSupportedException();

    public Task<Result> UncommitUntilCommitAsync(string id, string wd) => throw new NotSupportedException();

    public Task<Result> StashAsync(string message, string wd) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Stash>>> GetStashesAsync(string wd) => throw new NotSupportedException();

    public Task<Result> StashPopAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> StashDropAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd) =>
        throw new NotSupportedException();

    public Task<Result> AddTagAsync(string name, string commitId, string wd) => throw new NotSupportedException();

    public Task<Result> AddAnnotatedTagAsync(string name, string message, string commitID, string wd) =>
        throw new NotSupportedException();

    public Task<Result> RemoveTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> PushTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> DeleteRemoteTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<Result> ResetHardUntilCommitAsync(string id, string wd) => throw new NotSupportedException();
}
