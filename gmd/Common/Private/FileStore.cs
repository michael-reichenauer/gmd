using System.Text.Json;

namespace gmd.Common.Private;

// Read and write objects to specified file as json text.
interface IFileStore
{
    T Get<T>(string path);
    T Set<T>(string path, Action<T> set);
}

// Several gmd instances can share a file (~/.gmdconfig always, a repository's .gmdconfig when it is
// open twice, e.g. once per worktree), so the file is never trusted to be as this instance left it:
// - Set reads the file afresh, not from the cache, so another instance's changes are kept rather
//   than overwritten by this one's stale copy. Get reads the cache, as it is read while drawing.
// - A file is written beside it and renamed over it, so it is never seen half written, by another
//   instance or after a crash in the middle of a write.
// - A file that is not valid json is set aside, as '<file>.unreadable', and started anew, rather than
//   stopping gmd on every start until the user finds and deletes it.
[SingleInstance]
class FileStore : IFileStore
{
    public const string UnreadableSuffix = ".unreadable";

    readonly JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
    readonly Dictionary<string, object> cache = [];

    public T Get<T>(string path)
    {
        lock (cache)
            return cache.TryGetValue(path, out var cached) ? (T)cached : Read<T>(path);
    }

    public T Set<T>(string path, Action<T> set)
    {
        lock (cache)
        {
            var state = Read<T>(path);
            set(state);
            Write(path, state);
            return state;
        }
    }

    void Write<T>(string path, T state)
    {
        // Named for this write alone, since two instances, or two threads, may write at once
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var written = Result.Catch(() =>
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, options));
            File.Move(tempPath, path, overwrite: true);
        });
        if (written is Error e)
        {
            Result.Catch(() => File.Delete(tempPath));
            // Thrown, so that state which never reached the file is not cached as if it had
            throw Asserter.FailFast($"Failed to write '{path}': {e.Message}");
        }
        cache[path] = state!;
    }

    // The file as it is now, which another instance may have written since this one last read it
    T Read<T>(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var jsonResult = Result.Catch(() => File.ReadAllText(path));
                if (jsonResult is not string json)
                    throw Asserter.FailFast(jsonResult.Error.Message);
                if (Deserialize<T>(json, out var why) is T state)
                {
                    cache[path] = state;
                    return state;
                }

                var asidePath = path + UnreadableSuffix;
                if (Result.Catch(() => File.Move(path, asidePath, overwrite: true)) is Error e)
                    throw Asserter.FailFast($"Failed to set aside unreadable '{path}': {e.Message}");
                Log.Warn($"'{path}' could not be read, so it was set aside as '{asidePath}' and started anew: {why}");
            }

            var initial = (T)Activator.CreateInstance(typeof(T))!;
            Write(path, initial);
            return initial;
        }
        catch (Exception e)
        {
            throw Asserter.FailFast($"Failed to read '{path}': {e.Message}");
        }
    }

    // Null for text that is not json of the type, an empty file included, with why in 'why'
    static T? Deserialize<T>(string json, out string why)
    {
        try
        {
            why = "it holds null";
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException e)
        {
            why = e.Message;
            return default;
        }
    }
}
