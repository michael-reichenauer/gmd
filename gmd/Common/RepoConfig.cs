using gmd.Common.Private;

namespace gmd.Common;

class RepoConfig
{
    public bool SyncMetaData { get; set; } = false;

    public List<string> Branches { get; set; } = new List<string>();
    public Dictionary<string, int> BranchColors { get; set; } = new Dictionary<string, int>();
    public List<BranchOrder> BranchOrders { get; set; } = new List<BranchOrder>();
}


public class BranchOrder
{
    public string Branch { get; set; } = "";
    public string Other { get; set; } = "";
    public int Order { get; set; } = 0;
}




interface IRepoConfig
{
    RepoConfig Get(string rootPath);
    void Set(string rootPath, Action<RepoConfig> set);
}

// cSpell:ignore gmdconfig
class RepoConfigImpl : IRepoConfig
{
    static readonly string FileName = ".gmdconfig";
    private readonly IFileStore store;

    public RepoConfigImpl(IFileStore store) => this.store = store;

    public RepoConfig Get(string path) => store.Get<RepoConfig>(RepoPath(path));

    public void Set(string path, Action<RepoConfig> set) => store.Set(RepoPath(path), set);

    static string RepoPath(string path) => Path.Join(path, ".git", FileName);
}