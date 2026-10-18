using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// The branch choices shared through origin, over two real clones of one: a value changed in one
// clone and pushed is taken over by the other's fetch, and a value changed here and not pushed yet
// survives a fetch. Git updates a ref to a blob on a fetch without asking whether it is a fast
// forward, so a fetch into the local value lost that change, and the merge took origin's.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class MetaDataServiceIntegrationTest
{
    TempRepo repo = null!;
    string other = "";
    MetaDataService service = null!;

    [TestInitialize]
    public async Task Init()
    {
        repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -q -u origin main");

        other = repo.Path + "-other";
        repo.TrackFolder(other);
        await repo.GitAsync($"clone -q \"{repo.Path}-origin\" \"{other}\"");

        var config = new FakeRepoConfig();
        config.Set(repo.Path, c => c.SyncMetaData = true);
        config.Set(other, c => c.SyncMetaData = true);
        service = new MetaDataService(repo.Git, config, new MetaDataFile());
    }

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    async Task SetAsync(string path, string branch, string sid = "abc123") =>
        AssertOk(await service.UpdateMetaDataAsync(path, m => m.SetCommitBranch(sid, branch)));

    async Task<string> GetAsync(string path)
    {
        var metaData = AssertOk(await service.GetMetaDataAsync(path));
        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var branch, out _));
        return branch;
    }

    // A gmd before kept the metadata in the repository, under the ref it is shared through, which is
    // read until the first write moves it into the file in the git dir, and deletes the ref
    [TestMethod]
    public async Task TestTheFirstWriteMovesTheMetaDataOutOfTheRepository()
    {
        var valuePath = System.IO.Path.Join(repo.Path, ".git", "value.json");
        File.WriteAllText(valuePath, """{ "CommitBranchBySid": { "abc123": "*dev" } }""");
        var id = (await repo.GitAsync($"hash-object -w \"{valuePath}\"")).Trim();
        File.Delete(valuePath);
        await repo.GitAsync($"update-ref refs/gmd-metadata-key-value/data {id}");
        Assert.AreEqual("dev", await GetAsync(repo.Path), "Read from the ref");

        await SetAsync(repo.Path, "main", "def456");

        Assert.AreEqual("dev", await GetAsync(repo.Path), "Read from the file");
        Assert.AreEqual("", await repo.GitAsync("for-each-ref refs/gmd-metadata-key-value"), "The ref is gone");
        Assert.IsTrue(File.Exists(System.IO.Path.Join(repo.Path, ".git", MetaDataFile.FileName)));
    }

    [TestMethod]
    public async Task TestAChangeNotPushedYetSurvivesAFetch()
    {
        await SetAsync(repo.Path, "dev");
        AssertOk(await service.PushMetaDataAsync(repo.Path));

        await SetAsync(repo.Path, "main");
        AssertOk(await service.FetchMetaDataAsync(repo.Path));

        Assert.AreEqual("main", await GetAsync(repo.Path));
    }

    [TestMethod]
    public async Task TestAChangePushedByAnotherCloneIsTakenOver()
    {
        await SetAsync(repo.Path, "dev");
        AssertOk(await service.PushMetaDataAsync(repo.Path));
        AssertOk(await service.FetchMetaDataAsync(other));
        Assert.AreEqual("dev", await GetAsync(other));

        await SetAsync(other, "main");
        AssertOk(await service.PushMetaDataAsync(other));
        AssertOk(await service.FetchMetaDataAsync(repo.Path));

        Assert.AreEqual("main", await GetAsync(repo.Path));
    }

    // A choice another clone pushed since the last fetch here is not lost by a push from here: the
    // value was pushed with --force over origin's, so it was gone from origin, and from every clone
    // that fetched after. The push is leased on origin's value as last fetched, and refused, origin's
    // is then fetched and merged, and the merge pushed.
    [TestMethod]
    public async Task TestAChoicePushedByAnotherCloneIsNotPushedOver()
    {
        await SetAsync(repo.Path, "dev");
        AssertOk(await service.PushMetaDataAsync(repo.Path));
        AssertOk(await service.FetchMetaDataAsync(other));
        await SetAsync(other, "feature", "def456");
        AssertOk(await service.PushMetaDataAsync(other));

        await SetAsync(repo.Path, "fix", "aaa111");
        AssertOk(await service.PushMetaDataAsync(repo.Path));

        var origin = await repo.GitAsync($"-C \"{repo.Path}-origin\" cat-file -p refs/gmd-metadata-key-value/data");
        StringAssert.Contains(origin, "def456", "The other clone's choice is still on origin");
        StringAssert.Contains(origin, "aaa111", "and this one's was pushed");
        var here = AssertOk(await service.GetMetaDataAsync(repo.Path));
        Assert.IsTrue(here.TryGetCommitBranch("def456", out var branch, out _));
        Assert.AreEqual("feature", branch, "Merged here");
    }
}
