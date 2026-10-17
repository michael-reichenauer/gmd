using gmd.Git;
using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// Amend and Drop of an older commit, and Squash, over a real repository: git's own --autosquash and
// rebase do the rewrite, so what git then has is asserted, and that Undo takes each back as one change.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class CommitRewriteIntegrationTest
{
    TempRepo repo = null!;
    readonly FakeRepoConfig config = new();
    AugmentedService service = null!;
    UndoService undo = null!;
    CommitRewriteService rewrite = null!;
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
        undo = new UndoService(repo.Git, config);
        rewrite = new CommitRewriteService(repo.Git, config);
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
        AssertOk(await undo.UndoStepAsync(augmented, step));
        return step;
    }

    // A new message alone: the commit after it is replayed, nothing else changes, and Undo takes the
    // amend back whole, not just the rebase that finished it
    [TestMethod]
    public async Task TestAmendAnOlderCommitWithANewMessage()
    {
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await rewrite.AmendOlderCommitAsync(augmented, c2, "Second, reworded\n\nWith a body", []));

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

        AssertOk(await rewrite.AmendOlderCommitAsync(augmented, c2, "Second", ["b.txt"]));

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

        AssertOk(await rewrite.AmendOlderCommitAsync(augmented, c1, "The start", []));

        Assert.AreEqual("Third\nSecond\nThe start", await LogAsync());
    }

    // A drop takes the commit and its changes out of the branch, and Undo brings it back
    [TestMethod]
    public async Task TestDropACommitAndUndoIt()
    {
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await rewrite.DropCommitAsync(augmented, c2));

        Assert.AreEqual("Third\nInitial", await LogAsync());
        Assert.IsFalse(File.Exists(Path.Join(repo.Path, "b.txt")));

        var undone = await UndoAsync();

        Assert.AreEqual((StepKind.Drop, "Second"), (undone.Kind, undone.Name));
        Assert.AreEqual(c3, await repo.HeadIdAsync());
        Assert.IsTrue(File.Exists(Path.Join(repo.Path, "b.txt")));
    }

    // Git is asked, whatever the repo read says: a commit another branch is on, here or on origin, is
    // left as it is. One on origin's copy of the branch is not (ForcePushIntegrationTest).
    [TestMethod]
    public async Task TestACommitOnAnotherBranchHereOrOnOriginIsLeftAsItIs()
    {
        await repo.GitAsync("branch other HEAD~1");
        var augmented = AssertOk(await ReadAsync());

        var otherError = AssertError(await rewrite.DropCommitAsync(augmented, c2));

        StringAssert.Contains(otherError.AllMessages(), "'other' is on");
        Assert.AreEqual("Third\nSecond\nInitial", await LogAsync());

        await repo.GitAsync("branch -D other");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.GitAsync("push origin main:release");
        augmented = AssertOk(await ReadAsync());

        var pushedError = AssertError(await rewrite.AmendOlderCommitAsync(augmented, c3, "Reworded", []));

        StringAssert.Contains(pushedError.AllMessages(), "is on 'origin/release' already");
        Assert.AreEqual(c3, await repo.HeadIdAsync());
    }

    // A commit after it that cannot be replayed without it stops the rebase there, as a conflict
    [TestMethod]
    public async Task TestAConflictStopsTheRebase()
    {
        await repo.CommitFileAsync("b.txt", "b, changed\n", "Fourth");
        var augmented = AssertOk(await ReadAsync());

        var error = AssertError(await rewrite.DropCommitAsync(augmented, c2));

        Assert.IsTrue(ConflictError.IsIn(error), error.AllMessages());
        StringAssert.Contains(await repo.GitAsync("status"), "rebase in progress");
    }

    // A merge among the commits on top of the squashed ones is kept: they were cherry-picked back one
    // by one, and cherry-pick refused the merge, which stopped the squash with the branch already reset
    [TestMethod]
    public async Task TestSquashKeepsAMergeOnTop()
    {
        await repo.GitAsync("checkout -q -b side");
        await repo.CommitFileAsync("s.txt", "s\n", "Side work");
        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAsync("d.txt", "d\n", "On top");
        await repo.GitAsync("merge --no-ff -q -m \"Merge side\" side");
        var tip = await repo.HeadIdAsync();
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await rewrite.SquashCommitsAsync(augmented, c3, c2, "Squashed"));

        Assert.AreEqual(
            "Merge side\nOn top\nSquashed\nInitial",
            (await repo.GitAsync("log --format=%s --first-parent")).Trim()
        );
        Assert.AreEqual(3, (await repo.GitAsync("rev-list --parents -1 HEAD")).Trim().Split(' ').Length, "A merge");
        Assert.AreEqual("", await repo.GitAsync($"diff {tip} HEAD"), "The same files");
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
    }

    // The squash is the files of the newest commit on the parent of the oldest, with the message
    // given, and the commits on top keep their files and authors
    [TestMethod]
    public async Task TestSquashKeepsTheFilesAndTheCommitsOnTop()
    {
        repo.WriteFile("d.txt", "d\n");
        await repo.GitAsync("add d.txt");
        await repo.GitAsync("commit -q --author=\"Ann Other <ann@example.com>\" -m \"On top\"");
        var tip = await repo.HeadIdAsync();
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await rewrite.SquashCommitsAsync(augmented, c3, c2, "Squashed\n\nSecond and third"));

        Assert.AreEqual("On top\nSquashed\nInitial", await LogAsync());
        Assert.AreEqual("Squashed\n\nSecond and third", (await repo.GitAsync("log --format=%B -1 HEAD~1")).Trim());
        Assert.AreEqual("", await repo.GitAsync($"diff {c3} HEAD~1"), "The squash has the third commit's files");
        Assert.AreEqual("", await repo.GitAsync($"diff {tip} HEAD"));
        Assert.AreEqual("Ann Other", (await repo.GitAsync("log --format=%an -1")).Trim());
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
    }

    // With commit.gpgsign set, git signs what commit, rebase and cherry-pick make, but the squash is
    // made by commit-tree, which does not read it, so it was the one commit left unsigned, and a
    // branch that requires signed commits refused it on the push
    [TestMethod]
    public async Task TestSquashIsSignedWhenTheConfigSignsCommits()
    {
        await repo.UseSshSigningKeyAsync();
        await repo.GitAsync("config commit.gpgsign true");
        await repo.CommitFileAsync("d.txt", "d\n", "On top");
        var augmented = AssertOk(await ReadAsync());

        AssertOk(await rewrite.SquashCommitsAsync(augmented, c3, c2, "Squashed"));

        Assert.AreEqual("On top\nSquashed\nInitial", await LogAsync());
        StringAssert.Contains(await repo.GitAsync("cat-file -p HEAD~1"), "BEGIN SSH SIGNATURE", "The squash");
        StringAssert.Contains(await repo.GitAsync("cat-file -p HEAD"), "BEGIN SSH SIGNATURE", "The commit on top");
    }

    // A commit moved down, past the one before it, is the two swapped, and Undo takes it back whole;
    // moved up is the same swap, seen from the other commit
    [TestMethod]
    public async Task TestMoveACommitAndUndoIt()
    {
        AssertOk(await rewrite.MoveCommitAsync(AssertOk(await ReadAsync()), c3, isUp: false));

        Assert.AreEqual("Second\nThird\nInitial", await LogAsync());
        Assert.AreEqual("", await repo.GitAsync("status --porcelain"));
        var undone = await UndoAsync();
        Assert.AreEqual((StepKind.Move, "Third"), (undone.Kind, undone.Name));
        Assert.AreEqual(c3, await repo.HeadIdAsync());

        var second = (await repo.GitAsync("rev-parse HEAD~1")).Trim();
        AssertOk(await rewrite.MoveCommitAsync(AssertOk(await ReadAsync()), second, isUp: true));
        Assert.AreEqual("Second\nThird\nInitial", await LogAsync());
    }

    // Two commits that change the same lines conflict once swapped, which stops the rebase there
    [TestMethod]
    public async Task TestAMoveThatConflictsStops()
    {
        var fourth = await repo.CommitFileAsync("c.txt", "c, changed\n", "Fourth");

        var error = AssertError(await rewrite.MoveCommitAsync(AssertOk(await ReadAsync()), fourth, isUp: false));

        Assert.IsTrue(ConflictError.IsIn(error), error.AllMessages());
        StringAssert.Contains(await repo.GitAsync("status"), "rebase in progress");
    }
}
