using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// The branch the user is on is shown when it becomes current, e.g. checked out in a terminal, and
// only then, so that hiding it afterwards sticks
[TestClass]
public class CurrentBranchShownTest
{
    // A branch checked out outside gmd, which gmd sees when it reads the repo again
    [TestMethod]
    public async Task TestABranchThatBecameCurrentIsShown()
    {
        var shown = new CurrentBranchShown();
        shown.Shown(await OnBranch("main").ViewRepoAsync("main"));

        var read = await OnBranch("dev").ViewRepoAsync("main");

        Assert.IsFalse(read.BranchByName["dev"].IsInView, "Hidden, as the shown branches say");
        Assert.AreEqual("dev", shown.ToShow(read));
    }

    // Opening gmd, a repo or a worktree on a hidden branch
    [TestMethod]
    public async Task TestOpeningOnAHiddenBranchShowsIt()
    {
        var read = await OnBranch("dev").ViewRepoAsync("main");

        Assert.AreEqual("dev", new CurrentBranchShown().ToShow(read));
    }

    [TestMethod]
    public async Task TestAShownCurrentBranchIsNotShownAgain()
    {
        var read = await OnBranch("dev").ViewRepoAsync("main", "dev");

        Assert.AreEqual("", new CurrentBranchShown().ToShow(read));
    }

    // The user hid the branch they are on, and the next refresh leaves it hidden
    [TestMethod]
    public async Task TestACurrentBranchTheUserHidStaysHidden()
    {
        var shown = new CurrentBranchShown();
        shown.Shown(await OnBranch("dev").ViewRepoAsync("main", "dev"));
        var hidden = await OnBranch("dev").ViewRepoAsync("main");
        shown.Shown(hidden);

        Assert.AreEqual("", shown.ToShow(await OnBranch("dev").ViewRepoAsync("main")));
    }

    // A rebase, or a checkout of a commit and back, goes through a detached HEAD, and the branch it
    // comes back to has stayed current rather than become so
    [TestMethod]
    public async Task TestBackFromADetachedHeadIsNotBecomingCurrent()
    {
        var shown = new CurrentBranchShown();
        shown.Shown(await OnBranch("dev").ViewRepoAsync("main"));

        var detached = await Detached().ViewRepoAsync("main");
        Assert.AreEqual("", shown.ToShow(detached), "A detached HEAD is always shown");
        shown.Shown(detached);

        Assert.AreEqual("", shown.ToShow(await OnBranch("dev").ViewRepoAsync("main")));
    }

    // Another worktree is another repo, even with the same branch checked out
    [TestMethod]
    public async Task TestAnotherPathIsAnotherRepo()
    {
        var shown = new CurrentBranchShown();
        shown.Shown(await OnBranch("dev").AtPath("/repo").ViewRepoAsync("main"));

        var read = await OnBranch("dev").AtPath("/repo-worktree").ViewRepoAsync("main");

        Assert.AreEqual("dev", shown.ToShow(read));
    }

    // A search's results are not the branches the user has shown
    [TestMethod]
    public async Task TestASearchHasNothingToShow()
    {
        var shown = new CurrentBranchShown();
        shown.Shown(await OnBranch("main").ViewRepoAsync("main"));

        var found = await OnBranch("dev").FilteredViewRepoAsync("Initial");
        shown.Shown(found);

        Assert.AreEqual("", shown.ToShow(found));
        Assert.AreEqual("dev", shown.ToShow(await OnBranch("dev").ViewRepoAsync("main")), "Not remembered");
    }

    [TestMethod]
    public async Task TestAnEmptyRepoHasNothingToShow()
    {
        var read = await new RepoBuilder().EmptyRepo().ViewRepoAsync();

        Assert.AreEqual("", new CurrentBranchShown().ToShow(read));
    }

    static RepoBuilder OnBranch(string current) =>
        new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: current == "main")
            .LocalBranch("dev", "d1", isCurrent: current == "dev");

    static RepoBuilder Detached() =>
        new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("dev", "d1")
            .DetachedHead("c1");
}
