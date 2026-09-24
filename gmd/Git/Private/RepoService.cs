namespace gmd.Git.Private;

interface IRepoService
{
    Task<Result> InitAsync(string path, bool isBare = false);
}

class RepoService : IRepoService
{
    readonly ICmd cmd;

    public RepoService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result> InitAsync(string path, bool isBare = false)
    {
        string bareText = isBare ? " --bare " : "";

        return await cmd.RunAsync("git", $"init {bareText} \"{path}\"", "");
    }
}
