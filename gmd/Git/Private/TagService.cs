namespace gmd.Git.Private;

interface ITagService
{
    Task<Result<IReadOnlyList<Tag>>> GetTagsAsync(string wd);
    Task<Result> AddTagAsync(string name, string commitId, string wd);
    Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, string wd);
    Task<Result> RemoveTagAsync(string name, string wd);
    Task<Result<IReadOnlyDictionary<string, string>>> GetTrackedRemoteTagsAsync(string wd);
    Task<Result> PruneDeletedRemoteTagsAsync(IReadOnlyDictionary<string, string> tracked, string wd);
}

class TagService : ITagService
{
    // gmd's own remote-tracking namespace for tags, which git itself does not provide. A branch has
    // refs/remotes/origin/*, a record of what the remote had at the last fetch, which is how
    // 'fetch --prune' can tell a branch the remote deleted from one that was never pushed. Tags are
    // fetched straight into the shared refs/tags/*, so 'fetch --prune-tags' has only one question it
    // can ask — "is it on the remote right now" — and both cases answer no. It therefore deleted
    // local tags that had simply never been pushed, silently and with no undo.
    //
    // Mirroring the remote's tags in here on every fetch is the missing record: a tag that
    // disappears from the mirror is one the remote deleted, and a tag that was never in it was never
    // on the remote. FetchRefSpec is what RemoteService.FetchAsync passes to fetch it, and 'fetch
    // --prune' prunes this namespace because it is fetched by an explicit refspec (verified against
    // git 2.55; auto-followed tags are not pruned, which is what --prune-tags exists for). It goes on
    // the command line together with the configured branch refspecs, since naming one refspec there
    // switches the configured ones off — on its own it fetched tags and nothing else.
    internal const string TrackedRemoteTagsRef = "refs/gmdtags/origin/";
    internal const string FetchRefSpec = "+refs/tags/*:" + TrackedRemoteTagsRef + "*";

    readonly ICmd cmd;

    internal TagService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    // Every tag and the commit it is on. --tags limits this to refs/tags/, so the tracked remote
    // tags above are not included. Git exits with 1 when there are none, which is an answer.
    public async Task<Result<IReadOnlyList<Tag>>> GetTagsAsync(string wd)
    {
        var result = await cmd.RunRawAsync("git", "show-ref --dereference --tags", wd, skipLogError: true);
        if (result.ExitCode == 1)
            return new List<Tag>();
        if (result.ToResult() is not string output)
        {
            Log.Warn($"Failed to get tags, {result.ErrorOutput}");
            return result.ToResult().Error;
        }

        return ParseTags(output);
    }

    public async Task<Result> AddTagAsync(string name, string commitId, string wd) =>
        await cmd.RunAsync("git", $"tag {name} {commitId}", wd, true);

    public async Task<Result> AddAnnotatedTagAsync(string name, string message, string commitId, string wd) =>
        await cmd.RunAsync("git", $"tag -a {name} {commitId} -m {Cmd.Quote(message)}", wd, true);

    public async Task<Result> RemoveTagAsync(string name, string wd) =>
        await cmd.RunAsync("git", $"tag -d {name}", wd, true);

    // The tags the remote had at the last fetch, as name -> object id. Read before a fetch, since
    // the fetch is what updates it, and compared with what it holds afterwards by the prune below.
    public Task<Result<IReadOnlyDictionary<string, string>>> GetTrackedRemoteTagsAsync(string wd) =>
        GetRefsAsync(TrackedRemoteTagsRef, 3, wd);

    // Deletes the local tags the remote deleted, i.e. those that were in the given snapshot of the
    // tracked remote tags and are no longer there after the fetch. Two cases are deliberately left
    // alone, and they are the ones 'fetch --prune-tags' got wrong:
    //   - a tag that was never in the snapshot, so it was never seen on the remote and is a local
    //     tag that has not been pushed,
    //   - a tag that no longer points where it did when it was seen, so it has been re-tagged
    //     locally and the local ref is no longer the remote's.
    public async Task<Result> PruneDeletedRemoteTagsAsync(IReadOnlyDictionary<string, string> tracked, string wd)
    {
        // Nothing has been seen on the remote yet, so nothing can be known to have been deleted.
        // This is also the first fetch after upgrading, where the namespace is only being filled in,
        // which is what makes turning this on safe: it can never delete a tag it has not observed.
        if (tracked.Count == 0)
            return Result.Ok;

        var remainingResult = await GetTrackedRemoteTagsAsync(wd);
        if (remainingResult is not IReadOnlyDictionary<string, string> remaining)
            return remainingResult.Error;
        var localResult = await GetRefsAsync("refs/tags/", 2, wd);
        if (localResult is not IReadOnlyDictionary<string, string> local)
            return localResult.Error;

        foreach (var (name, id) in tracked)
        {
            if (remaining.ContainsKey(name))
                continue; // Still on the remote
            if (!local.TryGetValue(name, out var localId) || localId != id)
                continue; // Already gone locally, or moved locally since it was seen

            if (await RemoveTagAsync(name, wd) is Error re)
            {
                Log.Warn($"Failed to delete local tag {name}, which was deleted on the remote, {re}");
                continue;
            }

            // Logged with the id, since 'git tag <name> <id>' is how it is put back if this is
            // unwanted. The object survives until it is garbage collected.
            Log.Info($"Deleted local tag {name} ({id}), which was deleted on the remote");
        }

        return Result.Ok;
    }

    // Reads a ref namespace as name -> object id. for-each-ref rather than show-ref, since it can
    // strip the namespace prefix off the name and does not repeat an annotated tag as a second,
    // peeled '^{}' line the way --dereference does. The id is the ref's own value, i.e. the tag
    // object of an annotated tag rather than the commit, which is what both sides of the comparison
    // in the prune above hold.
    async Task<Result<IReadOnlyDictionary<string, string>>> GetRefsAsync(string refPrefix, int stripCount, string wd)
    {
        var args = $"for-each-ref --format=\"%(objectname) %(refname:strip={stripCount})\" {refPrefix}";
        var result = await cmd.RunAsync("git", args, wd, true);
        if (result is not string output)
            return result.Error;

        Dictionary<string, string> refs = [];
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 2);
            if (parts.Length != 2 || !ObjectId.Is(parts[0]))
                continue;

            refs[parts[1]] = parts[0];
        }

        return refs;
    }

    // '<id> refs/tags/<name>' per tag. --dereference adds a line for an annotated tag, whose own id
    // is that of the tag object: '<commit id> refs/tags/<name>^{}', the commit the tag is on, which
    // takes the place of the first, so that each tag is listed once, with the id of its commit.
    static List<Tag> ParseTags(string output)
    {
        const string Prefix = "refs/tags/";
        const string Peeled = "^{}";
        List<Tag> tags = [];
        Dictionary<string, int> indexByName = [];

        foreach (var line in output.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 2);
            if (parts.Length != 2 || !parts[1].StartsWith(Prefix))
                continue;

            var name = parts[1][Prefix.Length..];
            if (name.EndsWith(Peeled) && indexByName.TryGetValue(name[..^Peeled.Length], out var index))
            {
                tags[index] = tags[index] with { CommitId = parts[0] };
                continue;
            }

            indexByName[name] = tags.Count;
            tags.Add(new Tag(name, parts[0]));
        }

        return tags;
    }
}
