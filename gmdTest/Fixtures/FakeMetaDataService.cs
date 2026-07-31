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

    public Task<R<MetaData>> GetMetaDataAsync(string path) => Task.FromResult<R<MetaData>>(metaData);

    public Task<R> SetMetaDataAsync(string path, MetaData metaData) => Task.FromResult(R.Ok);

    public Task<R> FetchMetaDataAsync(string path) => Task.FromResult(R.Ok);

    public Task<R> PushMetaDataAsync(string path) => Task.FromResult(R.Ok);
}
