using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// A force push on origin, as a real repository sees it: a second clone rewrites a branch and force
// pushes it, and this one fetches. Every commit is made at a minute of its own, since a copy of a
// commit is told by its author time.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  ./test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class ForcePushIntegrationTest
{
    TempRepo repo = null!;
    AugmentedService service = null!;
    string other = "";
    int minute = 0;

    [TestInitialize]
    public async Task Init()
    {
        repo = await TempRepo.CreateAsync();
        service = RepoBuilder.NewAugmentedService(repo.Git, new FakeMetaDataService(new MetaData()));
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

        AssertOk(await service.PullRewrittenAsync(augmented, augmented.RemoteRewrites["dev"]));

        Assert.AreEqual("Own work\nKept\nTheirs\nInitial", (await repo.GitAsync("log --format=%s dev")).Trim());
        Assert.AreEqual(newTip, (await repo.GitAsync("rev-parse dev~1")).Trim());
        var pulled = AssertOk(await service.GetRepoAsync(repo.Path));
        Assert.AreEqual(0, pulled.RemoteRewrites.Count);
        Assert.AreEqual((StepKind.Pull, own), (pulled.UndoSteps["dev"].Kind, pulled.UndoSteps["dev"].TargetId));
    }

    // With no commits of its own, a branch that is not checked out just takes the new version
    [TestMethod]
    public async Task TestPullTakesTheNewVersionOfABranchNotCheckedOut()
    {
        var (_, _, _, newTip) = await ForcePushedAsync(hasOwnCommit: false);
        await repo.GitAsync("checkout main");
        var augmented = AssertOk(await service.GetRepoAsync(repo.Path));

        AssertOk(await service.PullRewrittenAsync(augmented, augmented.RemoteRewrites["dev"]));

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

        AssertError(await service.PullRewrittenAsync(augmented, augmented.RemoteRewrites["dev"]));

        Assert.AreEqual(own, (await repo.GitAsync("rev-parse dev")).Trim());
    }
}
