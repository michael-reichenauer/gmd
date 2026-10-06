using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// Undo over a real repository: the change is made with git, read back as the branch's last change,
// and undone through the augmented service, and what git then has is asserted. The point is that
// nothing uncommitted is lost, whichever way a change is taken back.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  ./test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class UndoIntegrationTest
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

    // The current branch's last change, as the repo read now has it
    async Task<(Repo, UndoStep)> LastStepAsync(string branch = "main")
    {
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));
        Assert.IsTrue(augmented.UndoSteps.TryGetValue(branch, out var step), $"No step for {branch}");
        return (augmented, step);
    }

    async Task<UndoStep> UndoAsync(string branch = "main")
    {
        var (augmented, step) = await LastStepAsync(branch);
        AssertOk(await service.UndoStepAsync(augmented, step));
        return step;
    }

    string Read(string name) => File.ReadAllText(Path.Join(repo.Path, name));

    // A commit undone comes back as uncommitted changes, and Undo again, now a redo, commits them back
    [TestMethod]
    public async Task TestUndoACommitAndRedoIt()
    {
        var c1 = await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        var c2 = await repo.CommitFileAsync("a.txt", "two\n", "Second");

        var undone = await UndoAsync();

        Assert.AreEqual(
            (StepKind.Commit, "Second", c1, false),
            (undone.Kind, undone.Name, undone.TargetId, undone.IsRedo)
        );
        Assert.AreEqual(c1, await repo.HeadIdAsync());
        Assert.AreEqual("two\n", Read("a.txt"));
        Assert.AreEqual(" M a.txt", (await repo.GitAsync("status --porcelain")).TrimEnd());

        var redone = await UndoAsync();

        Assert.AreEqual(
            (StepKind.Commit, "Second", c2, true),
            (redone.Kind, redone.Name, redone.TargetId, redone.IsRedo)
        );
        Assert.AreEqual(c2, await repo.HeadIdAsync());
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));

        // And it is the commit's undo again, not the undo of a reset
        var (_, again) = await LastStepAsync();
        Assert.AreEqual((StepKind.Commit, "Second", c1, false), (again.Kind, again.Name, again.TargetId, again.IsRedo));
    }

    // The commit before the amend is back, and what the amend added is uncommitted
    [TestMethod]
    public async Task TestUndoAnAmend()
    {
        await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        var c2 = await repo.CommitFileAsync("a.txt", "two\n", "Second");
        repo.WriteFile("a.txt", "three\n");
        await repo.GitAsync("commit -a --amend -m \"Second amended\"");

        var undone = await UndoAsync();

        Assert.AreEqual((StepKind.Amend, "Second amended"), (undone.Kind, undone.Name));
        Assert.AreEqual(c2, await repo.HeadIdAsync());
        Assert.AreEqual("three\n", Read("a.txt"));
    }

    // The branch and its files are back as they were before the rebase
    [TestMethod]
    public async Task TestUndoARebase()
    {
        await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        await repo.GitAsync("checkout -b feature");
        var f1 = await repo.CommitFileAsync("f.txt", "f\n", "Feature");
        await repo.GitAsync("checkout main");
        await repo.CommitFileAsync("m.txt", "m\n", "Main");
        await repo.GitAsync("checkout feature");
        await repo.GitAsync("rebase main");

        var undone = await UndoAsync("feature");

        Assert.AreEqual(StepKind.Rebase, undone.Kind);
        Assert.AreEqual(f1, await repo.HeadIdAsync());
        Assert.IsFalse(File.Exists(Path.Join(repo.Path, "m.txt")));
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
    }

    // A 'reset --hard' made outside gmd threw the files away with the commits; with a clean tree
    // the undo brings both back
    [TestMethod]
    public async Task TestUndoAResetHardBringsTheFilesBack()
    {
        var c1 = await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        var c2 = await repo.CommitFileAsync("b.txt", "b\n", "Second");
        await repo.GitAsync($"reset --hard {c1}");

        var undone = await UndoAsync();

        Assert.AreEqual(StepKind.Reset, undone.Kind);
        Assert.AreEqual(c2, await repo.HeadIdAsync());
        Assert.AreEqual("b\n", Read("b.txt"));
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
    }

    // gmd's Uncommit left the commit's changes uncommitted, and more were made after it: the undo
    // puts the commit back and leaves every file as it is, so nothing made since is lost
    [TestMethod]
    public async Task TestUndoAnUncommitLosesNothingMadeSince()
    {
        await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        var c2 = await repo.CommitFileAsync("a.txt", "two\n", "Second");
        AssertOk(await repo.Git.UncommitLastCommitAsync(repo.Path));
        repo.WriteFile("b.txt", "made since\n");

        var undone = await UndoAsync();

        Assert.AreEqual(StepKind.Uncommit, undone.Kind);
        Assert.AreEqual(c2, await repo.HeadIdAsync());
        Assert.AreEqual("two\n", Read("a.txt"));
        Assert.AreEqual("made since\n", Read("b.txt"));
        Assert.AreEqual("?? b.txt", (await repo.GitAsync("status --porcelain")).TrimEnd());
    }
}
