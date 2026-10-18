using System.Text.Json;
using System.Text.Json.Serialization;
using gmd.Common;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

public class MetaData
{
    // Which branch a commit is on, by the commit's full id: as the user chose it, '*' and the branch
    // name ('*' alone once taken back, kept so that the removal is shared too), or as gmd recorded it
    // on creating a branch at the commit, the name alone. Named in the json as it was when the key was
    // the commit's short id, which is no longer read, since a short id can be another commit's too: a
    // gmd from before reads these entries still, by the full id it falls back to.
    [JsonPropertyName("CommitBranchBySid")]
    public Dictionary<string, string> CommitBranchById { get; set; } = [];

    // What the reflog witnessed about a commit's branch, by the commit's full id, kept here since the
    // reflog is local and expires (see CommitBranchRules.TryIsWitnessed). A map of its own, so that a
    // fact never takes the place of a choice of the same commit, nor a choice the place of a fact.
    public Dictionary<string, string> WitnessedById { get; set; } = [];

    // The metadata in its json, with the facts the reflog witnessed moved to their own map from among
    // the choices, where they were kept marked with a '~' before they had one
    internal static MetaData Parse(string json)
    {
        var metaData =
            JsonSerializer.Deserialize<MetaData>(json) ?? throw new JsonException("No metadata in the value");
        foreach (var (id, name) in metaData.CommitBranchById.Where(e => e.Value.StartsWith('~')).ToList())
        {
            metaData.WitnessedById.TryAdd(id, name[1..]);
            metaData.CommitBranchById.Remove(id);
        }

        return metaData;
    }

    internal void SetCommitBranch(string id, string branchName)
    {
        CommitBranchById[id] = "*" + branchName;
    }

    internal void SetBranched(string id, string branchName)
    {
        CommitBranchById[id] = branchName;
    }

    internal void SetWitnessed(string id, string branchName)
    {
        WitnessedById[id] = branchName;
    }

    internal bool TryGetWitnessedBranch(string id, out string branchName) =>
        WitnessedById.TryGetValue(id, out branchName!);

    internal void RemoveCommitBranch(string id)
    {
        SetCommitBranch(id, ""); // Mark as removed to support sync
    }

    // Values are branch nice names, so a renamed branch must be renamed here as well. Without this,
    // the stored name would no longer match any branch, and since the branch set here is the first
    // and strongest rule when assigning commits to branches, the old name would be resurrected as a
    // deleted branch of its own (see CommitBranchRules.TryIsBranchSetByUser).
    internal void RenameBranch(string oldNiceName, string newNiceName)
    {
        foreach (var id in CommitBranchById.Keys.ToList())
        {
            var name = CommitBranchById[id];
            if (name == oldNiceName)
            {
                SetBranched(id, newNiceName);
            }
            else if (name == "*" + oldNiceName)
            { // Branch was set by user, keep it set by user
                SetCommitBranch(id, newNiceName);
            }
        }

        foreach (var id in WitnessedById.Where(e => e.Value == oldNiceName).Select(e => e.Key).ToList())
        {
            SetWitnessed(id, newNiceName);
        }
    }

    internal bool TryGetCommitBranch(string id, out string branchName, out bool isSetByUser)
    {
        branchName = "";
        isSetByUser = false;

        if (CommitBranchById.TryGetValue(id, out var name))
        {
            if (name.StartsWith("*"))
            {
                branchName = name.TrimPrefix("*");
                isSetByUser = true;
            }
            else
            {
                branchName = name;
            }

            // The value kan exist, but be empty if user removed the value (marked removed)
            return branchName != "";
        }
        return false;
    }
}

interface IMetaDataService
{
    Task<Result<MetaData>> GetMetaDataAsync(string path);
    Task<Result> UpdateMetaDataAsync(string path, Action<MetaData> update);
    Task<Result> FetchMetaDataAsync(string path);
    Task<Result> PushMetaDataAsync(string path);
    Task<Result> AddWitnessedAsync(string path, IReadOnlyList<WitnessedBranch> witnessed);
}

