using gmd.Common;

namespace gmdTest.Fixtures;

// An in-memory IRepoConfig, i.e. the per repo user choices (branch colors and branch order) that
// BranchColorService and ViewRepoCreater read, without touching '<repo>/.git/.gmdconfig'.
class FakeRepoConfig : IRepoConfig
{
    readonly Dictionary<string, RepoConfig> configByPath = [];

    public RepoConfig Get(string rootPath)
    {
        if (!configByPath.TryGetValue(rootPath, out var config))
        {
            config = new RepoConfig();
            configByPath[rootPath] = config;
        }

        return config;
    }

    public void Set(string rootPath, Action<RepoConfig> set) => set(Get(rootPath));
}
