using gmd.Common;
using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// A branch deleted and brought back over a real repository and its origin: the delete is recorded,
// since git deletes the branch's reflog with it, and the restore puts the branch back where it was,
// tracking what it tracked, and origin's back only if no one pushed a branch of the name since.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  ./test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class DeletedBranchIntegrationTest
{
    TempRepo repo = null!;
    readonly FakeRepoConfig config = new();
    AugmentedService service = null!;

    [TestInitialize]
    public async Task Init()
    {
        repo = await TempRepo.CreateAsync();
        service = RepoBuilder.NewAugmentedService(
            repo.Git,
            new FakeMetaDataService(new MetaData()),
            new FakeFileMonitor(),
            config
        );
    }

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    async Task<Repo> ReadAsync() => AssertOk(await service.GetRepoAsync(repo.Path));

    async Task<IReadOnlyList<DeletedBranch>> DeletedAsync() =>
        AssertOk(await service.GetDeletedBranchesAsync(await ReadAsync()));

    async Task DeleteAsync(string localName, string remoteName) =>
        AssertOk(await service.DeleteBranchAsync(await ReadAsync(), localName, remoteName, true));

    async Task RestoreAsync(DeletedBranch deleted, bool isLocal, bool isRemote) =>
        AssertOk(await service.RestoreBranchAsync(await ReadAsync(), deleted, isLocal, isRemote));

    async Task<string> TipAsync(string reference) => (await repo.GitAsync($"rev-parse {reference}")).Trim();

    async Task<string> OriginTipAsync(string name) =>
        (await repo.GitAsync($"ls-remote origin refs/heads/{name}")).Split('\t')[0].Trim();

    async Task<string> UpstreamAsync(string name) =>
        (await repo.GitAllowFailAsync($"rev-parse --abbrev-ref {name}@{{upstream}}")).Trim();

    List<RecordedDelete> Records => config.Get(repo.Path).DeletedBranches;

    // main on origin, and dev pushed with one commit and a second one not pushed, with main checked out
    async Task<(string Tip, string PushedTip)> DevOnOriginAsync()
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -q -u origin main");
        await repo.GitAsync("checkout -q -b dev");
        var pushed = await repo.CommitFileAsync("d.txt", "d\n", "Dev work");
        await repo.GitAsync("push -q -u origin dev");
        var tip = await repo.CommitFileAsync("e.txt", "e\n", "More dev work");
        await repo.GitAsync("checkout -q main");
        return (tip, pushed);
    }

    [TestMethod]
    public async Task TestALocalBranchDeletedAndRestored()
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        await repo.GitAsync("checkout -q -b dev");
        var tip = await repo.CommitFileAsync("d.txt", "d\n", "Dev work");
        await repo.GitAsync("checkout -q main");

        await DeleteAsync("dev", "");

        Assert.AreEqual("main", (await repo.GitAsync("branch --format=%(refname:short)")).Trim());
        var deleted = (await DeletedAsync()).Single();
        Assert.AreEqual(("dev", tip, "", "", "Dev work"), Fields(deleted));

        await RestoreAsync(deleted, true, false);

        Assert.AreEqual(tip, await TipAsync("dev"));
        Assert.AreEqual(0, (await DeletedAsync()).Count, "Restored, so there is nothing to restore");
        Assert.AreEqual(0, Records.Count, "And its record is forgotten");
    }

    // Both sides deleted are one record, and restored, the local branch tracks origin's again
    [TestMethod]
    public async Task TestABranchDeletedOnBothSidesAndRestored()
    {
        var (tip, pushedTip) = await DevOnOriginAsync();

        await DeleteAsync("dev", "origin/dev");

        Assert.AreEqual("", await OriginTipAsync("dev"));
        var deleted = (await DeletedAsync()).Single();
        Assert.AreEqual(("dev", tip, "origin/dev", pushedTip, "More dev work"), Fields(deleted));

        await RestoreAsync(deleted, true, true);

        Assert.AreEqual(tip, await TipAsync("dev"));
        Assert.AreEqual(pushedTip, await OriginTipAsync("dev"));
        Assert.AreEqual("origin/dev", await UpstreamAsync("dev"));
        Assert.AreEqual(0, (await DeletedAsync()).Count);
    }

    // A local branch deleted alone tracks its remote branch again, which was kept
    [TestMethod]
    public async Task TestALocalBranchRestoredTracksTheRemoteBranchItTracked()
    {
        var (tip, _) = await DevOnOriginAsync();

        await DeleteAsync("dev", "");
        var deleted = (await DeletedAsync()).Single();
        Assert.AreEqual(("dev", tip, "origin/dev", "", "More dev work"), Fields(deleted));

        await RestoreAsync(deleted, true, false);

        Assert.AreEqual(tip, await TipAsync("dev"));
        Assert.AreEqual("origin/dev", await UpstreamAsync("dev"));
    }

    // Restoring one side leaves the other to restore later, and the local branch, restored before
    // origin's, tracks it once it is back
    [TestMethod]
    public async Task TestTheSidesRestoredOneAtATime()
    {
        var (tip, pushedTip) = await DevOnOriginAsync();
        await DeleteAsync("dev", "origin/dev");

        await RestoreAsync((await DeletedAsync()).Single(), true, false);

        Assert.AreEqual(tip, await TipAsync("dev"));
        Assert.AreEqual("", await OriginTipAsync("dev"));
        Assert.AreEqual("", await UpstreamAsync("dev"), "Nothing to track yet");
        var remoteLeft = (await DeletedAsync()).Single();
        Assert.AreEqual(("dev", "", "origin/dev", pushedTip, "More dev work"), Fields(remoteLeft));

        await RestoreAsync(remoteLeft, false, true);

        Assert.AreEqual(pushedTip, await OriginTipAsync("dev"));
        Assert.AreEqual("origin/dev", await UpstreamAsync("dev"));
        Assert.AreEqual(0, (await DeletedAsync()).Count);
    }

    // Someone pushed a branch of the name since it was read as deleted: it is not overwritten
    [TestMethod]
    public async Task TestABranchPushedSinceIsNotOverwritten()
    {
        await DevOnOriginAsync();
        await DeleteAsync("", "origin/dev");
        var deleted = (await DeletedAsync()).Single();
        var main = await TipAsync("main");
        await repo.GitAsync("push -q origin main:refs/heads/dev");

        var error = AssertError(await service.RestoreBranchAsync(await ReadAsync(), deleted, false, true));

        StringAssert.Contains(error.Message, "Origin has a branch 'origin/dev' again");
        Assert.AreEqual(main, await OriginTipAsync("dev"));
        Assert.AreEqual(
            0,
            (await DeletedAsync()).Count,
            "A branch of the name is back, so there is nothing to restore"
        );
    }

    // A branch made again under the name, by hand, is what is there: the record is forgotten
    [TestMethod]
    public async Task TestARecordIsForgottenOnceABranchOfTheNameIsBack()
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        await repo.GitAsync("branch dev");
        await DeleteAsync("dev", "");
        Assert.AreEqual(1, Records.Count);

        await repo.GitAsync("branch dev");

        Assert.AreEqual(0, (await DeletedAsync()).Count);
        Assert.AreEqual(0, Records.Count);
    }

    // A tip git no longer has, which a gc pruned, is no branch to restore
    [TestMethod]
    public async Task TestARecordWhoseCommitIsGoneIsForgotten()
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        Records.Add(new RecordedDelete { Name = "dev", TipId = RepoBuilder.Sha("dead") });

        Assert.AreEqual(0, (await DeletedAsync()).Count);
        Assert.AreEqual(0, Records.Count);
    }

    static (string, string, string, string, string) Fields(DeletedBranch d) =>
        (d.Name, d.TipId, d.RemoteName, d.RemoteTipId, d.Subject);
}
