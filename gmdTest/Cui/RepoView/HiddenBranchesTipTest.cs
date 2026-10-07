using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What the first repository with hidden branches says on the status line: what is shown, how many
// branches are hidden, and how to show one
[TestClass]
public class HiddenBranchesTipTest
{
    // main and a dev merged into it, with dev hidden, which is how a repository opens: the current
    // branch and main. A branch and its remote branch are one branch.
    [TestMethod]
    public async Task TestTheHiddenBranchesAreCounted()
    {
        var repo = await BranchAndMerge().ViewRepoAsync();

        Assert.AreEqual(
            "Showing main; 1 other branch is hidden: ⇧→ shows one, as does a click on a dark ╮ or ╯",
            HiddenBranchesTip.Of(repo)
        );
    }

    [TestMethod]
    public async Task TestTheShownBranchesAreNamedAFew()
    {
        var repo = await ThreeBranches().ViewRepoAsync("feat");

        Assert.AreEqual(
            "Showing main and feat; 2 other branches are hidden: ⇧→ shows one, as does a click on a dark ╮ or ╯",
            HiddenBranchesTip.Of(repo)
        );
    }

    // Nothing is hidden, so nothing is told, which leaves it for a repository that has hidden branches
    [TestMethod]
    public async Task TestNothingHiddenIsNoTip()
    {
        var repo = await BranchAndMerge().ViewRepoAsync(ShowBranches.AllActive);

        Assert.IsNull(HiddenBranchesTip.Of(repo));
    }

    static RepoBuilder BranchAndMerge() =>
        new RepoBuilder()
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d2")
            .Commit("d2", "Dev work 2", "d1")
            .Commit("d1", "Dev work 1", "c2")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true)
            .BranchWithRemote("dev", "d2");

    // main with three branches made from it: dev and feat merged back, and fix, not yet
    static RepoBuilder ThreeBranches() =>
        new RepoBuilder()
            .Commit("c5", "Merge branch 'feat' into main", "c4", "f1")
            .Commit("x1", "Fix", "c4")
            .Commit("f1", "Feat work", "c2")
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d1")
            .Commit("d1", "Dev work", "c2")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c5", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .BranchWithRemote("feat", "f1")
            .LocalBranch("fix", "x1");
}
