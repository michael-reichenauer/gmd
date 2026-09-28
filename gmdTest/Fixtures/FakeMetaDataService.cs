using gmd.Server.Private.Augmented.Private;

namespace gmdTest.Fixtures;

// A double for IMetaDataService, the user's branch choices stored as git key/value data.
// RepoBuilder passes the choices in with the git repo, so nothing is read from or written to git
// here. Changes are made to the metadata it was given, for the tests asserting what was stored
// (MetaDataTest covers how it is stored).
class FakeMetaDataService : IMetaDataService
{
    readonly MetaData metaData;

    public FakeMetaDataService(MetaData metaData) => this.metaData = metaData;

    public Task<Result<MetaData>> GetMetaDataAsync(string path) => Task.FromResult<Result<MetaData>>(metaData);

    public Task<Result> UpdateMetaDataAsync(string path, Action<MetaData> update)
    {
        update(metaData);
        return Task.FromResult(Result.Ok);
    }

    public Task<Result> FetchMetaDataAsync(string path) => Task.FromResult(Result.Ok);

    public Task<Result> PushMetaDataAsync(string path) => Task.FromResult(Result.Ok);

    // Kept the way MetaDataService keeps them, so a test can see what was
    public Task<Result> AddWitnessedAsync(string path, IReadOnlyList<WitnessedBranch> witnessed)
    {
        witnessed.ToList().ForEach(w => metaData.SetWitnessed(w.Id, w.BranchName));
        return Task.FromResult(Result.Ok);
    }
}
