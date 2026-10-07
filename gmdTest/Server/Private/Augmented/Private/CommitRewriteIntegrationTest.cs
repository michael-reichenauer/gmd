using gmd.Git;
using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// Amend and Drop of an older commit over a real repository: git's own --autosquash and rebase do the
// rewrite, so what git then has is asserted, and that Undo takes each back as one change.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  ./test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class CommitRewriteIntegrationTest
{
    TempRepo repo = null!;
    readonly FakeRepoConfig config = new();
    AugmentedService service = null!;
    string c1 = "";
    string c2 = "";
    string c3 = "";

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
        c1 = await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        c2 = await repo.CommitFileAsync("b.txt", "b\n", "Second");
        c3 = await repo.CommitFileAsync("c.txt", "c\n", "Third");
    }

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    Task<Result<gmd.Server.Repo>> ReadAsync() => service.GetRepoAsync(repo.Path);

    async Task<string> LogAsync() => (await repo.GitAsync("log --format=%s")).Trim();

    async Task<UndoStep> UndoAsync()
    {
        var augmented = AssertOk(await ReadAsync());
        Assert.IsTrue(augmented.UndoSteps.TryGetValue("main", out var step), "No step for main");
        AssertOk(await service.UndoStepAsync(augmented, step));
        return step;
    }

    // A new message alone: the commit after it is replayed, nothing else changes, and Undo takes the
    // amend back whole, not just the rebase that finished it
    [TestMethod]
    public async Task TestAmendAnOlderCommitWithANewMessage()
    {
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await service.AmendOlderCommitAsync(augmented, c2, "Second, reworded\n\nWith a body", []));

        Assert.AreEqual("Third\nSecond, reworded\nInitial", await LogAsync());
        Assert.AreEqual("Second, reworded\n\nWith a body", (await repo.GitAsync("log --format=%B -1 HEAD~1")).Trim());
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));

        var undone = await UndoAsync();

        Assert.AreEqual((StepKind.Amend, "Second, reworded"), (undone.Kind, undone.Name));
        Assert.AreEqual(c3, await repo.HeadIdAsync());
    }

    // The ticked files go into the commit, and the others stay changed in the working tree
    [TestMethod]
    public async Task TestAmendAnOlderCommitWithTheTickedFiles()
    {
        File.WriteAllText(Path.Join(repo.Path, "b.txt"), "b, fixed\n");
        File.WriteAllText(Path.Join(repo.Path, "c.txt"), "c, not done\n");
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await service.AmendOlderCommitAsync(augmented, c2, "Second", ["b.txt"]));

        Assert.AreEqual("Third\nSecond\nInitial", await LogAsync());
        Assert.AreEqual("b, fixed", (await repo.GitAsync("show HEAD~1:b.txt")).Trim());
        Assert.AreEqual(" M c.txt", (await repo.GitAsync("status --porcelain")).TrimEnd());
        Assert.AreEqual("c, not done\n", File.ReadAllText(Path.Join(repo.Path, "c.txt")));
    }

    // The first commit has no parent to rebase from, so the rebase starts at the root
    [TestMethod]
    public async Task TestAmendTheFirstCommit()
    {
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await service.AmendOlderCommitAsync(augmented, c1, "The start", []));

        Assert.AreEqual("Third\nSecond\nThe start", await LogAsync());
    }

    // A drop takes the commit and its changes out of the branch, and Undo brings it back
    [TestMethod]
    public async Task TestDropACommitAndUndoIt()
    {
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await service.DropCommitAsync(augmented, c2));

        Assert.AreEqual("Third\nInitial", await LogAsync());
        Assert.IsFalse(File.Exists(Path.Join(repo.Path, "b.txt")));

        var undone = await UndoAsync();

        Assert.AreEqual((StepKind.Drop, "Second"), (undone.Kind, undone.Name));
        Assert.AreEqual(c3, await repo.HeadIdAsync());
        Assert.IsTrue(File.Exists(Path.Join(repo.Path, "b.txt")));
    }

    // Git is asked, whatever the repo read says: a commit on origin, or one another branch is on, is
    // left as it is
    [TestMethod]
    public async Task TestACommitPushedOrOnAnotherBranchIsLeftAsItIs()
    {
        await repo.GitAsync("branch other HEAD~1");
        var augmented = AssertOk(await ReadAsync());

        var otherError = AssertError(await service.DropCommitAsync(augmented, c2));

        StringAssert.Contains(otherError.AllMessages(), "'other' is on");
        Assert.AreEqual("Third\nSecond\nInitial", await LogAsync());

        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        augmented = AssertOk(await ReadAsync());

        var pushedError = AssertError(await service.AmendOlderCommitAsync(augmented, c3, "Reworded", []));

        StringAssert.Contains(pushedError.AllMessages(), "is on 'origin/main' already");
        Assert.AreEqual(c3, await repo.HeadIdAsync());
    }

    // A commit after it that cannot be replayed without it stops the rebase there, as a conflict
    [TestMethod]
    public async Task TestAConflictStopsTheRebase()
    {
        await repo.CommitFileAsync("b.txt", "b, changed\n", "Fourth");
        var augmented = AssertOk(await ReadAsync());

        var error = AssertError(await service.DropCommitAsync(augmented, c2));

        Assert.IsTrue(ConflictError.IsIn(error), error.AllMessages());
        StringAssert.Contains(await repo.GitAsync("status"), "rebase in progress");
    }
}
