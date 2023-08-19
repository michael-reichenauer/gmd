using System.Reflection;
using gmd.Common.Private;

namespace gmd.Common;


interface IConfigService
{
    ConfigData Get();
    void Set(Action<ConfigData> setState);
    void Register(Config config);
}


// cSpell:ignore gmdconfig
[SingleInstance]
class ConfigService : IConfigService
{
    // Storing registered config instances as week references to avoid needing unregister of dispose
    List<WeakReference> registered = new List<WeakReference>();

    // Get all public properties from Config
    static readonly PropertyInfo[] properties = typeof(Config)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance);


    static readonly string FilePath = Path.Join(Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile), ".gmdconfig");
    private readonly IFileStore store;

    internal ConfigService(IFileStore store) => this.store = store;

    public ConfigData Get() => store.Get<ConfigData>(FilePath);

    public void Register(Config config)
    {
        Copy(Get(), config);
        registered.Add(new WeakReference(config));
    }

    public void Set(Action<ConfigData> set)
    {
        var sourceConfig = store.Set(FilePath, set);

        registered.ForEach(wr =>
        {
            if (wr.Target is not Config targetConfig) return;
            Copy(sourceConfig, targetConfig);
        });

        // Clean reclaimed week references to config values 
        registered = registered.Where(wr => wr.Target != null).ToList();
    }

    static void Copy(ConfigData source, ConfigData target)
    {
        // Copy property values to instance to ensure updated values
        properties.ForEach(fi => fi.SetValue(target, fi.GetValue(source)));
    }
}

