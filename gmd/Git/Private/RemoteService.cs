namespace gmd.Git.Private;


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

    public static string TrimRemotePrefix(string name) => name.TrimPrefix("origin/");

    public async Task<R> FetchAsync()
    {
        var args = "fetch --force --prune --tags --prune-tags origin";
        return await cmd.RunAsync("git", args);
    }

    public async Task<R> PushBranchAsync(string name)
    {
        name = TrimRemotePrefix(name);
        string refs = $"refs/heads/{name}:refs/heads/{name}";
        var args = $"push --porcelain origin --set-upstream {refs}";
        return await cmd.RunAsync("git", args);
    }

    public async Task<R> PullCurrentBranchAsync()
    {
        var args = $"pull --ff --no-rebase";
        return await cmd.RunAsync("git", args);
    }

    public async Task<R> PullBranchAsync(string name)
    {
        name = TrimRemotePrefix(name);
        var refs = $"{name}:{name}";
        var args = $"fetch origin {refs}";
        return await cmd.RunAsync("git", args);
    }

    public async Task<R> DeleteRemoteBranchAsync(string name)
    {
        name = TrimRemotePrefix(name);

        var args = $"push --porcelain origin --delete {name}";
        return await cmd.RunAsync("git", args);
    }


    public async Task<R> PushRefForceAsync(string name)
    {
        name = TrimRemotePrefix(name);
        string refs = $"{name}:{name}";
        var args = $"push --porcelain origin --set-upstream --force {refs}";
        return await cmd.RunAsync("git", args);
    }

    public async Task<R> PullRefAsync(string name)
    {
        name = TrimRemotePrefix(name);
        string refs = $"{name}:{name}";
        var args = $"fetch origin {refs}";
        return await cmd.RunAsync("git", args);
    }


    public async Task<R> CloneAsync(string uri, string path)
    {
        var args = $"clone {uri} {path}";
        return await cmd.RunAsync("git", args);
    }
}
