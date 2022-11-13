namespace gmd.Git.Private;


interface IRemoteService
{
    Task<R> FetchAsync(string wd);
    Task<R> PushBranchAsync(string name, string wd);
    Task<R> PullCurrentBranchAsync(string wd);
    Task<R> PullBranchAsync(string name, string wd);
    Task<R> DeleteRemoteBranchAsync(string name, string wd);
    Task<R> PushRefForceAsync(string name, string wd);
    Task<R> PullRefAsync(string name, string wd);
    Task<R> CloneAsync(string uri, string path, string wd);
}

class RemoteService : IRemoteService
{
    private readonly ICmd cmd;

    public RemoteService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public static string TrimRemotePrefix(string name) => name.TrimPrefix("origin/");

    public async Task<R> FetchAsync(string wd)
    {
        var args = "fetch --force --prune --tags --prune-tags origin";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> PushBranchAsync(string name, string wd)
    {
        name = TrimRemotePrefix(name);
        string refs = $"refs/heads/{name}:refs/heads/{name}";
        var args = $"push --porcelain origin --set-upstream {refs}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> PullCurrentBranchAsync(string wd)
    {
        var args = $"pull --ff --no-rebase";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> PullBranchAsync(string name, string wd)
    {
        name = TrimRemotePrefix(name);
        var refs = $"{name}:{name}";
        var args = $"fetch origin {refs}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> DeleteRemoteBranchAsync(string name, string wd)
    {
        name = TrimRemotePrefix(name);

        var args = $"push --porcelain origin --delete {name}";
        return await cmd.RunAsync("git", args, wd);
    }


    public async Task<R> PushRefForceAsync(string name, string wd)
    {
        name = TrimRemotePrefix(name);
        string refs = $"{name}:{name}";
        var args = $"push --porcelain origin --set-upstream --force {refs}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> PullRefAsync(string name, string wd)
    {
        name = TrimRemotePrefix(name);
        string refs = $"{name}:{name}";
        var args = $"fetch origin {refs}";
        return await cmd.RunAsync("git", args, wd);
    }


    public async Task<R> CloneAsync(string uri, string path, string wd)
    {
        var args = $"clone {uri} {path}";
        return await cmd.RunAsync("git", args, wd);
    }
}
