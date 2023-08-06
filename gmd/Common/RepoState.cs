using gmd.Common.Private;

namespace gmd.Common;

public class BranchOrder
{
    public string Branch { get; set; } = "";
    public string Other { get; set; } = "";
    public int Order { get; set; } = 0;
}

public class RepoState
{
    public List<string> Branches { get; set; } = new List<string>();
    public Dictionary<string, int> BranchColors { get; set; } = new Dictionary<string, int>();
    public List<BranchOrder> BranchOrders { get; set; } = new List<BranchOrder>();
}

interface IRepoState
{
    RepoState Get(string rootPath);
    void Set(string rootPath, Action<RepoState> set);
}

class RepoStateImpl : IRepoState
{
    static readonly string FileName = ".gmdstate";
    private readonly IFileStore store;

    public RepoStateImpl(IFileStore store) => this.store = store;

    public RepoState Get(string path) => store.Get<RepoState>(RepoPath(path));

    public void Set(string path, Action<RepoState> set) => store.Set(RepoPath(path), set);

    static string RepoPath(string path) => Path.Join(path, ".git", FileName);
}