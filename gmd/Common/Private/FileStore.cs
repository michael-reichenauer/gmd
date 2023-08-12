using System.Text.Json;

namespace gmd.Common.Private;

// Read and write objects to specified file as json text.
interface IFileStore
{
    T Get<T>(string path);
    T Set<T>(string path, Action<T> set);
}


[SingleInstance]
class FileStore : IFileStore
{
    readonly JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
    readonly Dictionary<string, object> cache = new Dictionary<string, object>();

    public T Get<T>(string path) => Read<T>(path);

    public T Set<T>(string path, Action<T> set) => SetValue(path, set);

    T SetValue<T>(string path, Action<T> setState)
    {
        var state = Read<T>(path);
        setState(state);
        Write(path, state);
        return state;
    }

    void Write<T>(string path, T state)
    {
        try
        {
            string json = JsonSerializer.Serialize(state, options);
            if (!Try(out var e, () => File.WriteAllText(path, json))) Asserter.FailFast(e.ErrorMessage);
            cache[path] = state!;
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
            if (cache.TryGetValue(path, out var cached))
            {
                return (T)cached;
            }

            if (!File.Exists(path))
            {
                Write(path, (T)Activator.CreateInstance(typeof(T))!);
            }

            if (!Try(out var json, out var e, () => File.ReadAllText(path))) throw Asserter.FailFast(e.ErrorMessage);
            var state = JsonSerializer.Deserialize<T>(json) ?? throw Asserter.FailFast($"Failed to deserialize '{path}'");
            cache[path] = state;
            return state;
        }
        catch (Exception e)
        {
            throw Asserter.FailFast(e, $"Failed to read '{path}'");
        }
    }
}
