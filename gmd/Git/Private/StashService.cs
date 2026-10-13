namespace gmd.Git.Private;

interface IStashService
{
    Task<Result> StashAsync(string message, string wd);
    Task<Result> PopAsync(string name, string wd);
    Task<Result> ApplyAsync(string name, string wd);
    Task<Result> DropAsync(string name, string wd);
    Task<Result<IReadOnlyList<Stash>>> ListAsync(string wd);
    Task<Result<CommitDiff>> GetDiffAsync(string name, int contextLines, string wd);
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

    public async Task<Result> StashAsync(string message, string wd)
    {
        var msg = message != "" ? $"save {Cmd.Quote(message)} " : "";
        return await cmd.RunAsync("git", $"stash {msg}-u", wd);
    }

    public async Task<Result> PopAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"stash pop {name}", wd);
    }

    // As pop, but keeps the stash, e.g. to put the same changes on another branch too
    public async Task<Result> ApplyAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"stash apply {name}", wd);
    }

    public async Task<Result> DropAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"stash drop {name}", wd);
    }

    public async Task<Result<IReadOnlyList<Stash>>> ListAsync(string wd)
    {
        var listed = await logService.GetStashListAsync(wd);
        if (listed is not IReadOnlyList<Commit> stashes)
            return listed.Error;

        return stashes.Select(ToStash).ToList();
    }

    // The subject is '<stash ref>:<git's message>' (LogService.GetStashListAsync), and git's message
    // is 'WIP on <branch>: <sha> <subject>' or 'On <branch>: <the message given>'. Split at the first
    // two colons only, since a branch name cannot have one but a message can ('fix: the thing'),
    // and was cut short at it. One with fewer, as another tool may write it, is kept whole rather
    // than failing the list.
    static Stash ToStash(Commit c)
    {
        var parts = c.Subject.Split(':', 3);
        var name = parts[0].Trim();
        var (on, message) = parts.Length switch
        {
            3 => (parts[1].Trim(), parts[2].Trim()),
            2 => ("", parts[1].Trim()),
            _ => ("", ""),
        };
        var branch = on[(on.LastIndexOf(' ') + 1)..];

        return new Stash(c.Id, name, branch, c.ParentIds[0], c.ParentIds.ElementAtOrDefault(1) ?? "", message);
    }

    public Task<Result<CommitDiff>> GetDiffAsync(string name, int contextLines, string wd) =>
        diffService.GetStashDiffAsync(name, contextLines, wd);
}
