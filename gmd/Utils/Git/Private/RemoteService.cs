namespace gmd.Utils.Git.Private;


interface IRemoteService
{
    Task<R> FetchAsync();
    Task<R> PushBranchAsync(string name);
    Task<R> PullCurrentBranchAsync();
    Task<R> PullBranchAsync(string name);
    Task<R> DeleteRemoteBranchAsync(string name);
    Task<R> PushRefForceAsync(string name);
    Task<R> PullRefAsync(string name);
    Task<R> CloneAsync(string uri, string path);
}

class RemoteService : IRemoteService
{
    private readonly ICmd cmd;

    public RemoteService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> FetchAsync()
    {
        var args = "fetch --force --prune --tags --prune-tags origin";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }

    public async Task<R> PushBranchAsync(string name)
    {
        string refs = $"refs/heads/{name}:refs/heads/{name}";
        var args = $"push --porcelain origin --set-upstream {refs}";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }

    public async Task<R> PullCurrentBranchAsync()
    {
        var args = $"pull --ff --no-rebase";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }

    public async Task<R> PullBranchAsync(string name)
    {
        var refs = $"{name}:{name}";
        var args = $"fetch origin {refs}";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }

    public async Task<R> DeleteRemoteBranchAsync(string name)
    {
        name = name.StartsWith("origin/") ? name.Substring("origin/".Length) : name;

        var args = $"push --porcelain origin --delete {name}";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }


    public async Task<R> PushRefForceAsync(string name)
    {
        string refs = $"{name}:{name}";
        var args = $"push --porcelain origin --set-upstream --force {refs}";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }

    public async Task<R> PullRefAsync(string name)
    {
        string refs = $"{name}:{name}";
        var args = $"fetch origin {refs}";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }


    public async Task<R> CloneAsync(string uri, string path)
    {
        var args = $"clone {uri} {path}";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        return R.Ok;
    }
}
