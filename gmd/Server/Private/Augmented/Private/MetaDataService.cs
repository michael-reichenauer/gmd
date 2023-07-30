using System.Text.Json;
using gmd.Common;
using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

public class MetaData
{
    //public Dictionary<string, string> CommitBranchBySid { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> CommitBranchBySid { get; set; } = new Dictionary<string, string>();


    internal void SetCommitBranch(string sid, string branchName)
    {
        CommitBranchBySid[sid] = "*" + branchName;
    }

    internal void SetBranched(string sid, string branchName)
    {
        CommitBranchBySid[sid] = branchName;
    }

    internal void RemoveCommitBranch(string sid)
    {
        SetCommitBranch(sid, "");  // Mark as removed to support sync
    }

    internal bool TryGetCommitBranch(string sid, out string branchName, out bool isSetByUser)
    {
        branchName = "";
        isSetByUser = false;

        if (CommitBranchBySid.TryGetValue(sid, out var name))
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
    Task<R<MetaData>> GetMetaDataAsync(string path);
    Task<R> SetMetaDataAsync(string path, MetaData metaData);
    Task<R> FetchMetaDataAsync(string path);
    Task<R> PushMetaDataAsync(string path);
}


[SingleInstance]
class MetaDataService : IMetaDataService
{
    const string metaDataKey = "data";
    readonly IGit git;
    readonly IRepoConfig repoConfig;
    bool isUpdating = false;


    internal MetaDataService(IGit git, IRepoConfig repoConfig)
    {
        this.git = git;
        this.repoConfig = repoConfig;
    }

    public async Task<R<MetaData>> GetMetaDataAsync(string path)
    {
        if (!Try(out var json, out var e, await git.GetValueAsync(metaDataKey, path)))
        {   // Failed to read local value
            if (IsNoLocalKey(e))
            {   // No local key,
                return new MetaData();
            }

            // Failed to get local value
            return e;
        };

        //Log.Info($"Metadata:\n{json}");
        if (!Try(out var data, out e, () => JsonSerializer.Deserialize<MetaData>(json))) return e;
        //Log.Info($"Read {data.CommitBranchBySid.Count()} meta data items");
        return data;
    }

    public async Task<R> SetMetaDataAsync(string path, MetaData metaData)
    {
        try
        {
            isUpdating = true;
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(metaData, options);

            if (!Try(out var e, await git.SetValueAsync(metaDataKey, json, path))) return e;
            // Log.Info($"Wrote:\n{json}");
            return R.Ok;
        }
        finally
        {
            isUpdating = false;
        }
    }


    public async Task<R> FetchMetaDataAsync(string path)
    {
        if (!repoConfig.Get(path).SyncMetaData)
        {
            Log.Debug("Repo fetch sync disabled");
            return R.Ok;
        }

        if (isUpdating)
        {
            return R.Ok;
        }

        // Lets get current local value so we can merge local and remote values
        if (!Try(out var localMetaData, out var e, await GetMetaDataAsync(path))) return e;

        // Pull latest data from remote server
        if (!Try(out e, await git.PullValueAsync(metaDataKey, path)))
        {
            // Could not pull remote value,
            if (IsNoRemoteKey(e))
            {   // Key does not exist on remote server,
                return R.Ok;
            }

            // Failed to fetch remote value, 
            return e;
        }

        // Lets get remote value after remote server pull
        if (!Try(out var remoteMetaData, out e, await GetMetaDataAsync(path))) return e;

        // Merge previous local and new remote data
        if (!Try(out e, await MergeLocalAndRemote(path, localMetaData, remoteMetaData))) return e;

        return R.Ok;
    }


    public async Task<R> PushMetaDataAsync(string path)
    {
        if (!repoConfig.Get(path).SyncMetaData)
        {
            Log.Debug("Repo push sync disabled");
            return R.Ok;
        }

        using (Timing.Start())
        {
            await git.PushValueAsync(metaDataKey, path);
            return R.Ok;
        }
    }


    async Task<R> MergeLocalAndRemote(string path,
          MetaData localMetaData, MetaData remoteMetaData)
    {
        // We will merge before and after values and if different we will then push it

        // Check if metadata count has changed
        bool hasChanged = remoteMetaData.CommitBranchBySid.Count
            != localMetaData.CommitBranchBySid.Count;

        // Merge data, we prefer remote data. Let iterate all remote data first
        foreach (var pair in remoteMetaData.CommitBranchBySid)
        {
            var key = pair.Key;
            var remoteValue = pair.Value;

            if (!localMetaData.CommitBranchBySid.TryGetValue(key, out var localValue))
            {  // The local is missing a value for that key, setting remote value
                localMetaData.CommitBranchBySid[key] = remoteValue;
                localValue = remoteValue;
                hasChanged = true;
            }

            if (remoteValue != localValue)
            {   // The remote value has changed (unusual)
                localMetaData.CommitBranchBySid[key] = remoteValue;
                hasChanged = true;
            }
        }

        if (hasChanged)
        {   // The local meta data had some new values, or remote was different,
            // We need to set and push the merged collection;
            if (!Try(out var e, await SetMetaDataAsync(path, localMetaData))) return e;
        }

        return R.Ok;
    }


    bool IsNoLocalKey(ErrorResult e) => e.ErrorMessage.Contains("Not a valid object name");

    bool IsNoRemoteKey(ErrorResult e) => e.ErrorMessage.Contains("couldn't find remote ref");
}
