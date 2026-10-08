using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// The lost lines of work of a real repository, as the reflogs git wrote leave them. Every commit is
// made at a minute of its own, since an older version of a commit is told by its author time.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class LostWorkIntegrationTest
{
    TempRepo repo = null!;
    AugmentedService service = null!;
    int minute = 0;

    [TestInitialize]
    public async Task Init()
    {
        repo = await TempRepo.CreateAsync();
        service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
    }

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    Task<string> CommitAsync(string name, string message) =>
        repo.CommitFileAtAsync(name, $"{message}\n", message, TempRepo.BaseTime.AddMinutes(minute++));

    async Task<IReadOnlyList<LostWork>> LostWorkAsync()
    {
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));
        return AssertOk(await service.GetLostWorkAsync(augmented));
    }

    [TestMethod]
    public async Task TestNothingIsLost()
    {
        await CommitAsync("a.txt", "Initial");
        await CommitAsync("b.txt", "Second");

        Assert.AreEqual(0, (await LostWorkAsync()).Count);
    }

    [TestMethod]
    public async Task TestTheCommitsAResetLeftBehind()
    {
        await CommitAsync("a.txt", "Initial");
        var second = await CommitAsync("b.txt", "Second");
        var third = await CommitAsync("c.txt", "Third");
        await repo.GitAsync("reset --hard HEAD~2");

        var works = await LostWorkAsync();

        Assert.AreEqual(1, works.Count, string.Join("\n", works));
        Assert.AreEqual(
            (third, "main", LostBy.Reset, false),
            (works[0].TipId, works[0].BranchName, works[0].LostBy, works[0].IsRewritten)
        );
        CollectionAssert.AreEqual(new[] { third, second }, works[0].CommitIds.ToArray());
    }

    [TestMethod]
    public async Task TestTheWorkOfADeletedBranch()
    {
        await CommitAsync("a.txt", "Initial");
        await repo.GitAsync("checkout -b feature");
        var work = await CommitAsync("f.txt", "Feature work");
        await repo.GitAsync("checkout main");
        await repo.GitAsync("branch -D feature");

        var works = await LostWorkAsync();

        Assert.AreEqual(1, works.Count, string.Join("\n", works));
        Assert.AreEqual((work, "feature", LostBy.Deleted), (works[0].TipId, works[0].BranchName, works[0].LostBy));
    }

    [TestMethod]
    public async Task TestAnAmendLeavesAnOlderVersion()
    {
        await CommitAsync("a.txt", "Initial");
        var second = await CommitAsync("b.txt", "Second");
        await repo.GitAsync("commit --amend -m \"Second, reworded\"");

        var works = await LostWorkAsync();

        Assert.AreEqual(1, works.Count, string.Join("\n", works));
        Assert.AreEqual((second, LostBy.Amend, true), (works[0].TipId, works[0].LostBy, works[0].IsRewritten));
    }

    [TestMethod]
    public async Task TestTheWorkLeftOnADetachedHead()
    {
        await CommitAsync("a.txt", "Initial");
        await repo.GitAsync("checkout --detach");
        var work = await CommitAsync("d.txt", "Detached work");
        await repo.GitAsync("checkout main");

        var works = await LostWorkAsync();

        Assert.AreEqual(1, works.Count, string.Join("\n", works));
        Assert.AreEqual((work, "", LostBy.Detached), (works[0].TipId, works[0].BranchName, works[0].LostBy));
    }

    // A stash keeps what it was made on, even the older ones only the stash's own reflog holds
    [TestMethod]
    public async Task TestWhatAStashKeepsIsNotLost()
    {
        await CommitAsync("a.txt", "Initial");
        await repo.GitAsync("checkout -b feature");
        await CommitAsync("f.txt", "Feature work");
        repo.WriteFile("f.txt", "changed\n");
        await repo.GitAsync("stash");
        repo.WriteFile("f.txt", "changed again\n");
        await repo.GitAsync("stash");
        await repo.GitAsync("checkout main");
        await repo.GitAsync("branch -D feature");

        Assert.AreEqual(0, (await LostWorkAsync()).Count);
    }

    // A branch made at a lost commit brings it back, though the repo it was picked from has no
    // such commit to say which branch the new one branches out of
    [TestMethod]
    public async Task TestABranchAtALostCommitBringsItBack()
    {
        await CommitAsync("a.txt", "Initial");
        var lost = await CommitAsync("b.txt", "Second");
        await repo.GitAsync("reset --hard HEAD~1");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        AssertOk(await service.CreateBranchFromCommitAsync(augmented, "restored", lost, false, repo.Path));

        Assert.AreEqual(lost, (await repo.GitAsync("rev-parse restored")).Trim());
        Assert.AreEqual(0, (await LostWorkAsync()).Count);
    }
}
