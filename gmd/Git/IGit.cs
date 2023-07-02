namespace gmd.Git;


interface IGit
{
    R<string> RootPath(string path);
    Task<R<string>> Version();

    Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd);
    Task<R<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd);
    Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd);
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<R<Status>> GetStatusAsync(string wd);
    Task<R> CommitAllChangesAsync(string message, bool isAmend, string wd);
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd);
    Task<R<CommitDiff>> GetUncommittedDiff(string wd);
    Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd);
    Task<R<CommitDiff>> GetPreviewMergeDiffAsync(string sha1, string sha2, string message, string wd);
    Task<R> FetchAsync(string wd);
    Task<R> PushBranchAsync(string name, string wd);
    Task<R> PullCurrentBranchAsync(string wd);
    Task<R> PullBranchAsync(string name, string wd);
    Task<R> PushRefForceAsync(string name, string wd);
    Task<R> PullRefAsync(string name, string wd);
    Task<R> CloneAsync(string uri, string path, string wd);
    Task<R> CheckoutAsync(string name, string wd);
    Task<R> MergeBranchAsync(string name, string wd);
    Task<R> CherryPickAsync(string sha, string wd);
    Task<R> CreateBranchAsync(string name, bool isCheckout, string wd);
    Task<R> CreateBranchFromCommitAsync(string name, string sha, bool isCheckout, string wd);
    Task<R> DeleteLocalBranchAsync(string name, bool isForced, string wd);
    Task<R> DeleteRemoteBranchAsync(string name, string wd);
    Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd);
    Task<R> UndoAllUncommittedChangesAsync(string wd);
    Task<R> UndoUncommittedFileAsync(string path, string wd);
    Task<R> CleanWorkingFolderAsync(string wd);
    Task<R> UndoCommitAsync(string id, int parentIndex, string wd);
    Task<R> UncommitLastCommitAsync(string wd);
    Task<R<string>> GetValueAsync(string key, string wd);
    Task<R> SetValueAsync(string key, string value, string wd);
    Task<R> PushValueAsync(string key, string wd);
    Task<R> PullValueAsync(string key, string wd);
    Task<R> StashAsync(string wd);
    Task<R<IReadOnlyList<Stash>>> GetStashesAsync(string wd);
    Task<R> StashPopAsync(string name, string wd);
    Task<R> StashDropAsync(string name, string wd);
    Task<R<CommitDiff>> GetStashDiffAsync(string name, string wd);
    Task<R> AddTagAsync(string name, string commitId, string wd);
    Task<R> RemoveTagAsync(string name, string wd);
    Task<R> PushTagAsync(string name, string wd);
    Task<R> DeleteRemoteTagAsync(string name, string wd);
}


public record Commit(
    string Id,
    string Sid,
    string[] ParentIds,
    string Subject,
    string Message,
    string Author,
    DateTime AuthorTime,
    DateTime CommitTime
);

public record Branch(
    string Name,
    string CommonName,
    string TipID,
    bool IsCurrent,
    bool IsRemote,
    string RemoteName,
    bool IsDetached,
    int AheadCount,
    int BehindCount
);

public record Status(
    int Modified,
    int Added,
    int Deleted,
    int Conflicted,
    bool IsMerging,
    string MergeMessage,
    string MergeHeadId,
    string[] ModifiedFiles,
    string[] AddedFiles,
    string[] DeletedFiles,
    string[] ConflictsFiles
)
{
    public override string ToString() => $"M:{Modified},A:{Added},D:{Deleted},C:{Conflicted}";
}

public record Tag(string Name, string CommitId);

public record Stash(
    string Id,
    string Name,
    string Branch,
    string parentId,
    string indexId,
    string Message
);

record CommitDiff(
    string Id,
    string Author,
    DateTime Time,
    string Message,
    IReadOnlyList<FileDiff> FileDiffs
);

record FileDiff(
    string PathBefore,
    string PathAfter,
    bool IsRenamed,
    bool IsBinary,
    DiffMode DiffMode,
    IReadOnlyList<SectionDiff> SectionDiffs
);

record SectionDiff(
    string ChangedIndexes,
    int LeftLine,
    int LeftCount,
    int RightLine,
    int RightCount,
    IReadOnlyList<LineDiff> LineDiffs
);

record LineDiff(DiffMode DiffMode, string Line);

enum DiffMode
{
    DiffModified,
    DiffAdded,
    DiffRemoved,
    DiffSame,
    DiffConflicts,
    DiffConflictStart,
    DiffConflictSplit,
    DiffConflictEnd,
}