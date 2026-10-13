namespace gmd.Git.Private;

interface IRepoService
{
    Task<Result> InitAsync(string path);
}

class RepoService : IRepoService
{
    readonly ICmd cmd;

    public RepoService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result> InitAsync(string path) => await cmd.RunAsync("git", $"init {Cmd.Quote(path)}", "");
}
