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
    bool isUpdating = false;

    readonly IGit git;

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

        if (!Try(out var e, await git.PullValueAsync(metaDatakey, path)))
        {
            // Could not pull remote value,
            if (IsNoRemoteKey(e))
            {   //  Key does not exist on remote server, lets push local value up
                if (!Try(out var _, await GetMetaDataAsync(path))) return e;
                return await PushMetaDataAsync(path);
            }

            // Failed to fetch remote value, 
            return e;
        }
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


    async Task<R> PushMetaDataAsync(string path)
    {
        await git.PushValueAsync(metaDatakey, path);
        return R.Ok;
    }


    bool IsNoLocalKey(ErrorResult e) => e.ErrorMessage.Contains("Not a valid object name");

    bool IsNoRemoteKey(ErrorResult e) => e.ErrorMessage.Contains("couldn't find remote ref");
}
