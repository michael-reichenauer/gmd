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

    // What the branch is set to track, whether that is there or not, as 'origin refs/heads/dev'
    async Task<string> UpstreamConfigAsync(string name) =>
        (
            (await repo.GitAllowFailAsync($"config branch.{name}.remote")).Trim()
            + " "
            + (await repo.GitAllowFailAsync($"config branch.{name}.merge")).Trim()
        ).Trim();

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
        Assert.AreEqual(0, Records.Count, "Both sides are back, so the record is forgotten as they are");
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
    // origin's, is set to track it, and tracks it once it is back. The row left has the subject of
    // origin's tip, which is what is restored then.
    [TestMethod]
    public async Task TestTheSidesRestoredOneAtATime()
    {
        var (tip, pushedTip) = await DevOnOriginAsync();
        await DeleteAsync("dev", "origin/dev");

        await RestoreAsync((await DeletedAsync()).Single(), true, false);

        Assert.AreEqual(tip, await TipAsync("dev"));
        Assert.AreEqual("", await OriginTipAsync("dev"));
        Assert.AreEqual("origin refs/heads/dev", await UpstreamConfigAsync("dev"), "Set, though it is not there yet");
        var remoteLeft = (await DeletedAsync()).Single();
        Assert.AreEqual(("dev", "", "origin/dev", pushedTip, "Dev work"), Fields(remoteLeft));

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

        Assert.AreEqual(
            "Origin has a branch 'origin/dev' again, which was left as it is, so nothing was restored",
            error.Message
        );
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

    // Restored and later deleted again, a branch is where it was the last time: nothing of the
    // first delete is left to join the second, which would offer to push origin's old tip back
    [TestMethod]
    public async Task TestABranchRestoredAndDeletedAgainIsWhereItWasTheLastTime()
    {
        var (_, pushedTip) = await DevOnOriginAsync();
        await DeleteAsync("dev", "origin/dev");
        await RestoreAsync((await DeletedAsync()).Single(), true, true);
        await repo.GitAsync("checkout -q dev");
        var newTip = await repo.CommitFileAsync("f.txt", "f\n", "Newer dev work");
        await repo.GitAsync("checkout -q main");
        await repo.GitAsync("push -q origin --delete dev");

        await DeleteAsync("dev", "");

        var deleted = (await DeletedAsync()).Single();
        Assert.AreEqual(("dev", newTip, "", "", "Newer dev work"), Fields(deleted));
        Assert.AreNotEqual(pushedTip, deleted.RemoteTipId);
    }

    // A local branch tracking nothing and a remote branch no local branch tracked are two branches,
    // though they have a name, so restoring the one leaves the other alone
    [TestMethod]
    public async Task TestUnrelatedBranchesOfANameAreRestoredApart()
    {
        await DevOnOriginAsync();
        await repo.GitAsync("branch --unset-upstream dev");
        await DeleteAsync("", "origin/dev");
        await DeleteAsync("dev", "");

        var deleted = await DeletedAsync();
        Assert.AreEqual(2, deleted.Count);
        var local = deleted.Single(d => d.IsLocal);
        Assert.IsFalse(local.IsRemote);

        await RestoreAsync(local, true, false);

        Assert.AreEqual("", await UpstreamConfigAsync("dev"), "It tracked nothing, and still does");
        Assert.AreEqual("", await OriginTipAsync("dev"));
        Assert.IsTrue((await DeletedAsync()).Single().IsRemote, "Origin's is still there to restore");
    }

    // A branch the repo given no longer has, since a refresh while the delete dialog was shown read
    // it as gone, is said rather than thrown on, and nothing is deleted
    [TestMethod]
    public async Task TestABranchGoneSinceItWasPickedIsNotDeleted()
    {
        await DevOnOriginAsync();

        var error = AssertError(await service.DeleteBranchAsync(await ReadAsync(), "dev", "origin/gone", true));

        Assert.AreEqual("There is no branch 'origin/gone' any more, so nothing was deleted", error.Message);
        StringAssert.Contains(await repo.GitAsync("branch --list dev"), "dev", "Not deleted");
        Assert.AreEqual(0, Records.Count);
    }

    static (string, string, string, string, string) Fields(DeletedBranch d) =>
        (d.Name, d.TipId, d.RemoteName, d.RemoteTipId, d.Subject);
}
