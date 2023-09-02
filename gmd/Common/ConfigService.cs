using System.Reflection;
using gmd.Common.Private;

namespace gmd.Common;


interface IConfigService
{
    Config Get();
    void Set(Action<Config> setState);
}


// cSpell:ignore gmdconfig
[SingleInstance]
class ConfigService : IConfigService
{
    readonly IFileStore store;
    readonly Config config = new Config(); // The single instance of Config used in DI

    // Get all public properties from Config, used when copying properties to single instance
    static readonly PropertyInfo[] properties = typeof(Config)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance);

    static readonly string FilePath = Path.Join(Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile), ".gmdconfig");


    internal ConfigService(IFileStore store, Config config)
    {
        this.store = store;
        this.config = config;

        // Get Config stored from file and updated the single instance Config
        Copy(Get(), this.config);
    }

    public Config Get() => store.Get<Config>(FilePath);


    public void Set(Action<Config> set)
    {
        var storedConfig = store.Set(FilePath, set);

        // Update the single instance Config
        Copy(storedConfig, config);
    }

    // Copy property values to instance to ensure updated values
    static void Copy(Config source, Config target)
    {
        properties.ForEach(fi => fi.SetValue(target, fi.GetValue(source)));
    }
}

