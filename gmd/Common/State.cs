using gmd.Common.Private;
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

        if (!System.Version.TryParse(v1Text, out var v1))
        {
            return false;
        }
        if (!System.Version.TryParse(v2Text, out var v2))
        {
            return true;
        }
        return v1 > v2;
    }
}

public class State
{
    public List<string> RecentFolders { get; set; } = new List<string>();
    public List<string> RecentParentFolders { get; set; } = new List<string>();
    public Releases Releases { get; set; } = new Releases();
    public string GitVersion { get; set; } = "";
}

interface IState
{
    State Get();
    void Set(Action<State> set);
}


class StateImpl : IState
{
    static string FilePath = Path.Join(Environment.GetFolderPath(
        SpecialFolder.UserProfile), ".gmdstate");
    private readonly IFileStore store;

    internal StateImpl(IFileStore store) => this.store = store;

    public State Get() => store.Get<State>(FilePath);

    public void Set(Action<State> set) => store.Set(FilePath, set);
}
