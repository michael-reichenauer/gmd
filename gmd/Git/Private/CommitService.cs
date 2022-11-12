namespace gmd.Git.Private;

interface ICommitService
{
    Task<R> CommitAllChangesAsync(string message, string wd);
}

class CommitService : ICommitService
{
    private readonly ICmd cmd;

    public CommitService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> CommitAllChangesAsync(string message, string wd)
    {
        // Encode '"' chars
        message = message.Replace("\"", "\\\"");

        if (!StatusService.IsMergeInProgress(wd))
        {
            if (!Try(out var _, out var e, await cmd.RunAsync("git", "add .", wd)))
            {
                return e;
            }
        }

        return await cmd.RunAsync("git", $"commit -am \"{message}\"", wd);
    }


}
