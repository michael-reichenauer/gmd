using System.Text.Json;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Augmented;

// MetaDataService is the storage side of the user's branch choices: the choices are serialized to
// JSON and kept in git under a ref of their own, so they can be pushed and pulled between users
// like a branch. MetaDataTest covers the choices themselves, this covers getting them in and out
// of git and merging another user's.
[TestClass]
public class MetaDataServiceTest
{
    const string Path = "/wd";
    const string Key = "data";

    FakeGit git = null!;
    FakeRepoConfig repoConfig = null!;
    MetaDataService service = null!;

    [TestInitialize]
    public void Init()
    {
        git = new FakeGit();
        repoConfig = new FakeRepoConfig();
        service = new MetaDataService(git, repoConfig);
    }

    // Sharing is off unless the user turns it on, which is what makes the fetch and push below
    // no-ops by default
    void EnableSync() => repoConfig.Set(Path, c => c.SyncMetaData = true);

    static string Json(params (string sid, string branch)[] choices) =>
        JsonSerializer.Serialize(new MetaData { CommitBranchBySid = choices.ToDictionary(c => c.sid, c => c.branch) });

    [TestMethod]
    public async Task TestSetAndGetRoundTrip()
    {
        var metaData = new MetaData();
        metaData.SetCommitBranch("abc123", "dev");

        Assert.IsTrue(Try(out var e, await service.SetMetaDataAsync(Path, metaData)), $"{e}");
        Assert.IsTrue(Try(out var read, out e, await service.GetMetaDataAsync(Path)), $"{e}");

        Assert.IsTrue(read.TryGetCommitBranch("abc123", out var name, out var isSetByUser));
        Assert.AreEqual("dev", name);
        Assert.IsTrue(isSetByUser);
    }

    // A repo nobody has made a choice in has no key at all, which is not an error
    [TestMethod]
    public async Task TestGetWithNoStoredValueIsEmptyMetaData()
    {
        Assert.IsTrue(Try(out var metaData, out var e, await service.GetMetaDataAsync(Path)), $"{e}");

        Assert.AreEqual(0, metaData.CommitBranchBySid.Count);
        CollectionAssert.AreEqual(new[] { "get data" }, git.ValueCalls);
    }

    [TestMethod]
    public async Task TestGetWithUnreadableValueIsAnError()
    {
        git.Values[Key] = "not json";

        var result = await service.GetMetaDataAsync(Path);

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the deserialization to fail");
    }

    // Sync is off by default, so nothing reaches git
    [TestMethod]
    public async Task TestFetchDoesNothingWhenSyncIsDisabled()
    {
        Assert.IsTrue(Try(out var e, await service.FetchMetaDataAsync(Path)), $"{e}");

        Assert.AreEqual(0, git.ValueCalls.Count);
    }

    [TestMethod]
    public async Task TestPushDoesNothingWhenSyncIsDisabled()
    {
        Assert.IsTrue(Try(out var e, await service.PushMetaDataAsync(Path)), $"{e}");

        Assert.AreEqual(0, git.ValueCalls.Count);
    }

    [TestMethod]
    public async Task TestPushSendsTheValueWhenSyncIsEnabled()
    {
        EnableSync();
        git.Values[Key] = Json(("abc123", "*dev"));

        Assert.IsTrue(Try(out var e, await service.PushMetaDataAsync(Path)), $"{e}");

        Assert.AreEqual(git.Values[Key], git.RemoteValues[Key]);
    }

    // Nobody has pushed choices yet, so there is nothing to fetch. Not an error, it is the normal
    // state of a repo where no one uses gmd.
    [TestMethod]
    public async Task TestFetchWithNoRemoteValueIsOk()
    {
        EnableSync();

        Assert.IsTrue(Try(out var e, await service.FetchMetaDataAsync(Path)), $"{e}");

        CollectionAssert.AreEqual(new[] { "get data", "pull data" }, git.ValueCalls);
    }

    // Another user's choices are taken over, and since the merge changed the local data it is
    // written back so the next push shares the merged result
    [TestMethod]
    public async Task TestFetchTakesOverRemoteChoices()
    {
        EnableSync();
        git.RemoteValues[Key] = Json(("remote1", "*dev"));

        Assert.IsTrue(Try(out var e, await service.FetchMetaDataAsync(Path)), $"{e}");
        Assert.IsTrue(Try(out var metaData, out e, await service.GetMetaDataAsync(Path)), $"{e}");

        Assert.IsTrue(metaData.TryGetCommitBranch("remote1", out var name, out _));
        Assert.AreEqual("dev", name);
    }

    // The local choices the remote does not have are kept, so a fetch never loses the user's own
    // work
    [TestMethod]
    public async Task TestFetchKeepsLocalChoicesTheRemoteDoesNotHave()
    {
        EnableSync();
        git.Values[Key] = Json(("local1", "*main"));
        git.RemoteValues[Key] = Json(("remote1", "*dev"));

        Assert.IsTrue(Try(out var e, await service.FetchMetaDataAsync(Path)), $"{e}");
        Assert.IsTrue(Try(out var metaData, out e, await service.GetMetaDataAsync(Path)), $"{e}");

        CollectionAssert.AreEquivalent(new[] { "local1", "remote1" }, metaData.CommitBranchBySid.Keys.ToArray());
    }

    // When the same commit was assigned differently by two users, the remote value wins
    [TestMethod]
    public async Task TestFetchPrefersTheRemoteValueOnConflict()
    {
        EnableSync();
        git.Values[Key] = Json(("abc123", "*main"));
        git.RemoteValues[Key] = Json(("abc123", "*dev"));

        Assert.IsTrue(Try(out var e, await service.FetchMetaDataAsync(Path)), $"{e}");
        Assert.IsTrue(Try(out var metaData, out e, await service.GetMetaDataAsync(Path)), $"{e}");

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("dev", name);
    }

    // Nothing changed, so nothing is written back
    [TestMethod]
    public async Task TestFetchWritesNothingWhenAlreadyInSync()
    {
        EnableSync();
        git.Values[Key] = Json(("abc123", "*dev"));
        git.RemoteValues[Key] = Json(("abc123", "*dev"));

        Assert.IsTrue(Try(out var e, await service.FetchMetaDataAsync(Path)), $"{e}");

        CollectionAssert.DoesNotContain(git.ValueCalls, "set data");
    }

    // A removed choice is stored as an empty value rather than being deleted, so the removal
    // survives the merge instead of being undone by the other user's data
    [TestMethod]
    public async Task TestFetchKeepsARemovedChoiceRemoved()
    {
        EnableSync();
        var local = new MetaData();
        local.SetCommitBranch("abc123", "dev");
        local.RemoveCommitBranch("abc123");
        Assert.IsTrue(Try(out var e, await service.SetMetaDataAsync(Path, local)), $"{e}");
        git.RemoteValues[Key] = git.Values[Key];

        Assert.IsTrue(Try(out e, await service.FetchMetaDataAsync(Path)), $"{e}");
        Assert.IsTrue(Try(out var metaData, out e, await service.GetMetaDataAsync(Path)), $"{e}");

        Assert.IsFalse(metaData.TryGetCommitBranch("abc123", out _, out _));
        Assert.IsTrue(metaData.CommitBranchBySid.ContainsKey("abc123"), "The removal is still shareable");
    }
}
