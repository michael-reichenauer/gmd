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



internal interface IGitRepo
{
    Task<R<IReadOnlyList<Commit>>> GetLog(int maxCount = 30000);
}