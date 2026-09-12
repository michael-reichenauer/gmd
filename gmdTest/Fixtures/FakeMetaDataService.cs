using gmd.Server.Private.Augmented.Private;

namespace gmdTest.Fixtures;

// A double for IMetaDataService, the user's branch choices stored as git key/value data.
// RepoBuilder passes the choices in with the git repo, so nothing is read from or written to git
// here. Writes are accepted and dropped, since the tests assert on the repo, not on the storage
// (MetaDataTest covers what gets stored).
class FakeMetaDataService : IMetaDataService
{
    readonly MetaData metaData;

    public FakeMetaDataService(MetaData metaData) => this.metaData = metaData;

    public Task<Result<MetaData>> GetMetaDataAsync(string path) => Task.FromResult<Result<MetaData>>(metaData);

    public Task<Result> SetMetaDataAsync(string path, MetaData metaData) => Task.FromResult(Result.Ok);

    public Task<Result> FetchMetaDataAsync(string path) => Task.FromResult(Result.Ok);

    public Task<Result> PushMetaDataAsync(string path) => Task.FromResult(Result.Ok);
}
