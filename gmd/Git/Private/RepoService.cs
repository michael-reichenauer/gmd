namespace gmd.Git.Private;


interface IRepoService
{
    Task<R> InitAsync(string path, bool isBare = false);
}


class RepoService : IRepoService
{
    readonly ICmd cmd;

    public RepoService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> InitAsync(string path, bool isBare = false)
    {
        string bareText = isBare ? " --bare " : "";

        return await cmd.RunAsync("git", $"init {bareText} \"{path}\"", "");
    }
}