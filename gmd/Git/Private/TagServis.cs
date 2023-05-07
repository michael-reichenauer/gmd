namespace gmd.Git.Private;

interface ITagService
{
    Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd);
    Task<R> AddTagAsync(string name, string commitID, bool hasRemoteBranch, string wd);
    Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd);
}


class TagService : ITagService
{
    readonly ICmd cmd;
    readonly IRemoteService remoteService;

    internal TagService(ICmd cmd, IRemoteService remoteService)
    {
        this.cmd = cmd;
        this.remoteService = remoteService;
    }

    public async Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd)
    {
        var args = "show-ref --dereference --tags";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd, true)))
        {
            if (e.ErrorMessage.StartsWith("\n"))
            {   // Empty tag list (no tags yet)
                return new List<Tag>();
            }
            Log.Warn($"Failed to get tags, {e}");
            return e;
        }

        return ParseTags(output);
    }

    public async Task<R> AddTagAsync(string name, string commitID, bool hasRemoteBranch, string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", $"tag {name} {commitID}", wd, true))) return e;

        if (!hasRemoteBranch) return R.Ok;

        return await remoteService.PushTagAsync(name, wd);
    }

    public async Task<R> RemoveTagAsync(string name, bool hasRemoteBranch, string wd)
    {
        if (!Try(out var _, out var e, await cmd.RunAsync("git", $"tag -d {name}", wd, true))) return e;

        if (!hasRemoteBranch) return R.Ok;

        return await remoteService.DeleteRemoteTagAsync(name, wd);
    }

    R<IReadOnlyList<Tag>> ParseTags(string output)
    {
        List<Tag> tags = new List<Tag>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            if (line.Length < 51)
            {
                continue;
            }

            var commitID = line.Substring(0, 40);
            var name = line.Substring(51);

            // Seems that some client add a suffix for some reason
            name = name.TrimSuffix("^{}");

            tags.Add(new Tag(name, commitID));
        }

        return tags;
    }
}
