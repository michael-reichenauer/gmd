namespace gmd.Utils.Git;


interface IGit
{
    string Path { get; }
    Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount = 30000);
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync();
    Task<R<Status>> GetStatusAsync();
    Task<R> CommitAllChangesAsync(string message);
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId);
    Task<R<CommitDiff>> GetUncommittedDiff();
    Task<R> FetchAsync();
    Task<R> PushBranchAsync(string name);
    Task<R> PullCurrentBranchAsync();
    Task<R> PullBranchAsync(string name);
    Task<R> DeleteRemoteBranchAsync(string name);
    Task<R> PushRefForceAsync(string name);
    Task<R> PullRefAsync(string name);
    Task<R> CloneAsync(string uri, string path);
    Task<R> CheckoutAsync(string name);
    Task<R> MergeBranch(string name);
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
    DiffConflictEnd
}