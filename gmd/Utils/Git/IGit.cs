namespace gmd.Utils.Git;

public record Commit(
    string Id,
     string Sid,
     string[] ParentIds,
     string Subject,
     string Message,
     string Author,
     DateTime AuthorTime,
     DateTime CommitTime);

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
    bool IsRemoteMissing);

internal interface IGit
{
    Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount = 30000);
    Task<R<IReadOnlyList<Branch>>> GetBranchesAsync();
}