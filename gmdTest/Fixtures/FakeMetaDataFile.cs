using gmd.Server.Private.Augmented.Private;

namespace gmdTest.Fixtures;

// An in-memory IMetaDataFile, i.e. the metadata's file in a repository's git dir, by working folder,
// without touching '<repo>/.git/gmd-metadata.json'
class FakeMetaDataFile : IMetaDataFile
{
    public Dictionary<string, string> TextByPath { get; } = [];

    // How many times it was written
    public int Writes { get; private set; }

    // Holds the next write until the task completes, so that a test can start another meanwhile
    public Task? NextWriteHeld { get; set; }

    public bool Exists(string wd) => TextByPath.ContainsKey(wd);

    public Task<Result<string>> ReadAsync(string wd) =>
        Task.FromResult<Result<string>>(
            TextByPath.TryGetValue(wd, out var text) ? text : new Error($"No file for '{wd}'")
        );

    public async Task<Result> WriteAsync(string wd, string text)
    {
        Writes++;
        var held = NextWriteHeld;
        NextWriteHeld = null;
        if (held != null)
            await held;

        TextByPath[wd] = text;
        return Result.Ok;
    }
}
