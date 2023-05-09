namespace gmd.Common;

public class RepoState
{
    public List<string> Branches { get; set; } = new List<string>();
    public Dictionary<string, int> BranchColors { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, string> BranchOrders { get; set; } = new Dictionary<string, string>();
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

    string RepoPath(string path) => Path.Join(path, ".git", FileName);
}