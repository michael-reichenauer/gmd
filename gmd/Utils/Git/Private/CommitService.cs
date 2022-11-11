namespace gmd.Utils.Git.Private;

interface ICommitService
{
    Task<R> CommitAllChangesAsync(string message);
}

class CommitService : ICommitService
{
    private readonly ICmd cmd;

    public CommitService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> CommitAllChangesAsync(string message)
    {
        // Encode '"' chars
        message = message.Replace("\"", "\\\"");

        if (!IsMergeInProgress())
        {
            if (!Try(out var _, out var e, await cmd.RunAsync("git", "add .")))
            {
                return e;
            }
        }

        return await cmd.RunAsync("git", $"commit -am \"{message}\"");
    }

    bool IsMergeInProgress() => File.Exists(Path.Join(cmd.WorkingDirectory, ".git", "MERGE_MSG"));
}
