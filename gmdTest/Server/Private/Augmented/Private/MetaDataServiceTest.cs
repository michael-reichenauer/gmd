using System.Text.Json;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Augmented;

// MetaDataService is the storage side of the user's branch choices: the choices are serialized to
// JSON and kept in a file in the git dir, and shared, when that is turned on, through a git value
// that is pushed and fetched like a branch. MetaDataTest covers the choices themselves, this covers
// getting them in and out of the file and git, and merging another user's.
[TestClass]
public class MetaDataServiceTest
{
    const string Path = "/wd";
    const string Key = "data";

    FakeGit git = null!;
    FakeRepoConfig repoConfig = null!;
    FakeMetaDataFile file = null!;
    MetaDataService service = null!;

    [TestInitialize]
    public void Init()
    {
        git = new FakeGit();
        repoConfig = new FakeRepoConfig();
        file = new FakeMetaDataFile();
        service = new MetaDataService(git, repoConfig, file);
    }

    // What the file holds, as this gmd keeps the metadata
    string Local
    {
        get => file.TextByPath[Path];
        set => file.TextByPath[Path] = value;
    }

    // Sharing is off unless the user turns it on, which is what makes the fetch and push below
    // no-ops by default
    void EnableSync() => repoConfig.Set(Path, c => c.SyncMetaData = true);

    static string Json(params (string id, string branch)[] choices) =>
        JsonSerializer.Serialize(new MetaData { CommitBranchById = choices.ToDictionary(c => c.id, c => c.branch) });

    [TestMethod]
    public async Task TestSetAndGetRoundTrip()
    {
        AssertOk(await service.UpdateMetaDataAsync(Path, m => m.SetCommitBranch("abc123", "dev")));
        var read = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(read.TryGetCommitBranch("abc123", out var name, out var isSetByUser));
        Assert.AreEqual("dev", name);
        Assert.IsTrue(isSetByUser);
    }

    // The metadata is read, changed and written whole, so the changes are made one at a time, each on
    // what the one before wrote: the reflog's facts are kept in the background, and a choice the user
    // made meanwhile was lost when the facts were written with the copy read before it
    [TestMethod]
    public async Task TestAChangeWaitsForTheOneBefore()
    {
        var held = new TaskCompletionSource();
        file.NextWriteHeld = held.Task;
        var id = RepoBuilder.Sha("e1");

        var keeping = service.AddWitnessedAsync(Path, [new WitnessedBranch(id, "feature1")]);
        var choosing = service.UpdateMetaDataAsync(Path, m => m.SetCommitBranch("abc123", "dev"));
        held.SetResult();
        AssertOk(await keeping);
        AssertOk(await choosing);

        var read = AssertOk(await service.GetMetaDataAsync(Path));
        Assert.IsTrue(read.TryGetWitnessedBranch(id, out _), "The facts");
        Assert.IsTrue(read.TryGetCommitBranch("abc123", out _, out _), "The choice");
    }

    // What the reflog witnessed is written only when something is new, so a repo whose facts are
    // all kept is not written to on every read
    [TestMethod]
    public async Task TestWitnessedBranchesAreWrittenOnlyWhenNew()
    {
        var id = RepoBuilder.Sha("e1");
        AssertOk(await service.AddWitnessedAsync(Path, [new WitnessedBranch(id, "feature1")]));
        AssertOk(await service.AddWitnessedAsync(Path, [new WitnessedBranch(id, "feature1")]));

        Assert.AreEqual(1, file.Writes);
        var read = AssertOk(await service.GetMetaDataAsync(Path));
        Assert.IsTrue(read.TryGetWitnessedBranch(id, out var name));
        Assert.AreEqual("feature1", name);
    }

    // A repo nobody has made a choice in has no file, nor a value a gmd before kept, which is not an
    // error, and nothing is written for it
    [TestMethod]
    public async Task TestGetWithNoStoredValueIsEmptyMetaData()
    {
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.AreEqual(0, metaData.CommitBranchById.Count);
        CollectionAssert.AreEqual(new[] { "get data" }, git.ValueCalls);
        Assert.AreEqual(0, file.Writes);
    }

    [TestMethod]
    public async Task TestGetWithUnreadableFileIsAnError()
    {
        Local = "not json";

        var result = await service.GetMetaDataAsync(Path);

        AssertError(result, "Expected the deserialization to fail");
    }

