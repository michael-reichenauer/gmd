namespace gmd.Common;


[SingleInstance]
class Config
{
    private readonly Lazy<IConfigService>? configService;

    // User config values
    public bool CheckUpdates { get; set; } = true;
    public bool AutoUpdate { get; set; } = false;
    public bool AllowPreview { get; set; } = false;

    // Values managed by app
    public List<string> RecentFolders { get; set; } = new List<string>();
    public List<string> RecentParentFolders { get; set; } = new List<string>();
    public Releases Releases { get; set; } = new Releases();
    public string GitVersion { get; set; } = "";


    // Constructor used when deserializing Config, the values are copied to the single instance
    // by the IConfigService
    public Config()
    {
    }

    // Constructor used by Dependency Injection for this single instance 
    public Config(Lazy<IConfigService> configService) => this.configService = configService;

    public void Set(Action<Config> set) => configService?.Value.Set(set);

    internal void Init() => configService?.Value.Get();
}


public class Release
{
    public string Version { get; set; } = "";
    public bool IsPreview { get; set; } = false;
    public Asset[] Assets { get; set; } = new Asset[0];
}

public class Asset
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public class Releases
{
    public string LatestVersion { get; set; } = "";
    public bool IsPreview { get; set; } = false;
    public Release PreRelease { get; set; } = new Release();
    public Release StableRelease { get; set; } = new Release();
    public string Etag { get; set; } = "";

    public bool IsUpdateAvailable()
    {
        if (Build.IsDevInstance()) return false;

        string v1Text = LatestVersion;
        string v2Text = Build.Version().ToString();

        if (!Version.TryParse(v1Text, out var v1))
        {
            return false;
        }
        if (!Version.TryParse(v2Text, out var v2))
        {
            return true;
        }
        return v1 > v2;
    }
}
