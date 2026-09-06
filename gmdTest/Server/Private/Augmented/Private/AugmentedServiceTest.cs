using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;
using GitWorktree = gmd.Git.Worktree;

namespace gmdTest.Server.Private.Augmented.Private;

// The worktree side of the augmented service over a fake git: re-reading the worktrees of a
// repo the pipeline already built, and the writes. The other worktrees' folders have to exist,
// since a status is only read for a folder that does, so they are temp folders.
[TestClass]
public class AugmentedServiceTest
{
    string root = "";
    string Main => Path.Join(root, "main");
    string Dev => Path.Join(root, "main-dev");

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-augmented-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Main);
        Directory.CreateDirectory(Dev);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    static T Value<T>(R<T> result)
    {
        Assert.IsTrue(Try(out var value, out var e, result), $"{e}");
        return value;
    }

    GitWorktree MainWorktree() =>
        new GitWorktree(Main, RepoBuilder.Sha("c1"), "main", true, false, false, false, "", false, "");

    GitWorktree DevWorktree(bool isLocked = false) =>
        new GitWorktree(Dev, RepoBuilder.Sha("d1"), "dev", false, false, false, isLocked, "", false, "");

    // The list and the changes of the others are read again; which worktree is current, and the
    // branches and commits, are what they were
    [TestMethod]
    public async Task TestUpdatedWorktreesReReadTheListAndTheChangesOfTheOthers()
    {
        var repo = await new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("main", "c1", isCurrent: true)
            .LocalBranch("dev", "d1")
            .AtPath(Main)
            .Worktree(Dev, "dev", changes: 0)
            .ViewRepoAsync();
        Assert.AreEqual(0, repo.Worktrees[1].ChangesCount);

        // Meanwhile the worktree got locked and two files were changed in it
        var git = new FakeGit(RepoBuilder.NoChanges);
        git.Worktrees.AddRange([MainWorktree(), DevWorktree(isLocked: true)]);
        git.StatusByPath[Dev] = RepoBuilder.NoChanges with { Modified = 2 };
        var service = RepoBuilder.NewAugmentedService(git, new FakeMetaDataService(new MetaData()));

        var updated = Value(await service.GetUpdatedWorktreesRepoAsync(repo));

        Assert.AreEqual(2, updated.Worktrees.Count);
        Assert.IsTrue(updated.Worktrees[0].IsCurrent);
        Assert.AreEqual(0, updated.Worktrees[0].ChangesCount);
        Assert.IsFalse(updated.Worktrees[1].IsCurrent);
        Assert.AreEqual(2, updated.Worktrees[1].ChangesCount);
        Assert.IsTrue(updated.Worktrees[1].IsLocked);
        Assert.AreSame(repo.AllBranches, updated.AllBranches, "Nothing but the worktrees is touched");
        Assert.AreSame(repo.ViewCommits, updated.ViewCommits);
    }

    // A worktree that appeared since is listed, and one that went is gone; only the status of the
    // others is read, the current worktree's changes are the repo's own status
    [TestMethod]
    public async Task TestUpdatedWorktreesFollowTheList()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", "Initial")
            .LocalBranch("main", "c1", isCurrent: true)
            .LocalBranch("dev", "c1")
            .AtPath(Main)
            .WithStatus(modified: 1)
            .ViewRepoAsync();
        Assert.AreEqual(0, repo.Worktrees.Count, "None declared");

        var git = new FakeGit(RepoBuilder.NoChanges with { Modified = 1 });
        git.Worktrees.AddRange([MainWorktree(), DevWorktree()]);
        git.StatusByPath[Dev] = RepoBuilder.NoChanges;
        var service = RepoBuilder.NewAugmentedService(git, new FakeMetaDataService(new MetaData()));

        var updated = Value(await service.GetUpdatedWorktreesRepoAsync(repo));

        Assert.AreEqual(
            "main:1, dev:0",
            string.Join(", ", updated.Worktrees.Select(w => $"{w.Branch}:{w.ChangesCount}"))
        );
        Assert.IsTrue(updated.Worktrees[0].IsCurrent, "Found by the repo's path when none was current before");
    }

    [TestMethod]
    public async Task TestWorktreeWritesGoToGitWithTheFileMonitorPaused()
    {
        var git = new FakeGit();
        var monitor = new FakeFileMonitor();
        var service = RepoBuilder.NewAugmentedService(git, new FakeMetaDataService(new MetaData()), monitor);

        Assert.IsTrue(Try(out var e, await service.AddWorktreeAsync(Dev, "dev", true, "main", Main)), $"{e}");
        Assert.IsTrue(Try(out e, await service.AddWorktreeAsync(Dev, "dev", false, "", Main)), $"{e}");
        Assert.IsTrue(Try(out e, await service.RemoveWorktreeAsync(Dev, true, Main)), $"{e}");
        Assert.IsTrue(Try(out e, await service.PruneWorktreesAsync(Main)), $"{e}");

        CollectionAssert.AreEqual(
            new[] { $"add {Dev} dev new main", $"add {Dev} dev existing", $"remove {Dev} --force", "prune" },
            git.WorktreeCalls.ToArray()
        );
        Assert.AreEqual(4, monitor.PauseCount, "Each write holds the monitor, as the other writes do");
    }
}