// The metadata is kept in a file of its own in the git dir (MetaDataFile), on this machine alone, and
// shared through origin only when the shared branch structure is on (RepoConfig.SyncMetaData), as a
// git value (IGit.PushValueAsync). A gmd before kept it in the repository, under the ref it is shared
// through, which is read until the file is first written, and deleted then.
//
// The metadata is read, changed and written whole, so every change is made under one lock, each on
// what the one before wrote. Otherwise a change made in the background, e.g. keeping what the reflog
// witnessed, and one the user made meanwhile each wrote a copy of its own, and the later write lost
// the other change. A fetch holds it too, since the merge with origin's is written as the local value.
//
// Origin's value is fetched into a record of its own (IGit.FetchValueAsync), beside the local value,
// and the two are merged three ways against the record from before the fetch, see Merge.
[SingleInstance]
class MetaDataService : IMetaDataService
{
    const string metaDataKey = "data";
    static readonly JsonSerializerOptions options = new() { WriteIndented = true };
    readonly IGit git;
    readonly IRepoConfigService repoConfig;
    readonly IMetaDataFile file;
    readonly SemaphoreSlim changing = new(1, 1);

    internal MetaDataService(IGit git, IRepoConfigService repoConfig, IMetaDataFile file)
    {
        this.git = git;
        this.repoConfig = repoConfig;
        this.file = file;
    }

    public async Task<Result<MetaData>> GetMetaDataAsync(string path)
    {
        if (!file.Exists(path))
            return await GetUnmovedMetaDataAsync(path);

        var read = await file.ReadAsync(path);
        if (read is not string json)
            return read.Error;
        return Result.Catch(() => MetaData.Parse(json));
    }

    // The metadata where a gmd before kept it, the repository's own git value, or none: the normal
    // state of a repo nobody has made a choice in, and of one whose reflog decided nothing. None too
    // when it was moved into the file just now, after the file was looked for, which is then read.
    async Task<Result<MetaData>> GetUnmovedMetaDataAsync(string path)
    {
        var read = await git.GetValueAsync(metaDataKey, path);
        if (read is not string json)
        {
            if (!IsNoLocalKey(read.Error))
                return read.Error;
            return file.Exists(path) ? await GetMetaDataAsync(path) : new MetaData();
        }

        return Result.Catch(() => MetaData.Parse(json));
    }

    // Changes the latest metadata and writes it
    public async Task<Result> UpdateMetaDataAsync(string path, Action<MetaData> update)
    {
        using (await LockAsync())
        {
            var read = await GetMetaDataAsync(path);
            if (read is not MetaData metaData)
                return read.Error;

            update(metaData);
            return await WriteMetaDataAsync(path, metaData);
        }
    }

    async Task<IDisposable> LockAsync()
    {
        await changing.WaitAsync();
        return new Disposable(() => changing.Release());
    }

    // The first write moves the metadata into the file, and deletes the git value it was read from
    // until then, so that nothing of it is left in the repository
    async Task<Result> WriteMetaDataAsync(string path, MetaData metaData)
    {
        var isMoving = !file.Exists(path);
        if (await file.WriteAsync(path, JsonSerializer.Serialize(metaData, options)) is Error e)
            return e;

        if (isMoving && await git.DeleteValueAsync(metaDataKey, path) is Error deleteError)
        { // The file is what is read from now on, so the value is merely left behind
            Log.Warn($"Failed to delete the metadata's git value, kept in a file now: {deleteError}");
        }
        return Result.Ok;
    }

    public async Task<Result> FetchMetaDataAsync(string path)
    {
        if (!repoConfig.Get(path).SyncMetaData)
        {
            Log.Debug("Repo fetch sync disabled");
            return Result.Ok;
        }

        using (await LockAsync())
        {
            return await FetchAndMergeMetaDataAsync(path);
        }
    }

    async Task<Result> FetchAndMergeMetaDataAsync(string path)
    {
        var local = await GetMetaDataAsync(path);
        if (local is not MetaData localMetaData)
            return local.Error;

        // Origin's as last fetched or pushed, which says what changed on which side since. None before
        // the first fetch, or one by a gmd that fetched over the local value instead.
        var lastOrigin = await GetOriginMetaDataAsync(path) is MetaData last ? last : null;

        // Never asking for a login, since it runs beside the fetch or push the user asked for, which
        // asks: one question per command, and it is best effort.
        Result fetched;
        using (Askpass.NeverAsk())
        {
            fetched = await git.FetchValueAsync(metaDataKey, path);
        }
        if (fetched is Error fetchError)
        {
            // Nobody pushed any metadata yet, which is no failure
            return IsNoRemoteKey(fetchError) ? Result.Ok : fetchError;
        }

        var origin = await GetOriginMetaDataAsync(path);
        if (origin is not MetaData originMetaData)
            return origin.Error;

        if (!Merge(localMetaData, lastOrigin, originMetaData))
            return Result.Ok;
        return await WriteMetaDataAsync(path, localMetaData);
    }

