namespace gmd.Git.Private;

interface ITagService
{
    Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd);
    Task<R> AddTagAsync(string name, string commitID, string wd);
    Task<R> AddAnnotatedTagAsync(string name, string message, string commitID, string wd);
    Task<R> RemoveTagAsync(string name, string wd);
    Task<R<IReadOnlyDictionary<string, string>>> GetTrackedRemoteTagsAsync(string wd);
    Task<R> PruneDeletedRemoteTagsAsync(IReadOnlyDictionary<string, string> tracked, string wd);
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

    public async Task<R<IReadOnlyList<Tag>>> GetTagsAsync(string wd)
    {
        // --tags limits this to refs/tags/, so the tracked remote tags above are not included
        var args = "show-ref --dereference --tags";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd, true)))
        {
            if (e.ErrorMessage.StartsWith("\n"))
            { // Empty tag list (no tags yet)
                return new List<Tag>();
            }
            Log.Warn($"Failed to get tags, {e}");
            return e;
        }

        return ParseTags(output);
    }

    public async Task<R> AddTagAsync(string name, string commitID, string wd)
    {
        return await cmd.RunAsync("git", $"tag {name} {commitID}", wd, true);
    }

    public async Task<R> AddAnnotatedTagAsync(string name, string message, string commitID, string wd)
    {
        return await cmd.RunAsync("git", $"tag -a {name} {commitID} -m \"{message}\"", wd, true);
    }

    public async Task<R> RemoveTagAsync(string name, string wd)
    {
        return await cmd.RunAsync("git", $"tag -d {name}", wd, true);
    }

    // The tags the remote had at the last fetch, as name -> object id. Read before a fetch, since
    // the fetch is what updates it, and compared with what it holds afterwards by the prune below.
    public Task<R<IReadOnlyDictionary<string, string>>> GetTrackedRemoteTagsAsync(string wd) =>
        GetRefsAsync(TrackedRemoteTagsRef, 3, wd);

    // Deletes the local tags the remote deleted, i.e. those that were in the given snapshot of the
    // tracked remote tags and are no longer there after the fetch. Two cases are deliberately left
    // alone, and they are the ones 'fetch --prune-tags' got wrong:
    //   - a tag that was never in the snapshot, so it was never seen on the remote and is a local
    //     tag that has not been pushed,
    //   - a tag that no longer points where it did when it was seen, so it has been re-tagged
    //     locally and the local ref is no longer the remote's.
    public async Task<R> PruneDeletedRemoteTagsAsync(IReadOnlyDictionary<string, string> tracked, string wd)
    {
        // Nothing has been seen on the remote yet, so nothing can be known to have been deleted.
        // This is also the first fetch after upgrading, where the namespace is only being filled in,
        // which is what makes turning this on safe: it can never delete a tag it has not observed.
        if (tracked.Count == 0)
            return R.Ok;

        if (!Try(out var remaining, out var e, await GetTrackedRemoteTagsAsync(wd)))
            return e;
        if (!Try(out var local, out var le, await GetRefsAsync("refs/tags/", 2, wd)))
            return le;

        foreach (var (name, id) in tracked)
        {
            if (remaining.ContainsKey(name))
                continue; // Still on the remote
            if (!local.TryGetValue(name, out var localId) || localId != id)
                continue; // Already gone locally, or moved locally since it was seen

            if (!Try(out var re, await RemoveTagAsync(name, wd)))
            {
                Log.Warn($"Failed to delete local tag {name}, which was deleted on the remote, {re}");
                continue;
            }

            // Logged with the id, since 'git tag <name> <id>' is how it is put back if this is
            // unwanted. The object survives until it is garbage collected.
            Log.Info($"Deleted local tag {name} ({id}), which was deleted on the remote");
        }

        return R.Ok;
    }

    // Reads a ref namespace as name -> object id. for-each-ref rather than show-ref, since it can
    // strip the namespace prefix off the name and does not repeat an annotated tag as a second,
    // peeled '^{}' line the way --dereference does. The id is the ref's own value, i.e. the tag
    // object of an annotated tag rather than the commit, which is what both sides of the comparison
    // in the prune above hold.
    async Task<R<IReadOnlyDictionary<string, string>>> GetRefsAsync(string refPrefix, int stripCount, string wd)
    {
        var args = $"for-each-ref --format=\"%(objectname) %(refname:strip={stripCount})\" {refPrefix}";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd, true)))
            return e;

        Dictionary<string, string> refs = [];
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 2);
            if (parts.Length != 2 || parts[0].Length != 40)
                continue;

            refs[parts[1]] = parts[0];
        }

        return refs;
    }

    R<IReadOnlyList<Tag>> ParseTags(string output)
    {
        List<Tag> tags = [];
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
