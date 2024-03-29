namespace gmd.Git.Private;

interface IStashService
{
    Task<R> StashAsync(string message, string wd);
    Task<R> PopAsync(string name, string wd);
    Task<R> DropAsync(string name, string wd);
    Task<R<IReadOnlyList<Stash>>> ListAsync(string wd);
    Task<R<CommitDiff>> GetDiffAsync(string name, string wd);
}

class StashService : IStashService
{
    readonly ICmd cmd;
    readonly ILogService logService;
    readonly IDiffService diffService;

    public StashService(ICmd cmd, ILogService logService, IDiffService diffService)
    {
        this.cmd = cmd;
        this.logService = logService;
        this.diffService = diffService;
    }

    public async Task<R> StashAsync(string message, string wd)
    {
        var msg = message != "" ? $"save \"{message}\" " : "";
        return await cmd.RunAsync("git", $"stash {msg}-u", wd);
    }

    public async Task<R> PopAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"stash pop {name}", wd);
    }

    public async Task<R> DropAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"stash drop {name}", wd);
    }

    public async Task<R<IReadOnlyList<Stash>>> ListAsync(string wd)
    {
        if (!Try(out var stashes, out var e, await logService.GetStashListAsync(wd))) return e;

        return stashes.Select(ToStash).ToList();
    }

    Stash ToStash(Commit c)
    {
        var id = c.Id;
        var parentId = c.ParentIds[0];
        var indexId = c.ParentIds[1];
        var parts = c.Subject.Split(':');
        var name = parts[0].Trim();
        var message = parts[2].Trim();

        var branch = parts[1].Trim();
        var start = branch.LastIndexOf(' ');
        if (start != -1)
        {
            branch = branch.Substring(start).Trim();
        }

        return new Stash(id, name, branch, parentId, indexId, message);
    }

    public Task<R<CommitDiff>> GetDiffAsync(string name, string wd) =>
        diffService.GetStashDiffAsync(name, wd);
}
