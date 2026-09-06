using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Server;

[TestClass]
public class RepoExtensionsTest
{
    // 'dev' is merged into main, 'feat' is not; 'feat' is checked out in another worktree
    static Task<Repo> Fixture() =>
        new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d1")
            .Commit("f1", "Feature work", "c1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .LocalBranch("feat", "f1")
            .Worktree("/test/repo-feat", "feat", changes: 1)
            .AugmentedRepoAsync();

    // The rule Delete Branch has always used: a branch whose tip is on it and has no children
    // would lose that commit, a merged branch's tip has the merge as a child
    [TestMethod]
    public async Task TestHasUnmergedCommits()
    {
        var repo = await Fixture();

        Assert.IsTrue(repo.HasUnmergedCommits(repo.BranchByName["feat"]));
        Assert.IsFalse(repo.HasUnmergedCommits(repo.BranchByName["dev"]));
        Assert.IsFalse(repo.HasUnmergedCommits(repo.BranchByName["origin/dev"]));
    }

    // A tip that is not in the log (an old branch in a truncated log) is taken as unmerged
    [TestMethod]
    public async Task TestATipNotInTheLogIsUnmerged()
    {
        var repo = await Fixture();
        var branch = repo.BranchByName["dev"] with { TipId = RepoBuilder.Sha("aa") };

        Assert.IsTrue(repo.HasUnmergedCommits(branch));
    }

    // A remote branch answers for its local branch, since that is the one a worktree holds
    [TestMethod]
    public async Task TestWorktreePathOfResolvesARemoteToItsLocalBranch()
    {
        var repo = await Fixture();

        Assert.AreEqual("/test/repo-feat", repo.WorktreePathOf(repo.BranchByName["feat"]));
        Assert.AreEqual("", repo.WorktreePathOf(repo.BranchByName["main"]));
        Assert.AreEqual("", repo.WorktreePathOf(repo.BranchByName["origin/main"]));
        Assert.AreEqual("", repo.WorktreePathOf(repo.BranchByName["origin/dev"]));
    }

    [TestMethod]
    public async Task TestOtherWorktreesLeavesOutTheCurrentOne()
    {
        var repo = await Fixture();

        Assert.AreEqual(2, repo.Worktrees.Count);
        Assert.AreEqual("/test/repo-feat", string.Join(", ", repo.OtherWorktrees().Select(w => w.Path)));
    }
}
