using gmd.Git;

namespace gmdTest.Fixtures;

// A double for IGit, the git area services the server layer calls. Only the members the pipeline
// needs are implemented; every other member throws, so a test that starts depending on git fails
// loudly rather than silently working on an empty result.
class FakeGit : IGit
{
    readonly Status status;

    public FakeGit(Status status) => this.status = status;

    public string CurrentAuthor => "Test Author";

    public Task<R<Status>> GetStatusAsync(string wd) => Task.FromResult<R<Status>>(status);

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

    public Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff>> GetUncommittedDiff(string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff>> GetPreviewMergeDiffAsync(string sha1, string sha2, string message, string wd) =>
        throw new NotSupportedException();

    public Task<R<CommitDiff>> GetDiffRangeAsync(string sha1, string sha2, string message, string wd) =>
        throw new NotSupportedException();

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

    public Task<R> CreateBranchAsync(string name, bool isCheckout, string wd) => throw new NotSupportedException();

    public Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd) =>
        throw new NotSupportedException();

    public Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd) => throw new NotSupportedException();

    public Task<R> DeleteRemoteBranchAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd) => throw new NotSupportedException();

    public Task<R> UndoAllUncommittedChangesAsync(string wd) => throw new NotSupportedException();

    public Task<R> UndoUncommittedFileAsync(string path, string wd) => throw new NotSupportedException();

    public Task<R> CleanWorkingFolderAsync(string wd) => throw new NotSupportedException();

    public Task<R> UndoCommitAsync(string id, int parentIndex, string wd) => throw new NotSupportedException();

    public Task<R> UncommitLastCommitAsync(string wd) => throw new NotSupportedException();

    public Task<R> UncommitUntilCommitAsync(string id, string wd) => throw new NotSupportedException();

    public Task<R<string>> GetValueAsync(string key, string wd) => throw new NotSupportedException();

    public Task<R> SetValueAsync(string key, string value, string wd) => throw new NotSupportedException();

    public Task<R> PushValueAsync(string key, string wd) => throw new NotSupportedException();

    public Task<R> PullValueAsync(string key, string wd) => throw new NotSupportedException();

    public Task<R> StashAsync(string message, string wd) => throw new NotSupportedException();

    public Task<R<IReadOnlyList<Stash>>> GetStashesAsync(string wd) => throw new NotSupportedException();

    public Task<R> StashPopAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> StashDropAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R<CommitDiff>> GetStashDiffAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> AddTagAsync(string name, string commitId, string wd) => throw new NotSupportedException();

    public Task<R> AddAnnotatedTagAsync(string name, string message, string commitID, string wd) =>
        throw new NotSupportedException();

    public Task<R> RemoveTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> PushTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> DeleteRemoteTagAsync(string name, string wd) => throw new NotSupportedException();

    public Task<R> ResetHardUntilCommitAsync(string id, string wd) => throw new NotSupportedException();
}
