namespace gmd.Utils.Git;


interface IGit
{
    Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount = 30000);
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync();
    Task<R<Status>> GetStatusAsync();
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
    string DisplayName,
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

