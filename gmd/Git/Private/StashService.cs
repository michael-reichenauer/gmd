namespace gmd.Git.Private;

interface IStashService
{
    Task<R> Stash(string wd);
    Task<R<IReadOnlyList<Stash>>> GetStashes(string wd);
}

class StashService : IStashService
{
    readonly ICmd cmd;
    readonly ILogService logService;

    public StashService(ICmd cmd, ILogService logService)
    {
        this.cmd = cmd;
        this.logService = logService;
    }

    public async Task<R> Stash(string wd)
    {
        return await cmd.RunAsync("git", $"stash -u", wd);
    }


    public async Task<R<IReadOnlyList<Stash>>> GetStashes(string wd)
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
}
