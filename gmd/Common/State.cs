using System.Text.Json;
using static System.Environment;

namespace gmd.Common;

public class State
{
    public List<string> RecentFolders { get; set; } = new List<string>();
    public List<string> RecentParentFolders { get; set; } = new List<string>();
}

interface IState
{
    State Get();

    void Set(Action<State> setState);
}


class StateService : IState
{
    static readonly string StateFileName = ".gmdstate.json";
    static string StatePath = StateFileName;

    static StateService()
    {
        StatePath = Path.Join(Environment.GetFolderPath(
            SpecialFolder.UserProfile), StateFileName);

        if (!File.Exists(StatePath))
        {
            Write(new State());
        }
    }

    public State Get() => Read();

    public void Set(Action<State> setState)
    {
        var state = Read();
        setState(state);
        Write(state);
    }


    private static void Write(State state)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(state, options);
            File.WriteAllText(StatePath, json);
        }
        catch (Exception e)
        {
            throw (Asserter.FailFast($"Error diffMode {e}"));
        }
    }

    private static State Read()
    {
        try
        {
            string json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<State>(json);
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
