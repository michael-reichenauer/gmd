using System.Reflection;
using gmd.Common.Private;

namespace gmd.Common;

// The user config in '~/.gmdconfig', kept in the single Config instance every class is given, which
// stays the same object while the values in it are replaced by the stored ones, read afresh
interface IConfigService
{
    // Copies the stored values into the config
    void Load(Config config);

    // Changes the stored config with 'set', and copies the result into the config, which thereby
    // also gets what another gmd instance stored since
    void Set(Config config, Action<Config> set);
}

// cSpell:ignore gmdconfig
[SingleInstance]
class ConfigService : IConfigService
{
    readonly IFileStore store;

    // Get all public properties from Config, used when copying properties to single instance
    static readonly PropertyInfo[] properties = typeof(Config).GetProperties(
        BindingFlags.Public | BindingFlags.Instance
    );

    static readonly string FilePath = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gmdconfig"
    );

    internal ConfigService(IFileStore store) => this.store = store;

    public void Load(Config config) => Copy(store.Get<Config>(FilePath), config);

    public void Set(Config config, Action<Config> set) => Copy(store.Set(FilePath, set), config);

    // Copy property values to instance to ensure updated values
    static void Copy(Config source, Config target)
    {
        properties.ForEach(fi => fi.SetValue(target, fi.GetValue(source)));
    }
}
