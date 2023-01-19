using static System.Environment;

namespace gmd.Common;


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
    public bool IsUpdateAvailable { get; set; } = false;
    public string LatestVersion { get; set; } = "";
    public bool IsPreview { get; set; } = false;
    public bool AllowPreview { get; set; } = false;
    public Release PreRelease { get; set; } = new Release();
    public Release StableRelease { get; set; } = new Release();
    public string Etag { get; set; } = "";
}

public class State
{
    public List<string> RecentFolders { get; set; } = new List<string>();
    public List<string> RecentParentFolders { get; set; } = new List<string>();

    public Releases Releases { get; set; } = new Releases();
}

interface IState
{
    State Get();
    void Set(Action<State> setState);
}


class StateImpl : IState
{
    static string StatePath = Path.Join(Environment.GetFolderPath(
        SpecialFolder.UserProfile), ".gmdstate.json");
    private readonly IFileStore store;

    internal StateImpl(IFileStore store) => this.store = store;

    public State Get() => store.Get<State>(StatePath);

    public void Set(Action<State> setState) => store.Set(StatePath, setState);
}
