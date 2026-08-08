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
        : this(new Status(0, 0, 0, 0, 0, GitOperation.None, "", "", "", 0, 0, [], [], [], [], [], [])) { }

    public string CurrentAuthor => "Test Author";

    public Task<R<Status>> GetStatusAsync(string wd) => Task.FromResult<R<Status>>(status);

    // The git key/value storage, i.e. the 'refs/gmd-metadata-key-value/<key>' refs MetaDataService
    // stores the user's branch choices in. Values is what this repo has, RemoteValues what the
    // remote server has: a pull copies remote to local and a push copies local to remote, which is
    // all MetaDataService needs from git.
    public Dictionary<string, string> Values { get; } = [];

    public Dictionary<string, string> RemoteValues { get; } = [];

    // Every key/value call made, in order, so tests can assert what was read, written and synced
    public List<string> ValueCalls { get; } = [];

    public Task<R<string>> GetValueAsync(string key, string wd)
    {
        ValueCalls.Add($"get {key}");
        return Task.FromResult<R<string>>(
            Values.TryGetValue(key, out var value)
                ? value
                // The message git gives for a ref that does not exist, which MetaDataService reads
                // as 'no local value yet' rather than as a failure
                : R.Error($"fatal: Not a valid object name refs/gmd-metadata-key-value/{key}")
        );
    }

    public Task<R> SetValueAsync(string key, string value, string wd)
    {
        ValueCalls.Add($"set {key}");
        Values[key] = value;
        return Task.FromResult(R.Ok);
    }

    public Task<R> PushValueAsync(string key, string wd)
    {
        ValueCalls.Add($"push {key}");
        if (!Values.TryGetValue(key, out var value))
            return Task.FromResult<R>(R.Error("error: src refspec does not match any"));

        RemoteValues[key] = value;
        return Task.FromResult(R.Ok);
    }

    public Task<R> PullValueAsync(string key, string wd)
    {
        ValueCalls.Add($"pull {key}");
        if (!RemoteValues.TryGetValue(key, out var value))
            return Task.FromResult<R>(R.Error($"fatal: couldn't find remote ref {key}"));

        Values[key] = value;
        return Task.FromResult(R.Ok);
    }

    // The rest of IGit is not reachable from the pipeline the tests drive
    public R<string> RootPath(string path) => throw new NotSupportedException();

    public Task<R<string>> Version() => throw new NotSupportedException();

    public Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd) => throw new NotSupportedException();

    public Task<R<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd) =>
        throw new NotSupportedException();

    public Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd) =>
        throw new NotSupportedException();

    public Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd) => throw new NotSupportedException();

    public Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd) =>
        throw new NotSupportedException();

    public Task<R<CommitDiff>> GetUncommittedDiff(int contextLines, string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd) =>
        throw new NotSupportedException();

    public Task<R<Blame>> GetBlameAsync(string path, string reference, string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff>> GetPreviewMergeDiffAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    ) => throw new NotSupportedException();

    public Task<R<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    ) => throw new NotSupportedException();

    public Task<R> RunDiffToolAsync(string path, string wd) => throw new NotSupportedException();

    public Task<R> RunMergeToolAsync(string path, string wd) => throw new NotSupportedException();

    public Task<R> FetchAsync(string wd) => throw new NotSupportedException();

    public Task<R> PushBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> PushCurrentBranchAsync(bool isForce, string wd) => throw new NotSupportedException();

    public Task<R> PullCurrentBranchAsync(string wd) => throw new NotSupportedException();

    public Task<R> PullBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> PushRefForceAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> PullRefAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> CloneAsync(string uri, string path, string wd) => throw new NotSupportedException();

    public Task<R> InitRepoAsync(string path, string wd) => throw new NotSupportedException();

    public Task<R> CheckoutAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> MergeBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> RebaseBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> RebaseOntoAsync(string newBase, string oldBase, string wd) => throw new NotSupportedException();

    public Task<R> CherryPickAsync(string sha, string wd) => throw new NotSupportedException();

    public Task<R> AbortOperationAsync(string wd) => throw new NotSupportedException();

    public Task<R> ContinueOperationAsync(string wd) => throw new NotSupportedException();

    public Task<R> SkipOperationAsync(string wd) => throw new NotSupportedException();

    public Task<R> CreateBranchAsync(string name, bool isCheckout, string wd) => throw new NotSupportedException();

    public Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd) =>
        throw new NotSupportedException();

    // Recorded rather than thrown, so a test can check what the rename did to the branch choices
    public List<string> RenameCalls { get; } = [];

    public Task<R> RenameBranchAsync(string oldName, string newName, string wd)
    {
        RenameCalls.Add($"{oldName} -> {newName}");
        return Task.FromResult(R.Ok);
    }

    public Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd) => throw new NotSupportedException();

    public Task<R> DeleteRemoteBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd) => throw new NotSupportedException();

    public Task<R> UndoAllUncommittedChangesAsync(string wd) => throw new NotSupportedException();

    public Task<R> UndoUncommittedFileAsync(string path, string wd) => throw new NotSupportedException();

    public Task<R> CleanWorkingFolderAsync(string wd) => throw new NotSupportedException();

    public Task<R> UndoCommitAsync(string id, int parentIndex, string wd) => throw new NotSupportedException();

    public Task<R> UncommitLastCommitAsync(string wd) => throw new NotSupportedException();

    public Task<R> UncommitUntilCommitAsync(string id, string wd) => throw new NotSupportedException();

    public Task<R> StashAsync(string message, string wd) => throw new NotSupportedException();

    public Task<R<IReadOnlyList<Stash>>> GetStashesAsync(string wd) => throw new NotSupportedException();

    public Task<R> StashPopAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> StashDropAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd) =>
        throw new NotSupportedException();

    public Task<R> AddTagAsync(string name, string commitId, string wd) => throw new NotSupportedException();

    public Task<R> AddAnnotatedTagAsync(string name, string message, string commitID, string wd) =>
        throw new NotSupportedException();

    public Task<R> RemoveTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> PushTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> DeleteRemoteTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> ResetHardUntilCommitAsync(string id, string wd) => throw new NotSupportedException();
}
