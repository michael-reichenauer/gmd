namespace gmd.Git.Private;

interface IRepoService
{
    Task<Result> InitAsync(string path);
    bool HasCommitGraph(string wd);
    Task<Result> WriteCommitGraphAsync(string wd);
}

class RepoService : IRepoService
{
    readonly ICmd cmd;

    public RepoService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result> InitAsync(string path) => await cmd.RunAsync("git", $"init {Cmd.Quote(path)}", "");

    // Whether the repository has a commit-graph file, a single one or a chain of them, which lets git
    // walk the commits in date order without first reading every commit of the history. Only looks,
    // in the common dir, where the objects are.
    public bool HasCommitGraph(string wd)
    {
        if (GitDir.Find(wd) is not GitDirInfo gitDir)
            return false;

        var info = Path.Join(gitDir.CommonDirPath, "objects", "info");
        return File.Exists(Path.Join(info, "commit-graph"))
            || File.Exists(Path.Join(info, "commit-graphs", "commit-graph-chain"));
    }

    // Writes the commit-graph file of every commit reachable from a ref, as 'git gc' does
    public async Task<Result> WriteCommitGraphAsync(string wd) =>
        await cmd.RunAsync("git", "commit-graph write --reachable --no-progress", wd);
}
