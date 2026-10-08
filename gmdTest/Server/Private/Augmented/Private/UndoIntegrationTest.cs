using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// Undo over a real repository: the change is made with git, read back as the branch's last change,
// and undone through the augmented service, and what git then has is asserted. The point is that
// nothing uncommitted is lost, whichever way a change is taken back.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
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

    // Merging the current branch into another (Shift-E) merges into that one and switches back, so
    // the merge is the last change of a branch that is not checked out: that branch is moved back,
    // and nothing else is touched
    [TestMethod]
    public async Task TestUndoAMergeIntoABranchNotCheckedOut()
    {
        var c1 = await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        await repo.GitAsync("checkout -b feature");
        var f1 = await repo.CommitFileAsync("f.txt", "f\n", "Feature");
        await repo.GitAsync("checkout main");
        await repo.GitAsync("merge --no-ff -m \"Merge branch 'feature'\" feature");
        await repo.GitAsync("checkout feature");

        var undone = await UndoAsync("main");

        Assert.AreEqual((StepKind.Merge, "feature"), (undone.Kind, undone.Name));
        Assert.AreEqual(c1, (await repo.GitAsync("rev-parse main")).Trim());
        Assert.AreEqual(f1, await repo.HeadIdAsync());
        Assert.AreEqual("feature", (await repo.GitAsync("branch --show-current")).Trim());
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));

        var (_, redo) = await LastStepAsync("main");
        Assert.AreEqual((StepKind.Merge, "feature", true), (redo.Kind, redo.Name, redo.IsRedo));
    }

    // gmd pulls a branch that is not checked out with 'fetch origin x:x', which moves it in place
    [TestMethod]
    public async Task TestUndoThePullOfABranchNotCheckedOut()
    {
        await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.GitAsync("checkout -b other");
        var o1 = await repo.CommitFileAsync("o.txt", "one\n", "Other one");
        var o2 = await repo.CommitFileAsync("o.txt", "two\n", "Other two");
        await repo.GitAsync("push -u origin other");
        await repo.GitAsync("checkout main");
        await repo.GitAsync($"branch -f other {o1}");
        await repo.GitAsync("fetch origin other:other");
        Assert.AreEqual(o2, (await repo.GitAsync("rev-parse other")).Trim());

        var undone = await UndoAsync("other");

        Assert.AreEqual(StepKind.Pull, undone.Kind);
        Assert.AreEqual(o1, (await repo.GitAsync("rev-parse other")).Trim());
    }

    // The branch is moved only from where the step found it: one moved since is left where it is
    [TestMethod]
    public async Task TestABranchMovedSinceItWasReadIsLeftWhereItIs()
    {
        var c1 = await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        await repo.GitAsync("branch other");
        await repo.GitAsync("checkout other");
        await repo.CommitFileAsync("o.txt", "one\n", "Other one");
        await repo.GitAsync("checkout main");
        var (augmented, step) = await LastStepAsync("other");
        await repo.GitAsync($"branch -f other {c1}");

        var error = AssertError(await service.UndoStepAsync(augmented, step));

        StringAssert.Contains(error.Message, "'other' has moved since it was read");
        Assert.AreEqual(c1, (await repo.GitAsync("rev-parse other")).Trim());
    }

    // The same for the branch checked out, which is reset rather than moved: a commit made since the
    // repo was read, e.g. in a terminal, would otherwise be taken back with the one the step names
    [TestMethod]
    public async Task TestTheCurrentBranchMovedSinceItWasReadIsLeftWhereItIs()
    {
        await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        await repo.CommitFileAsync("a.txt", "two\n", "Second");
        var (augmented, step) = await LastStepAsync();
        var c3 = await repo.CommitFileAsync("b.txt", "three\n", "Third");

        var error = AssertError(await service.UndoStepAsync(augmented, step));

        StringAssert.Contains(error.Message, "'main' has moved since it was read");
        Assert.AreEqual(c3, await repo.HeadIdAsync());
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
    }

    // A branch checked out in another worktree is moved only there, since that worktree's files
    // would otherwise no longer match its branch
    [TestMethod]
    public async Task TestNotABranchCheckedOutInAnotherWorktree()
    {
        await repo.CommitFileAsync("a.txt", "one\n", "Initial");
        var path = await repo.AddWorktreeAsync("dev");
        File.WriteAllText(Path.Join(path, "d.txt"), "d\n");
        await repo.GitAsync($"-C \"{path}\" add .");
        await repo.GitAsync($"-C \"{path}\" commit -m Dev");
        var (augmented, step) = await LastStepAsync("dev");

        AssertError(await service.UndoStepAsync(augmented, step));
    }

    // A squash is several moves of the branch, which gmd records as one change, so a single undo
    // puts back the commits it squashed and the newer ones it picked back on top
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task TestUndoASquashAsOneStep(bool hasNewerCommits)
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        var older = await repo.CommitFileAsync("b.txt", "b\n", "Older");
        var newer = await repo.CommitFileAsync("c.txt", "c\n", "Newer");
        var tip = hasNewerCommits ? await repo.CommitFileAsync("d.txt", "d\n", "On top") : newer;
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));
        AssertOk(await service.SquashCommits(augmented, newer, older, "Squashed"));
        Assert.AreEqual(hasNewerCommits ? "On top" : "Squashed", (await repo.GitAsync("log --format=%s -1")).Trim());

        var undone = await UndoAsync();

        Assert.AreEqual((StepKind.Squash, "Squashed", tip), (undone.Kind, undone.Name, undone.TargetId));
        Assert.AreEqual(tip, await repo.HeadIdAsync());
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
    }

    // Squashing pushed commits while the branch has a commit of its own on top: the commit on top
    // stays a commit of its own, on top of the squash. The commits to pick back used to be walked from
    // the remote branch's tip, so the local one was squashed in with the others.
    [TestMethod]
    public async Task TestSquashPushedCommitsKeepsTheLocalCommitOnTop()
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        var older = await repo.CommitFileAsync("b.txt", "b\n", "Older");
        var newer = await repo.CommitFileAsync("c.txt", "c\n", "Newer");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.CommitFileAsync("d.txt", "d\n", "Local on top");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        AssertOk(await service.SquashCommits(augmented, newer, older, "Squashed"));

        Assert.AreEqual("Local on top\nSquashed\nInitial", (await repo.GitAsync("log --format=%s")).Trim());
    }

    // A squash of commits only origin has, the local branch having diverged from it, is refused
    // before anything is changed: it used to reset the local branch onto them
    [TestMethod]
    public async Task TestSquashOfCommitsNotOnTheLocalBranchIsRefused()
    {
        await repo.CommitFileAsync("a.txt", "a\n", "Initial");
        var older = await repo.CommitFileAsync("b.txt", "b\n", "Older");
        var newer = await repo.CommitFileAsync("c.txt", "c\n", "Newer");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.GitAsync("reset -q --hard HEAD~2");
        var local = await repo.CommitFileAsync("d.txt", "d\n", "Local");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        var error = AssertError(await service.SquashCommits(augmented, newer, older, "Squashed"));

        StringAssert.Contains(error.Message, "Only commits on the current branch can be squashed");
        Assert.AreEqual(local, await repo.HeadIdAsync());
        Assert.AreEqual("", await repo.GitAsync("branch --list squash-backup-*"), "No backup branch is left");
    }
}
