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
    readonly Dictionary<string, object> cache = [];

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
        // Thrown, so that state which never reached the file is not cached as if it had
        var written = Result.Catch(() => File.WriteAllText(path, JsonSerializer.Serialize(state, options)));
        if (written is Error e)
            throw Asserter.FailFast($"Failed to write '{path}': {e.Message}");
        cache[path] = state!;
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

            var jsonResult = Result.Catch(() => File.ReadAllText(path));
            if (jsonResult is not string json)
                throw Asserter.FailFast(jsonResult.Error.Message);
            var state =
                JsonSerializer.Deserialize<T>(json) ?? throw Asserter.FailFast($"Failed to deserialize '{path}'");
            cache[path] = state;
            return state;
        }
        catch (Exception e)
        {
            throw Asserter.FailFast($"Failed to read '{path}': {e.Message}");
        }
    }
}
