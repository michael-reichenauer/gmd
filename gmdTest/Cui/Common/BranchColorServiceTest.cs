using gmd.Cui.Common;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui;

// A branch keeps the same color for as long as it exists, since the color is derived from the
// branch name rather than from its position in the graph. These pin down the derived colors, so a
// change to the hashing shows up as a test failure rather than as every branch in every repo
// silently changing color.
[TestClass]
public class BranchColorServiceTest
{
    // The main branch is always magenta, which is why magenta is not one of the colors a branch
    // can be given
    [TestMethod]
    public async Task TestMainBranchIsAlwaysMagenta()
    {
        var b = Repo();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);
        var colors = new BranchColorService(b.Config);

        Assert.AreEqual(Color.Magenta, colors.GetColor(repo, repo.BranchByName["origin/main"]));
        Assert.AreEqual(Color.Magenta, colors.GetColor(repo, repo.BranchByName["main"]));

        // Even a color the user set for main is ignored
        b.Config.Set(repo.Path, c => c.BranchColors["origin/main"] = 0);
        Assert.AreEqual(Color.Magenta, colors.GetColor(repo, repo.BranchByName["origin/main"]));
    }

    // The color comes from the primary branch name, so a local branch and its remote branch are
    // the same branch to the user and get the same color
    [TestMethod]
    public async Task TestLocalAndRemoteBranchShareTheColorOfTheirPrimaryName()
    {
        var b = Repo();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);
        var colors = new BranchColorService(b.Config);

        Assert.AreEqual(Color.Red, colors.GetColor(repo, repo.BranchByName["origin/dev"]));
        Assert.AreEqual(Color.Red, colors.GetColor(repo, repo.BranchByName["dev"]));
        Assert.AreEqual(Color.Green, colors.GetColor(repo, repo.BranchByName["feat"]));
    }

    // A color the user picked is stored per repo and wins over the derived one
    [TestMethod]
    public async Task TestUserSetColorIsUsed()
    {
        var b = Repo();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);
        var colors = new BranchColorService(b.Config);

        b.Config.Set(repo.Path, c => c.BranchColors["feat"] = 4);

        Assert.AreEqual(Color.Yellow, colors.GetColor(repo, repo.BranchByName["feat"]));
    }

    // Changing the color steps to the next one, which is what the 'Change color' menu item does
    [TestMethod]
    public async Task TestChangeColorStepsToTheNextColor()
    {
        var b = Repo();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);
        var colors = new BranchColorService(b.Config);

        colors.ChangeColor(repo, repo.BranchByName["feat"]); // Green
        Assert.AreEqual(Color.Cyan, colors.GetColor(repo, repo.BranchByName["feat"]));

        colors.ChangeColor(repo, repo.BranchByName["feat"]);
        Assert.AreEqual(Color.Red, colors.GetColor(repo, repo.BranchByName["feat"]));
    }

    // A child branch that happens to hash to the same color as its parent is stepped one color, so
    // the two are still told apart in the graph. 'dev' and 'feat' both hash to green.
    [TestMethod]
    public async Task TestChildBranchWithTheSameColorAsItsParentIsStepped()
    {
        var b = new RepoBuilder()
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("dev", "d1")
            .LocalBranch("feat", "f1");
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);
        var colors = new BranchColorService(b.Config);

        Assert.AreEqual("dev", repo.BranchByName["feat"].ParentBranchName);
        Assert.AreEqual(Color.Green, colors.GetColor(repo, repo.BranchByName["dev"]));
        Assert.AreEqual(Color.Cyan, colors.GetColor(repo, repo.BranchByName["feat"]));
    }

    // A detached HEAD is not a branch the user chose, so it is white rather than colored
    [TestMethod]
    public async Task TestDetachedHeadIsWhite()
    {
        var b = new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .DetachedHead("c1");
        var repo = await b.ViewRepoAsync();
        var colors = new BranchColorService(b.Config);

        Assert.AreEqual(Color.White, colors.GetColor(repo, repo.BranchByName["DETACHED"]));
    }

    static RepoBuilder Repo() =>
        new RepoBuilder()
            .Commit("c5", "Merge branch 'feat' into main", "c4", "f1")
            .Commit("f1", "Feature work", "c2")
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d1")
            .Commit("d1", "Dev work", "c2")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c5", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .LocalBranch("feat", "f1");
}
