namespace gmd.Git.Private;

interface IRemoteService
{
    Task<R> FetchAsync(string wd);
    Task<R> PushBranchAsync(string name, string wd);
    Task<R> PushCurrentBranchAsync(bool isForce, string wd);
    Task<R> PullCurrentBranchAsync(string wd);
    Task<R> PullBranchAsync(string name, string wd);
    Task<R> DeleteRemoteBranchAsync(string name, string wd);
    Task<R> PushRefForceAsync(string name, string wd);
    Task<R> PullRefAsync(string name, string wd);
    Task<R> CloneAsync(string uri, string path, string wd);
    Task<R> PushTagAsync(string name, string wd);
    Task<R> DeleteRemoteTagAsync(string name, string wd);
}

class RemoteService : IRemoteService
{
    private readonly ICmd cmd;
    private readonly ITagService tagService;

    public RemoteService(ICmd cmd, ITagService tagService)
    {
        this.cmd = cmd;
        this.tagService = tagService;
    }

    public static string TrimRemotePrefix(string name) => name.TrimPrefix("origin/");

    // Fetches branches and tags and prunes what the remote no longer has. Deliberately without
    // --prune-tags: that deletes every local tag the remote does not have, which throws away tags
    // that were never pushed — silently, and on merely opening a repo, since this runs then and
    // every five minutes after. The remote's tags are mirrored into gmd's own tracking namespace
    // instead (see TagService.TrackedRemoteTagsRef), which --prune does prune, and the local tags
    // are then pruned from that record so only tags the remote actually deleted are deleted here.
    public async Task<R> FetchAsync(string wd)
    {
        // Read before the fetch, since the fetch is what updates it. An error is not fatal here, it
        // only means no local tag is pruned this time.
        var tracked = (await tagService.GetTrackedRemoteTagsAsync(wd)).Or(new Dictionary<string, string>());

        // The branches have to be named as well: a refspec on the command line replaces the
        // configured remote.origin.fetch ones rather than adding to them, so given the tag mirror
        // alone the fetch updated tags and nothing else, and origin/* never moved. The configured
        // refspecs are passed along rather than assuming +refs/heads/*:refs/remotes/origin/*, so a
        // single-branch clone stays one, and --prune prunes exactly what a plain fetch would.
        var refSpecs = (await GetConfiguredFetchRefSpecsAsync(wd)).Append(TagService.FetchRefSpec);
        var args = $"fetch --force --prune --tags origin {string.Join(' ', refSpecs)}";
        if (!Try(out var _, out var e, await cmd.RunAsync("git", args, wd, true)))
            return e;

        // Only after a fetch that worked: a failed fetch says nothing about what the remote has
        if (!Try(out var pe, await tagService.PruneDeletedRemoteTagsAsync(tracked, wd)))
            Log.Warn($"Failed to prune deleted remote tags, {pe}");

        return R.Ok;
    }

    // The refspecs 'git fetch origin' uses when given none, i.e. remote.origin.fetch, of which there
    // can be several. None when the remote is not configured; the fetch then fails as it always has.
    async Task<IReadOnlyList<string>> GetConfiguredFetchRefSpecsAsync(string wd)
    {
        var args = "config --get-all remote.origin.fetch";
        if (!Try(out var output, await cmd.RunAsync("git", args, wd, true, true)))
            return [];

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<R> PushBranchAsync(string name, string wd)
    {
        name = TrimRemotePrefix(name);
        string refs = $"refs/heads/{name}:refs/heads/{name}";
        var args = $"push --porcelain origin --set-upstream {refs}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> PushCurrentBranchAsync(bool isForce, string wd)
    {
        var force = isForce ? " --force-with-lease" : "";
        var args = $"push{force}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> PullCurrentBranchAsync(string wd)
    {
        var args = $"pull";
        // var args = $"pull --ff --no-rebase";
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
        var args = $"clone {uri} \"{path}\"";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> PushTagAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"push --porcelain origin {name}", wd);
    }

    public async Task<R> DeleteRemoteTagAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"push --porcelain origin --delete {name}", wd);
    }
}
