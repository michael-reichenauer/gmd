using static System.Environment;

namespace gmd.Common;

class Config
{
    public bool AllowPreview { get; set; } = false;
}

interface IConfig
{
    Config Get();
    void Set(Action<Config> setState);
}


class ConfigImpl : IConfig
{
    static string FilePath = Path.Join(Environment.GetFolderPath(
        SpecialFolder.UserProfile), ".gmdconfig");
    private readonly IFileStore store;

    internal ConfigImpl(IFileStore store) => this.store = store;

    public Config Get() => store.Get<Config>(FilePath);

    public void Set(Action<Config> set) => store.Set(FilePath, set);
}
