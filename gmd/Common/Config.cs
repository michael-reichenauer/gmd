using gmd.Cui.Worktrees;

namespace gmd.Common;

[SingleInstance]
class Config
{
    public const int MaxRecentFolders = 15;
    readonly IConfigService? configService;

    // User config values
    public bool CheckUpdates { get; set; } = true;
    public bool AutoUpdate { get; set; } = false;
    public bool AllowPreview { get; set; } = false;
    public bool SpellCheck { get; set; } = true;
    public bool ShowKeyHints { get; set; } = true; // The line at the bottom of the log view
    public List<string> SpellWords { get; set; } = []; // Words added to the dictionary by the user
    public string SpellDictionary { get; set; } = ""; // Optional path to a Hunspell .dic (with its .aff beside it)

    // Values managed by app
    public List<string> RecentFolders { get; set; } = [];
    public string WorktreeLocation { get; set; } = ""; // The last picked place for a new worktree
    public Releases Releases { get; set; } = new Releases();
    public string GitVersion { get; set; } = "";
    public bool IsHiddenBranchesTold { get; set; } = false; // Once, see HiddenBranchesTip

    // Constructor used when deserializing Config, and by tests, for a config that is never stored
    public Config() { }

    // Constructor used by Dependency Injection for the single instance, which holds the stored values
    // from the start: a value read before they were loaded used to be the default, unnoticed
    internal Config(IConfigService configService)
    {
        this.configService = configService;
        configService.Load(this);
    }

    public void Set(Action<Config> set) => configService?.Set(this, set);

    // The folders the recent repos are in, those that still exist, most recent first
    public IReadOnlyList<string> RecentParentFolders() =>
        RecentFolders
            .Select(f => Path.GetDirectoryName(f))
            .OfType<string>()
            .Distinct()
            .Where(Directory.Exists)
            .ToList();
}

public class Release
{
    public string Version { get; set; } = "";
    public bool IsPreview { get; set; } = false;
    public Asset[] Assets { get; set; } = [];
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

    // A dev instance is never offered one, since its version is not a release's
    public bool IsUpdateAvailable() =>
        !Build.IsDevInstance() && Version.TryParse(LatestVersion, out var latest) && latest > Build.Version();

    // The version there is to update to, as gmd writes a version, or "" when there is none
    public string UpdateVersion() =>
        IsUpdateAvailable() && Version.TryParse(LatestVersion, out var latest) ? latest.Txt() : "";
}
