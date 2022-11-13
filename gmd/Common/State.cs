using System.Text.Json;
using static System.Environment;

namespace gmd.Common;

public class State
{
    public List<string> RecentFolders { get; set; } = new List<string>();
    public List<string> RecentParentFolders { get; set; } = new List<string>();
}

public class RepoState
{
    public List<string> Branches { get; set; } = new List<string>();
}

interface IState
{
    State Get();
    void Set(Action<State> setState);

    RepoState GetRepo(string path);
    void SetRepo(string path, Action<RepoState> setState);
}


class StateService : IState
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
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(state, options);
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            throw (Asserter.FailFast($"Error diffMode {e}"));
        }
    }

    static T Read<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Write(path, new State());
            }

            string json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<T>(json);
            if (state == null)
            {
                throw new Exception("Failed to deserialize state");
            }

            return state;
        }
        catch (Exception e)
        {
            throw (Asserter.FailFast($"Error diffMode {e}"));
        }
    }
}
