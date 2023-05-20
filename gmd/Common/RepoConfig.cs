using gmd.Common.Private;

namespace gmd.Common;

class RepoConfig
{
    public bool SyncMetaData { get; set; } = false;
}

interface IRepoConfig
{
    RepoConfig Get(string rootPath);
    void Set(string rootPath, Action<RepoConfig> set);
}

class RepoConfigImpl : IRepoConfig
{
    static readonly string FileName = ".gmdconfig";
    private readonly IFileStore store;

    public RepoConfigImpl(IFileStore store) => this.store = store;

    public RepoConfig Get(string path) => store.Get<RepoConfig>(RepoPath(path));

    public void Set(string path, Action<RepoConfig> set) => store.Set(RepoPath(path), set);

    string RepoPath(string path) => Path.Join(path, ".git", FileName);
}