using gmd.Git;

namespace gmd.Server.Private.Augmented.Private;

public class MetaData
{
    public Dictionary<string, string> CommitBranchBySid { get; set; } = new Dictionary<string, string>();

    internal void SetCommitBranch(string sid, string branchName) => CommitBranchBySid[sid] = "*" + branchName;

    internal void SetBranched(string sid, string branchName) => CommitBranchBySid[sid] = branchName;

    internal void Remove(string sid) => CommitBranchBySid.Remove(sid);

    internal bool TryGet(string sid, out string branchName, out bool isSetByUser)
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

            return true;
        }
        return false;
    }
}

interface IMetaDataService
{
    Task<R> FetchMetaDataAsync(string path);
    Task<R<MetaData>> GetMetaDataAsync(string path);
    Task<R> SetMetaDataAsync(string path, MetaData metaData);
}


[SingleInstance]
class MetaDataService : IMetaDataService
{
    const string metaDatakey = "data";
    readonly IGit git;

    bool isUpdating = false;


    internal MetaDataService(IGit git)
    {
        this.git = git;
    }


    public async Task<R> FetchMetaDataAsync(string path)
    {
        if (isUpdating)
        {
            return R.Ok;
        }

        // Lets get current local value so we can merge local and remote values
        if (!Try(out var localMetaData, out var e, await GetMetaDataAsync(path))) return e;

        // Pull latest data from remote server
        if (!Try(out e, await git.PullValueAsync(metaDatakey, path)))
        {
            // Could not pull remote value,
            if (IsNoRemoteKey(e))
            {   // Key does not exist on remote server, lets push local value up
                if (!Try(out var _, out e, await GetMetaDataAsync(path))) return e;
                return await PushMetaDataAsync(path);
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


    public async Task<R<MetaData>> GetMetaDataAsync(string path)
    {
        if (!Try(out var json, out var e, await git.GetValueAsync(metaDatakey, path)))
        {   // Failed to read local value
            if (IsNoLocalKey(e))
            {   // No local key, probably first time, so set default value for next time
                var metaData = new MetaData();
                if (!Try(out e, await SetMetaDataAsync(path, metaData))) return e;
                return metaData;
            }

            // Failed to get local value
            return e;
        };

        // Log.Info($"Read:\n{json}");
        if (!Try(out var data, out e, Json.Deserilize<MetaData>(json))) return e;

        return data;
    }


    public async Task<R> SetMetaDataAsync(string path, MetaData metaData)
    {
        try
        {
            isUpdating = true;
            string json = Json.SerilizePretty(metaData);
            if (!Try(out var e, await git.SetValueAsync(metaDatakey, json, path))) return e;
            // Log.Info($"Written:\n{json}");
            return await PushMetaDataAsync(path);
        }
        finally
        {
            isUpdating = false;
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



    async Task<R> PushMetaDataAsync(string path)
    {
        await git.PushValueAsync(metaDatakey, path);
        return R.Ok;
    }


    bool IsNoLocalKey(ErrorResult e) => e.ErrorMessage.Contains("Not a valid object name");

    bool IsNoRemoteKey(ErrorResult e) => e.ErrorMessage.Contains("couldn't find remote ref");
}
