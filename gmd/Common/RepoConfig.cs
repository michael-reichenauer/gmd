using gmd.Common.Private;
using gmd.Git;

namespace gmd.Common;

class RepoConfig
{
    public bool SyncMetaData { get; set; } = false;

    public List<string> Branches { get; set; } = [];
    public Dictionary<string, int> BranchColors { get; set; } = [];
    public List<BranchOrder> BranchOrders { get; set; } = [];
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

    // The file lives in the *common* git dir, so every worktree of a repository shares one: which
    // branches are shown, their colors and order are properties of the history, not of a checkout,
    // and nothing is lost when a linked worktree is removed. In a linked worktree '<root>/.git' is
    // a file, so joining onto it would try to write inside a file — and a failed write is fatal.
    static string RepoPath(string path)
    {
        var gitDir = Try(out var info, out var _, GitDir.Resolve(path)) ? info.CommonDirPath : Path.Join(path, ".git");
        return Path.Join(gitDir, FileName);
    }
}