    // The file is read, and git is not asked, once there is one
    [TestMethod]
    public async Task TestGetReadsTheFile()
    {
        Local = Json(("abc123", "*dev"));
        git.Values[Key] = Json(("abc123", "*main"));

        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev", name);
        Assert.AreEqual(0, git.ValueCalls.Count);
    }

    // A gmd before kept the metadata in the repository, as a git value, which is read as long as there
    // is no file, and a read writes nothing: neither the file nor the repository
    [TestMethod]
    public async Task TestGetReadsTheValueAGmdBeforeKeptUntilThereIsAFile()
    {
        git.Values[Key] = Json(("abc123", "*dev"));

        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev", name);
        Assert.AreEqual(0, file.Writes);
        CollectionAssert.AreEqual(new[] { "get data" }, git.ValueCalls);
    }

    // The first write moves the metadata into the file, the value read included, and deletes the git
    // value, so that nothing of it is left in the repository
    [TestMethod]
    public async Task TestTheFirstWriteMovesTheValueIntoTheFile()
    {
        git.Values[Key] = Json(("abc123", "*dev"));

        AssertOk(await service.UpdateMetaDataAsync(Path, m => m.SetCommitBranch("def456", "main")));

        Assert.IsFalse(git.Values.ContainsKey(Key), "The git value is deleted");
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));
        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev", name, "The value read is kept");
        Assert.IsTrue(metaData.TryGetCommitBranch("def456", out name, out _));
        Assert.AreEqual("main", name, "and the change");
    }

    // ... once: the git value is deleted at the first write alone
    [TestMethod]
    public async Task TestALaterWriteLeavesGitAlone()
    {
        Local = Json(("abc123", "*dev"));

        AssertOk(await service.UpdateMetaDataAsync(Path, m => m.SetCommitBranch("def456", "main")));

        Assert.AreEqual(0, git.ValueCalls.Count, string.Join(", ", git.ValueCalls));
    }

    // Sync is off by default, so nothing reaches git
    [TestMethod]
    public async Task TestFetchDoesNothingWhenSyncIsDisabled()
    {
        AssertOk(await service.FetchMetaDataAsync(Path));

        Assert.AreEqual(0, git.ValueCalls.Count);
    }

    [TestMethod]
    public async Task TestPushDoesNothingWhenSyncIsDisabled()
    {
        AssertOk(await service.PushMetaDataAsync(Path));

        Assert.AreEqual(0, git.ValueCalls.Count);
    }

    [TestMethod]
    public async Task TestPushSendsTheValueWhenSyncIsEnabled()
    {
        EnableSync();
        Local = Json(("abc123", "*dev"));

        AssertOk(await service.PushMetaDataAsync(Path));

        var pushed = MetaData.Parse(git.RemoteValues[Key]);
        Assert.IsTrue(pushed.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev", name);
    }

    // Nobody has pushed choices yet, so there is nothing to fetch. Not an error, it is the normal
    // state of a repo where no one uses gmd.
    [TestMethod]
    public async Task TestFetchWithNoRemoteValueIsOk()
    {
        EnableSync();

        AssertOk(await service.FetchMetaDataAsync(Path));

        CollectionAssert.AreEqual(new[] { "get data", "get origin data", "fetch data" }, git.ValueCalls);
        Assert.AreEqual(0, file.Writes);
    }

    // Another user's choices are taken over, and since the merge changed the local data it is
    // written back so the next push shares the merged result
    [TestMethod]
    public async Task TestFetchTakesOverRemoteChoices()
    {
        EnableSync();
        git.RemoteValues[Key] = Json(("remote1", "*dev"));

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch("remote1", out var name, out _));
        Assert.AreEqual("dev", name);
    }

    // The local choices the remote does not have are kept, so a fetch never loses the user's own
    // work
    [TestMethod]
    public async Task TestFetchKeepsLocalChoicesTheRemoteDoesNotHave()
    {
        EnableSync();
        Local = Json(("local1", "*main"));
        git.RemoteValues[Key] = Json(("remote1", "*dev"));

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        CollectionAssert.AreEquivalent(new[] { "local1", "remote1" }, metaData.CommitBranchById.Keys.ToArray());
    }

    // What another clone's reflog witnessed about a commit is a fact beside the choices, so taking it
    // over never replaces the choice made here of the same commit
    [TestMethod]
    public async Task TestFetchKeepsAChoiceBesideTheRemoteWitnessedBranch()
    {
        EnableSync();
        var id = RepoBuilder.Sha("e1");
        Local = Json((id, "*main"));
        var remote = new MetaData();
        remote.SetWitnessed(id, "dev");
        git.RemoteValues[Key] = JsonSerializer.Serialize(remote);

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch(id, out var name, out var isSetByUser));
        Assert.AreEqual("main", name);
        Assert.IsTrue(isSetByUser);
        Assert.IsTrue(metaData.TryGetWitnessedBranch(id, out name));
        Assert.AreEqual("dev", name);
    }

    // When the same commit was assigned differently here and on origin, with no record of origin's
    // value from before to say which side changed it, e.g. at the first fetch, the remote value wins
    [TestMethod]
    public async Task TestFetchPrefersTheRemoteValueOnConflict()
    {
        EnableSync();
        Local = Json(("abc123", "*main"));
        git.RemoteValues[Key] = Json(("abc123", "*dev"));

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev", name);
    }

    // Nothing changed, so nothing is written back
    [TestMethod]
    public async Task TestFetchWritesNothingWhenAlreadyInSync()
    {
        EnableSync();
        Local = Json(("abc123", "*dev"));
        git.RemoteValues[Key] = Json(("abc123", "*dev"));

        AssertOk(await service.FetchMetaDataAsync(Path));

        Assert.AreEqual(0, file.Writes);
    }

    // A removed choice is stored as an empty value rather than being deleted, so the removal
    // survives the merge instead of being undone by the other user's data
    [TestMethod]
    public async Task TestFetchKeepsARemovedChoiceRemoved()
    {
        EnableSync();
        AssertOk(
            await service.UpdateMetaDataAsync(
                Path,
                m =>
                {
                    m.SetCommitBranch("abc123", "dev");
                    m.RemoveCommitBranch("abc123");
                }
            )
        );
        git.RemoteValues[Key] = Local;

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsFalse(metaData.TryGetCommitBranch("abc123", out _, out _));
        Assert.IsTrue(metaData.CommitBranchById.ContainsKey("abc123"), "The removal is still shareable");
    }

    // A value changed here since the last fetch, which origin still has as it was, is kept: the fetch
    // took origin's over it, so a branch set by hand, an ambiguity resolved again or a branch renamed
    // was undone by the next background fetch until it was pushed
    [TestMethod]
    public async Task TestFetchKeepsAValueChangedHereSinceTheLastFetch()
    {
        EnableSync();
        Local = Json(("abc123", "*main"));
        git.OriginValues[Key] = Json(("abc123", "*dev"));
        git.RemoteValues[Key] = Json(("abc123", "*dev"));

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("main", name);
    }

    // ... while one changed on origin since, and not here, is taken over
    [TestMethod]
    public async Task TestFetchTakesAValueChangedOnOriginSinceTheLastFetch()
    {
        EnableSync();
        Local = Json(("abc123", "*dev"));
        git.OriginValues[Key] = Json(("abc123", "*dev"));
        git.RemoteValues[Key] = Json(("abc123", "*main"));

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("main", name);
    }

    // ... and one changed on both sides takes origin's, as before, so that every clone agrees
    [TestMethod]
    public async Task TestFetchPrefersOriginsValueWhenBothChangedIt()
    {
        EnableSync();
        Local = Json(("abc123", "*main"));
        git.OriginValues[Key] = Json(("abc123", "*dev"));
        git.RemoteValues[Key] = Json(("abc123", "*feature"));

        AssertOk(await service.FetchMetaDataAsync(Path));
        var metaData = AssertOk(await service.GetMetaDataAsync(Path));

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("feature", name);
    }

    // The fetch leaves the local value alone, and the merge writes it only when it changed
    [TestMethod]
    public async Task TestFetchNeverWritesOverTheLocalValue()
    {
        EnableSync();
        Local = Json(("abc123", "*main"));
        git.OriginValues[Key] = Json(("abc123", "*dev"));
        git.RemoteValues[Key] = Json(("abc123", "*dev"));

        AssertOk(await service.FetchMetaDataAsync(Path));

        Assert.AreEqual(0, file.Writes);
        Assert.AreEqual(Json(("abc123", "*main")), Local);
    }
}
