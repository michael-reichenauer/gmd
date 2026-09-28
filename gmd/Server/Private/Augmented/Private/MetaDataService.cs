using System.Text.Json;
using gmd.Common;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

public class MetaData
{
    //public Dictionary<string, string> CommitBranchBySid { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> CommitBranchBySid { get; set; } = [];

    internal void SetCommitBranch(string sid, string branchName)
    {
        CommitBranchBySid[sid] = "*" + branchName;
    }

    internal void SetBranched(string sid, string branchName)
    {
        CommitBranchBySid[sid] = branchName;
    }

    // What the reflog witnessed about a commit's branch, kept here since the reflog is local and
    // expires (see CommitBranchRules.TryIsWitnessed). Keyed by the full commit id, where a choice is
    // keyed by sid, so an entry never applies to another commit with the same sid, and marked with a
    // '~', so that it is never taken for a choice: a gmd that reads choices by sid only, as versions
    // before these entries do, never looks one up.
    internal void SetWitnessed(string id, string branchName)
    {
        CommitBranchBySid[id] = "~" + branchName;
    }

    internal bool TryGetWitnessedBranch(string id, out string branchName)
    {
        branchName = "";
        if (!CommitBranchBySid.TryGetValue(id, out var name) || !name.StartsWith("~"))
            return false;

        branchName = name[1..];
        return true;
    }

    internal void RemoveCommitBranch(string sid)
    {
        SetCommitBranch(sid, ""); // Mark as removed to support sync
    }

    // Values are branch nice names, so a renamed branch must be renamed here as well. Without this,
    // the stored name would no longer match any branch, and since the branch set here is the first
    // and strongest rule when assigning commits to branches, the old name would be resurrected as a
    // deleted branch of its own (see CommitBranchRules.TryIsBranchSetByUser).
    internal void RenameBranch(string oldNiceName, string newNiceName)
    {
        foreach (var sid in CommitBranchBySid.Keys.ToList())
        {
            var name = CommitBranchBySid[sid];
            if (name == oldNiceName)
            {
                SetBranched(sid, newNiceName);
            }
            else if (name == "*" + oldNiceName)
            { // Branch was set by user, keep it set by user
                SetCommitBranch(sid, newNiceName);
            }
            else if (name == "~" + oldNiceName)
            { // Branch was witnessed by the reflog
                SetWitnessed(sid, newNiceName);
            }
        }
    }

    // Looked up by the commit's sid, which is what entries are written under, and failing that by
    // its full id: creating a branch from a branch wrote the full id for a while, and those entries
    // were never found. The sid comes first, so that a later choice by the user wins over them.
    internal bool TryGetCommitBranch(string id, out string branchName, out bool isSetByUser)
    {
        branchName = "";
        isSetByUser = false;

        if (
            CommitBranchBySid.TryGetValue(id.Sid(), out var name)
            || (CommitBranchBySid.TryGetValue(id, out name) && !name.StartsWith("~"))
        )
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

// The metadata is read, changed and written whole, so every change is made under one lock, each on
// what the one before wrote. Otherwise a change made in the background, e.g. keeping what the reflog
// witnessed, and one the user made meanwhile each wrote a copy of its own, and the later write lost
// the other change. A fetch holds it too, since the pull overwrites the local value it merges into.
[SingleInstance]
class MetaDataService : IMetaDataService
{
    const string metaDataKey = "data";
    readonly IGit git;
    readonly IRepoConfig repoConfig;
    readonly SemaphoreSlim changing = new(1, 1);

    internal MetaDataService(IGit git, IRepoConfig repoConfig)
    {
        this.git = git;
        this.repoConfig = repoConfig;
    }

    public async Task<Result<MetaData>> GetMetaDataAsync(string path)
    {
        var read = await git.GetValueAsync(metaDataKey, path);
        if (read is not string json)
        { // Failed to read local value
            if (IsNoLocalKey(read.Error))
            { // No local key,
                return new MetaData();
            }

            // Failed to get local value
            return read.Error;
        }

        return Result.Catch(() =>
            JsonSerializer.Deserialize<MetaData>(json) ?? throw new JsonException("No metadata in the value")
        );
    }

    public async Task<Result> SetMetaDataAsync(string path, MetaData metaData)
    {
        using (await LockAsync())
        {
            return await WriteMetaDataAsync(path, metaData);
        }
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

    async Task<Result> WriteMetaDataAsync(string path, MetaData metaData)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(metaData, options);

        if (await git.SetValueAsync(metaDataKey, json, path) is Error e)
            return e;
        // Log.Info($"Wrote:\n{json}");
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
        // Lets get current local value so we can merge local and remote values
        var local = await GetMetaDataAsync(path);
        if (local is not MetaData localMetaData)
            return local.Error;

        // Pull latest data from remote server
        if (await git.PullValueAsync(metaDataKey, path) is Error pullError)
        {
            // Could not pull remote value,
            if (IsNoRemoteKey(pullError))
            { // Key does not exist on remote server,
                return Result.Ok;
            }

            // Failed to fetch remote value,
            return pullError;
        }

        // Lets get remote value after remote server pull
        var remote = await GetMetaDataAsync(path);
        if (remote is not MetaData remoteMetaData)
            return remote.Error;

        // Merge previous local and new remote data
        if (await MergeLocalAndRemote(path, localMetaData, remoteMetaData) is Error e)
            return e;

        return Result.Ok;
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

        using (Timing.Start())
        {
            await git.PushValueAsync(metaDataKey, path);
            return Result.Ok;
        }
    }

    async Task<Result> MergeLocalAndRemote(string path, MetaData localMetaData, MetaData remoteMetaData)
    {
        // We will merge before and after values and if different we will then push it

        // Check if metadata count has changed
        bool hasChanged = remoteMetaData.CommitBranchBySid.Count != localMetaData.CommitBranchBySid.Count;

        // Merge data, we prefer remote data. Let iterate all remote data first
        foreach (var pair in remoteMetaData.CommitBranchBySid)
        {
            var key = pair.Key;
            var remoteValue = pair.Value;

            if (!localMetaData.CommitBranchBySid.TryGetValue(key, out var localValue))
            { // The local is missing a value for that key, setting remote value
                localMetaData.CommitBranchBySid[key] = remoteValue;
                localValue = remoteValue;
                hasChanged = true;
            }

            if (remoteValue != localValue)
            { // The remote value has changed (unusual)
                localMetaData.CommitBranchBySid[key] = remoteValue;
                hasChanged = true;
            }
        }

        if (hasChanged)
        { // The local meta data had some new values, or remote was different,
            // We need to set and push the merged collection;
            if (await WriteMetaDataAsync(path, localMetaData) is Error e)
                return e;
        }

        return Result.Ok;
    }

    bool IsNoLocalKey(Error e) => e.Message.Contains("Not a valid object name");

    bool IsNoRemoteKey(Error e) => e.Message.Contains("couldn't find remote ref");
}
