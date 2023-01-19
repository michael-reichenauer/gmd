using System.Text.Json;

namespace gmd.Common;


interface IFileStore
{
    T Get<T>(string path);
    void Set<T>(string path, Action<T> set);
}

class FileStore : IFileStore
{
    public T Get<T>(string path) => Read<T>(path);

    public void Set<T>(string path, Action<T> set) => SetValue(path, set);

    void SetValue<T>(string path, Action<T> setState)
    {
        var state = Read<T>(path);
        setState(state);
        Write(path, state);
    }

    void Write<T>(string path, T state)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(state, options);
            if (!Try(out var e, Files.WriteAllText(path, json))) Asserter.FailFast(e.ErrorMessage);
        }
        catch (Exception e)
        {
            throw Asserter.FailFast(e, $"Failed to write '{path}'");
        }
    }

    T Read<T>(string path)
    {
        try
        {
            if (!Files.Exists(path))
            {
                Write(path, new State());
            }

            if (!Try(out var json, out var e, Files.ReadAllText(path))) throw Asserter.FailFast(e.ErrorMessage);
            var state = JsonSerializer.Deserialize<T>(json);
            if (state == null)
            {
                throw Asserter.FailFast($"Failed to deserialize '{path}'");
            }

            return state;
        }
        catch (Exception e)
        {
            throw Asserter.FailFast(e, $"Failed to read '{path}'");
        }
    }
}
