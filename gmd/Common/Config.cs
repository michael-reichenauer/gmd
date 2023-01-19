using static System.Environment;

namespace gmd.Common;

class Config { }

interface IConfig
{
    Config Get();
    void Set(Action<Config> setState);
}


class ConfigImpl : IConfig
{
    static string StatePath = Path.Join(Environment.GetFolderPath(
        SpecialFolder.UserProfile), ".gmdconfig.json");
    private readonly IFileStore store;

    internal ConfigImpl(IFileStore store) => this.store = store;

    public Config Get() => store.Get<Config>(StatePath);

    public void Set(Action<Config> setState) => store.Set(StatePath, setState);
}
