namespace gmd.Git;


interface IGit
{
    R<string> RootPath(string path);

    Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd);
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync(string wd);
    Task<R<Status>> GetStatusAsync(string wd);
    Task<R> CommitAllChangesAsync(string message, string wd);
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd);
    Task<R<CommitDiff>> GetUncommittedDiff(string wd);
    Task<R> FetchAsync(string wd);
    Task<R> PushBranchAsync(string name, string wd);
    Task<R> PullCurrentBranchAsync(string wd);
    Task<R> PullBranchAsync(string name, string wd);
    Task<R> DeleteRemoteBranchAsync(string name, string wd);
    Task<R> PushRefForceAsync(string name, string wd);
    Task<R> PullRefAsync(string name, string wd);
    Task<R> CloneAsync(string uri, string path, string wd);
    Task<R> CheckoutAsync(string name, string wd);
    Task<R> MergeBranch(string name, string wd);
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
    int BehindCount,
    bool IsRemoteMissing
);

public record Status(
    int Modified,
    int Added,
    int Deleted,
    int Conflicted,
    bool IsMerging,
    string MergeMessage,
    string[] AddedFiles,
    string[] ConflictsFiles
)
{
    public override string ToString() => $"M:{Modified},A:{Added},D:{Deleted},C:{Conflicted}";
}

record CommitDiff(
    string Id,
    string Author,
    string Date,
    string Message,
    IReadOnlyList<FileDiff> FileDiffs
);

record FileDiff(
    string PathBefore,
    string PathAfter,
    bool IsRenamed,
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