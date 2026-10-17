using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// A force push on origin, as a real repository sees it: a second clone rewrites a branch and force
// pushes it, and this one fetches. Every commit is made at a minute of its own, since a copy of a
// commit is told by its author time.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class ForcePushIntegrationTest
{
    TempRepo repo = null!;
    readonly FakeRepoConfig config = new();
    AugmentedService service = null!;
    UndoService undo = null!;
    string other = "";
    int minute = 0;

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
    }

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    DateTimeOffset NextTime() => TempRepo.BaseTime.AddMinutes(minute++);

    Task<string> CommitAsync(string name, string message) =>
        repo.CommitFileAtAsync(name, $"{message}\n", message, NextTime());

    // A commit in the other clone, at its own minute
    async Task<string> CommitInOtherAsync(string name, string message)
    {
        File.WriteAllText(Path.Join(other, name), $"{message}\n");
        await repo.GitAsync($"-C \"{other}\" add .");
        repo.GitAt(["-C", other, "commit", "-qm", message], NextTime());
        return (await repo.GitAsync($"-C \"{other}\" rev-parse HEAD")).Trim();
    }

    // dev on origin is Initial <- Kept <- Dropped, and here it has a commit of its own on top. The
    // other clone drops 'Dropped', copies 'Kept' onto a new commit of its own and force pushes, and
    // this one fetches.
    async Task<(string Kept, string Dropped, string Own, string NewTip)> ForcePushedAsync(bool hasOwnCommit = true)
    {
        await CommitAsync("a.txt", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.GitAsync("checkout -b dev");
        var kept = await CommitAsync("k.txt", "Kept");
        var dropped = await CommitAsync("d.txt", "Dropped");
        await repo.GitAsync("push -u origin dev");
        var own = hasOwnCommit ? await CommitAsync("o.txt", "Own work") : dropped;

        other = repo.Path + "-other";
        repo.TrackFolder(other);
        await repo.GitAsync($"clone -q -b main \"{repo.Path}-origin\" \"{other}\"");
        await repo.GitAsync($"-C \"{other}\" config user.name \"{TempRepo.AuthorName}\"");
        await repo.GitAsync($"-C \"{other}\" config user.email {TempRepo.AuthorEmail}");
        await repo.GitAsync($"-C \"{other}\" checkout -q dev");
        await repo.GitAsync($"-C \"{other}\" reset -q --hard main");
        await CommitInOtherAsync("t.txt", "Theirs");
        await repo.GitAsync($"-C \"{other}\" cherry-pick {kept}");
        var newTip = (await repo.GitAsync($"-C \"{other}\" rev-parse HEAD")).Trim();
        await repo.GitAsync($"-C \"{other}\" push -q --force origin dev");

        await repo.GitAsync("fetch origin");
        return (kept, dropped, own, newTip);
    }

    [TestMethod]
    public async Task TestAForcePushOnOriginIsFound()
    {
        var (_, dropped, _, newTip) = await ForcePushedAsync();

        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        var rewrite = augmented.RemoteRewrites["dev"];
        Assert.AreEqual(
            (dropped, dropped, newTip, 1, 2, false, true),
            (
                rewrite.ForkPointId,
                rewrite.OldTipId,
                rewrite.NewTipId,
                rewrite.OwnCount,
                rewrite.OldCopyCount,
                rewrite.IsByYou,
                rewrite.IsRestorable
            )
        );
        CollectionAssert.AreEqual(new[] { dropped }, rewrite.DroppedIds.ToArray());
    }

    // New commits on both sides, made without a force push, are no rewrite
    [TestMethod]
    public async Task TestNewCommitsOnBothSidesAreNot()
    {
        await CommitAsync("a.txt", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        other = repo.Path + "-other";
        repo.TrackFolder(other);
        await repo.GitAsync($"clone -q -b main \"{repo.Path}-origin\" \"{other}\"");
        await repo.GitAsync($"-C \"{other}\" config user.name \"{TempRepo.AuthorName}\"");
        await repo.GitAsync($"-C \"{other}\" config user.email {TempRepo.AuthorEmail}");
        await CommitInOtherAsync("t.txt", "Theirs");
        await repo.GitAsync($"-C \"{other}\" push -q origin main");
        await CommitAsync("o.txt", "Own work");
        await repo.GitAsync("fetch origin");

        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        Assert.AreEqual(0, augmented.RemoteRewrites.Count);
    }

    // gmd's Rebase force pushes, so once the rebase is undone here origin still has it: a rewrite
    // made from here, which origin can be put back from
    [TestMethod]
    public async Task TestARebasePushedAndUndoneHereIsARewriteFromHere()
    {
        await CommitAsync("a.txt", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.GitAsync("checkout -b dev");
        var feature = await CommitAsync("f.txt", "Feature");
        await repo.GitAsync("push -u origin dev");
        await repo.GitAsync("checkout main");
        await CommitAsync("m.txt", "Main moved");
        await repo.GitAsync("checkout dev");
        await repo.GitAsync("rebase main");
        await repo.GitAsync("push --force");
        await repo.GitAsync($"reset --keep {feature}");

        var rewrite = AssertOk(await service.GetRepoAsync(repo.Path)).RemoteRewrites["dev"];

        Assert.AreEqual(
            (feature, true, true, 0, 0),
            (rewrite.OldTipId, rewrite.IsByYou, rewrite.IsRestorable, rewrite.OwnCount, rewrite.DroppedIds.Count)
        );
    }

    // The pull moves the commit of its own onto the new version and leaves the old one out, rather
    // than merging the two, and is named a pull by Undo
    [TestMethod]
    public async Task TestPullMovesTheOwnCommitsOntoTheNewVersion()
    {
        var (_, _, own, newTip) = await ForcePushedAsync();
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        AssertOk(await undo.PullRewrittenAsync(augmented, augmented.RemoteRewrites["dev"]));

        Assert.AreEqual("Own work\nKept\nTheirs\nInitial", (await repo.GitAsync("log --format=%s dev")).Trim());
        Assert.AreEqual(newTip, (await repo.GitAsync("rev-parse dev~1")).Trim());
        var pulled = AssertOk(await service.GetRepoAsync(repo.Path));
        Assert.AreEqual(0, pulled.RemoteRewrites.Count);
        Assert.AreEqual((StepKind.Pull, own), (pulled.UndoSteps["dev"].Kind, pulled.UndoSteps["dev"].TargetId));
    }

    // The other clone only drops 'Dropped', resetting dev back to 'Kept' and force pushing: dev here is
    // then just ahead, with nothing to pull, which a plain push would take and put 'Dropped' back with.
    // Found all the same, and pulled by moving the commit of its own onto 'Kept'.
    [TestMethod]
    public async Task TestAForcePushThatOnlyDroppedCommitsIsFoundAndPulled()
    {
        await CommitAsync("a.txt", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.GitAsync("checkout -b dev");
        var kept = await CommitAsync("k.txt", "Kept");
        var dropped = await CommitAsync("d.txt", "Dropped");
        await repo.GitAsync("push -u origin dev");
        await CommitAsync("o.txt", "Own work");
        other = repo.Path + "-other";
        repo.TrackFolder(other);
        await repo.GitAsync($"clone -q -b dev \"{repo.Path}-origin\" \"{other}\"");
        await repo.GitAsync($"-C \"{other}\" reset -q --hard {kept}");
        await repo.GitAsync($"-C \"{other}\" push -q --force origin dev");
        await repo.GitAsync("fetch origin");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));
        var rewrite = augmented.RemoteRewrites["dev"];

        AssertOk(await undo.PullRewrittenAsync(augmented, rewrite));

        Assert.AreEqual(
            (dropped, kept, 1, 0, false, true),
            (
                rewrite.ForkPointId,
                rewrite.NewTipId,
                rewrite.OwnCount,
                rewrite.NewCount,
                rewrite.IsByYou,
                rewrite.IsRestorable
            )
        );
        CollectionAssert.AreEqual(new[] { dropped }, rewrite.DroppedIds.ToArray());
        Assert.AreEqual("Own work\nKept\nInitial", (await repo.GitAsync("log --format=%s dev")).Trim());
        Assert.AreEqual(0, AssertOk(await service.GetRepoAsync(repo.Path)).RemoteRewrites.Count);
    }

    // A branch merged in is not moved with the own commits: its commits keep their ids, and the merge
    // is made again on the new version
    [TestMethod]
    public async Task TestPullLeavesTheCommitsOfABranchMergedInAsTheyAre()
    {
        var (_, _, _, newTip) = await ForcePushedAsync();
        await repo.GitAsync("checkout main");
        var mainWork = await CommitAsync("m.txt", "Main work");
        await repo.GitAsync("checkout dev");
        await repo.GitAsync("merge --no-ff -m \"Merge main\" main");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));
        var rewrite = augmented.RemoteRewrites["dev"];

        AssertOk(await undo.PullRewrittenAsync(augmented, rewrite));

        Assert.AreEqual(2, rewrite.OwnCount);
        Assert.AreEqual(mainWork, (await repo.GitAsync("rev-parse dev^2")).Trim());
        Assert.AreEqual(newTip, (await repo.GitAsync("rev-parse dev~2")).Trim());
    }

    // With no commits of its own, a branch that is not checked out just takes the new version
    [TestMethod]
    public async Task TestPullTakesTheNewVersionOfABranchNotCheckedOut()
    {
        var (_, _, _, newTip) = await ForcePushedAsync(hasOwnCommit: false);
        await repo.GitAsync("checkout main");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        AssertOk(await undo.PullRewrittenAsync(augmented, augmented.RemoteRewrites["dev"]));

        Assert.AreEqual(newTip, (await repo.GitAsync("rev-parse dev")).Trim());
        Assert.AreEqual(StepKind.Pull, AssertOk(await service.GetRepoAsync(repo.Path)).UndoSteps["dev"].Kind);
    }

    // A branch that is not checked out cannot have its own commits moved onto the new version
    [TestMethod]
    public async Task TestABranchNotCheckedOutWithCommitsOfItsOwnIsNotPulled()
    {
        var (_, _, own, _) = await ForcePushedAsync();
        await repo.GitAsync("checkout main");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        AssertError(await undo.PullRewrittenAsync(augmented, augmented.RemoteRewrites["dev"]));

        Assert.AreEqual(own, (await repo.GitAsync("rev-parse dev")).Trim());
    }

    // Origin is put back as it was before the force push, and the other clone, which made it, then
    // sees its own branch rewritten in turn
    [TestMethod]
    public async Task TestRestoreOriginPutsTheOldVersionBack()
    {
        var (_, dropped, _, _) = await ForcePushedAsync();
        var rewrite = AssertOk(await service.GetRepoAsync(repo.Path)).RemoteRewrites["dev"];

        AssertOk(await repo.Git.PushRestoreAsync(rewrite.RemoteName, rewrite.OldTipId, rewrite.NewTipId, repo.Path));

        Assert.AreEqual(dropped, (await repo.GitAsync($"-C \"{repo.Path}-origin\" rev-parse dev")).Trim());
        Assert.AreEqual(0, AssertOk(await service.GetRepoAsync(repo.Path)).RemoteRewrites.Count);
        await repo.GitAsync($"-C \"{other}\" fetch -q origin");
        var seenThere = AssertOk(await service.GetRepoAsync(other)).RemoteRewrites["dev"];
        Assert.AreEqual((dropped, false), (seenThere.NewTipId, seenThere.IsByYou));
    }

    // Someone pushed since the last fetch: origin is not where it was read, so it is left alone
    [TestMethod]
    public async Task TestRestoreOriginIsRefusedWhenOriginMovedSince()
    {
        await ForcePushedAsync();
        var rewrite = AssertOk(await service.GetRepoAsync(repo.Path)).RemoteRewrites["dev"];
        var later = await CommitInOtherAsync("l.txt", "Later");
        await repo.GitAsync($"-C \"{other}\" push -q origin dev");

        AssertError(await repo.Git.PushRestoreAsync(rewrite.RemoteName, rewrite.OldTipId, rewrite.NewTipId, repo.Path));

        Assert.AreEqual(later, (await repo.GitAsync($"-C \"{repo.Path}-origin\" rev-parse dev")).Trim());
    }

    // A branch rewritten here once it was pushed, the mirror of the above: dev on origin is Initial <-
    // First <- Second, which is pushed, and then rewritten here, while origin keeps it
    async Task<(string First, string Second)> PushedDevAsync()
    {
        await CommitAsync("a.txt", "Initial");
        await repo.AddOriginAsync();
        await repo.GitAsync("push -u origin main");
        await repo.GitAsync("checkout -b dev");
        var first = await CommitAsync("f.txt", "First");
        var second = await CommitAsync("s.txt", "Second");
        await repo.GitAsync("push -u origin dev");
        return (first, second);
    }

    // gmd's squash of commits on origin, named as gmd recorded it: origin has the old version, and the
    // force push replaces it, leased on origin's tip as it was read, after which nothing is told
    [TestMethod]
    public async Task TestASquashOfPushedCommitsIsARewriteHere()
    {
        var (first, second) = await PushedDevAsync();
        var read = AssertOk(await service.GetRepoAsync(repo.Path));

        AssertOk(await new CommitRewriteService(repo.Git, config).SquashCommitsAsync(read, second, first, "Both"));

        var rewrite = AssertOk(await service.GetRepoAsync(repo.Path)).LocalRewrites["dev"];
        Assert.AreEqual(
            (StepKind.Squash, "Both", second, 1),
            (rewrite.Kind, rewrite.Name, rewrite.OldTipId, rewrite.NewCount)
        );
        CollectionAssert.AreEqual(new[] { second, first }, rewrite.OldIds.ToArray());

        AssertOk(await repo.Git.PushForceAsync("dev", "origin/dev", rewrite.OldTipId, repo.Path));

        Assert.AreEqual(
            await repo.HeadIdAsync(),
            (await repo.GitAsync($"-C \"{repo.Path}-origin\" rev-parse dev")).Trim()
        );
        Assert.AreEqual(0, AssertOk(await service.GetRepoAsync(repo.Path)).LocalRewrites.Count);
    }

    // The same made outside gmd, by the reflog's own words, as an agent's amend of a pushed commit
    [TestMethod]
    public async Task TestAnAmendOfAPushedCommitIsARewriteHere()
    {
        var (_, second) = await PushedDevAsync();

        repo.GitAt(["commit", "-q", "--amend", "-m", "Second, reworded"], NextTime());

        var rewrite = AssertOk(await service.GetRepoAsync(repo.Path)).LocalRewrites["dev"];
        Assert.AreEqual((StepKind.Amend, "Second, reworded", second), (rewrite.Kind, rewrite.Name, rewrite.OldTipId));
    }

    // A rebase that stopped on a conflict, and was continued: the rebase itself, as git's reflog of the
    // branch has it once it finishes, which Rebase and Push onto leaves to be pushed, since its push
    // never came
    [TestMethod]
    public async Task TestARebaseContinuedAfterAConflictIsARewriteHere()
    {
        await PushedDevAsync();
        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAtAsync("s.txt", "main's\n", "Main's", NextTime());
        await repo.GitAsync("checkout -q dev");

        AssertError(await repo.Git.RebaseOntoAsync("main", "main~1", repo.Path));
        File.WriteAllText(Path.Join(repo.Path, "s.txt"), "both\n");
        await repo.GitAsync("add s.txt");
        AssertOk(await repo.Git.ContinueOperationAsync(repo.Path));

        var rewrite = AssertOk(await service.GetRepoAsync(repo.Path)).LocalRewrites["dev"];
        Assert.AreEqual((StepKind.Rebase, 2), (rewrite.Kind, rewrite.OldIds.Count));
    }
}