    async Task<Result<MetaData>> GetOriginMetaDataAsync(string path)
    {
        var read = await git.GetOriginValueAsync(metaDataKey, path);
        if (read is not string json)
            return read.Error;

        return Result.Catch(() => MetaData.Parse(json));
    }

    // Merges origin's values into the local ones, and says whether any changed. A value changed here
    // since the last fetch or push, and not on origin, is kept; any other that differs takes origin's,
    // so that every clone ends up the same: one changed on origin only, one changed on both sides, and
    // any at all when there is no record of origin's from before. Without that record, every value
    // that differed took origin's, so a change made here to a value origin had, e.g. a renamed branch
    // or an ambiguity resolved again, was undone by the next fetch, every five minutes, until pushed.
    internal static bool Merge(MetaData local, MetaData? lastOrigin, MetaData origin)
    {
        // Both, each map with its own, so no short circuit
        var isChoicesChanged = Merge(local.CommitBranchById, lastOrigin?.CommitBranchById, origin.CommitBranchById);
        var isWitnessedChanged = Merge(local.WitnessedById, lastOrigin?.WitnessedById, origin.WitnessedById);
        return isChoicesChanged || isWitnessedChanged;
    }

    static bool Merge(
        Dictionary<string, string> local,
        Dictionary<string, string>? lastOrigin,
        Dictionary<string, string> origin
    )
    {
        var isChanged = false;
        foreach (var (key, originValue) in origin)
        {
            if (local.TryGetValue(key, out var localValue) && localValue == originValue)
                continue;

            var isChangedHereOnly =
                localValue != null
                && lastOrigin != null
                && lastOrigin.TryGetValue(key, out var lastValue)
                && lastValue == originValue;
            if (isChangedHereOnly)
                continue;

            local[key] = originValue;
            isChanged = true;
        }

        return isChanged;
    }

    // Keeps what the reflog witnessed, the facts that are not kept already. Written only when there
    // is something new, so a repo whose facts are all kept is not written to on every read.
    public async Task<Result> AddWitnessedAsync(string path, IReadOnlyList<WitnessedBranch> witnessed)
    {
        using (await LockAsync())
        {
            var read = await GetMetaDataAsync(path);
            if (read is not MetaData metaData)
                return read.Error;

            var added = witnessed
                .Where(w => !metaData.TryGetWitnessedBranch(w.Id, out var name) || name != w.BranchName)
                .ToList();
            if (added.Count == 0)
                return Result.Ok;

            added.ForEach(w => metaData.SetWitnessed(w.Id, w.BranchName));
            Log.Info($"Keeping {added.Count} branches witnessed by the reflog");
            return await WriteMetaDataAsync(path, metaData);
        }
    }

    public async Task<Result> PushMetaDataAsync(string path)
    {
        if (!repoConfig.Get(path).SyncMetaData)
        {
            Log.Debug("Repo push sync disabled");
            return Result.Ok;
        }

        // Never asking for a login, as the fetch above, and best effort, as it is: a push that fails is
        // left to the next one. Refused, for another clone having pushed since the last fetch here,
        // origin's is fetched and merged first, and the merge pushed, rather than pushed over origin's,
        // which lost the choices made there.
        using (Timing.Start())
        using (Askpass.NeverAsk())
        {
            for (var tries = 0; tries < MaxPushes; tries++)
            {
                if (await GetMetaDataAsync(path) is not MetaData metaData)
                    return Result.Ok;
                var json = JsonSerializer.Serialize(metaData, options);
                if (await git.PushValueAsync(metaDataKey, json, path) is not false)
                    return Result.Ok;

                using (await LockAsync())
                {
                    if (await FetchAndMergeMetaDataAsync(path) is Error)
                        return Result.Ok;
                }
            }

            return Result.Ok;
        }
    }

    // Another clone can push again between a fetch here and the push after it, so a few goes
    const int MaxPushes = 3;

    bool IsNoLocalKey(Error e) => e.Message.Contains("Not a valid object name");

    bool IsNoRemoteKey(Error e) => e.Message.Contains("couldn't find remote ref");
}
