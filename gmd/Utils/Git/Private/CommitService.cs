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
            CmdResult addResult = await cmd.RunAsync("git", "add .");
            if (addResult.ExitCode != 0)
            {
                return R.Error(addResult.Error);
            }
        }

        CmdResult commitResult = await cmd.RunAsync("git", $"commit -am \"{message}\"");
        if (commitResult.ExitCode != 0)
        {
            return R.Error(commitResult.Error);
        }

        return R.Ok;
    }

    bool IsMergeInProgress()
    {
        string mergeMsgPath = Path.Join(cmd.WorkingDirectory, ".git", "MERGE_MSG");
        return File.Exists(mergeMsgPath);
    }
}
