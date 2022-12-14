using System.Text.Json;
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

public class RepoState
{
    public List<string> Branches { get; set; } = new List<string>();
}

interface IStates
{
    State Get();
    void Set(Action<State> setState);

    RepoState GetRepo(string path);
    void SetRepo(string path, Action<RepoState> setState);
}


class States : IStates
{
    static readonly string StateFileName = ".gmdstate.json";
    static string StatePath = Path.Join(Environment.GetFolderPath(
            SpecialFolder.UserProfile), StateFileName);


    public State Get() => Read<State>(StatePath);

    public void Set(Action<State> setState) => SetValue(StatePath, setState);

    public RepoState GetRepo(string path) => Read<RepoState>(RepoPath(path));

    public void SetRepo(string path, Action<RepoState> setState) =>
        SetValue(RepoPath(path), setState);


    string RepoPath(string path) => Path.Join(path, ".git", StateFileName);

    void SetValue<T>(string path, Action<T> setState)
    {
        var state = Read<T>(path);
        setState(state);
        Write(path, state);
    }

    static void Write<T>(string path, T state)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(state, options);
        if (!Try(out var e, Files.WriteAllText(path, json))) Asserter.FailFast(e.ErrorMessage);
    }

    static T Read<T>(string path)
    {
        if (!Files.Exists(path))
        {
            Write(path, new State());
        }

        if (!Try(out var json, out var e, Files.ReadAllText(path))) throw Asserter.FailFast(e.ErrorMessage);
        var state = JsonSerializer.Deserialize<T>(json);
        if (state == null)
        {
            throw Asserter.FailFast("Failed to deserialize state");
        }

        return state;
    }
}
