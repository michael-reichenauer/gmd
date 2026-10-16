using gmd.Git;

namespace gmd.Server.Private;

// gmd's own record of what was discarded, so that it can be put back. A discard of uncommitted
// changes throws them away for good as far as git goes, since they were never in its history, which
// is all Undo and Recover Lost Commits have to go on; and a dropped stash is gone from every reflog.
//
// Each record is a commit gmd makes when the discard is done, kept by the reflog of a ref of its own,
// Ref, as git keeps what is lost: about 30 days once it is not the newest, as Recover Lost Commits
// says. It can be looked at with 'git log -g refs/gmd/discarded'.
//   - A discard: the working tree's files after it, whose parent is the files before it, so the
//     difference from the one to the other is what was thrown away, whichever command did it. Its
//     files are the ones the uncommitted diff has, the changed and the new, not those git ignores.
//   - A dropped stash: a commit whose parent is the stash, which is stored back as a stash.
// The kind of a record is in the reflog entry's message, before what it was.
class DiscardedChanges
{
    public const string Ref = "refs/gmd/discarded";
    const string DiscardKind = "Discard: ";
    const string StashKind = "Drop Stash: ";

    readonly IGit git;

    public DiscardedChanges(IGit git)
    {
        this.git = git;
    }

    // Runs the discard with the working tree kept before it, and records what it threw away, if
    // anything, also when it threw some away and then failed. Nothing is discarded when the files
    // cannot be kept first, rather than discarded with no way back.
    public async Task<Result> DiscardAsync(string what, Func<Task<Result>> discard, string wd)
    {
        var beforeResult = await git.WriteWorkingTreeAsync(wd);
        if (beforeResult is not string before)
            return new Error(
                "Nothing was discarded: the changes could not be kept, to restore them from",
                beforeResult.Error
            );

        var discarded = await discard();
        var recorded = await RecordAsync(what, before, wd);
        return (discarded, recorded) switch
        {
            (Error e, _) => e,
            (_, Error e) => new Error($"Discarded {what}, but could not keep it, so it cannot be restored", e),
            _ => Result.Ok,
        };
    }

    async Task<Result> RecordAsync(string what, string beforeTree, string wd)
    {
        var afterResult = await git.WriteWorkingTreeAsync(wd);
        if (afterResult is not string afterTree)
            return afterResult.Error;
        if (afterTree == beforeTree)
            return Result.Ok; // Nothing was thrown away

        var beforeResult = await git.WriteCommitAsync(beforeTree, [], $"Before: {what}", wd);
        if (beforeResult is not string before)
            return beforeResult.Error;
        var recordResult = await git.WriteCommitAsync(afterTree, [before], what, wd);
        if (recordResult is not string record)
            return recordResult.Error;
        return await git.LogRefAsync(Ref, record, $"{DiscardKind}{what}", wd);
    }

    // The stash is recorded first, and dropped only then
    public async Task<Result> DropStashAsync(Git.Stash stash, string wd)
    {
        var recordResult = await git.WriteCommitAsync($"{stash.Id}^{{tree}}", [stash.Id], stash.Message, wd);
        if (recordResult is not string record)
            return new Error("The stash was not dropped: it could not be kept, to restore it from", recordResult.Error);
        if (await git.LogRefAsync(Ref, record, $"{StashKind}{stash.Message}", wd) is Error logError)
            return new Error("The stash was not dropped: it could not be kept, to restore it from", logError);

        return await git.StashDropAsync(stash.Name, wd);
    }

    // The records, newest first
    public async Task<Result<IReadOnlyList<DiscardedChange>>> GetAsync(string wd)
    {
        if (await git.GetRefLogAsync(Ref, wd) is not IReadOnlyList<RefLogRecord> records)
            return new Error("Failed to read what was discarded");

        return records
            .Select(r =>
                r.Message.StartsWith(DiscardKind)
                    ? new DiscardedChange(r.Id, r.Message[DiscardKind.Length..], r.Time, false)
                : r.Message.StartsWith(StashKind)
                    ? new DiscardedChange(r.Id, r.Message[StashKind.Length..], r.Time, true)
                : null
            )
            .OfType<DiscardedChange>()
            .ToList();
    }

    // A discard's changes applied to the working tree again, which git refuses, whole, if its files
    // have changed since; a stash stored back in the list of stashes
    public Task<Result> RestoreAsync(DiscardedChange discarded, string wd) =>
        discarded.IsStash
            ? git.StashStoreAsync($"{discarded.Id}^", discarded.What, wd)
            : git.ApplyDiffAsync(discarded.Id, $"{discarded.Id}^", wd);

    // What Restore puts back: a discard's difference from the files after it to those before it, and
    // a stash's own diff
    public Task<Result<Git.CommitDiff>> GetDiffAsync(DiscardedChange discarded, DiffOptions options, string wd) =>
        discarded.IsStash
            ? git.GetStashDiffAsync($"{discarded.Id}^", options, wd)
            : git.GetPreviewMergeDiffAsync(
                discarded.Id,
                $"{discarded.Id}^",
                $"Discarded: {discarded.What}",
                options,
                wd
            );
}
