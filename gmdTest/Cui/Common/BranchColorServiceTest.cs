using gmd.Cui.Common;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

// A branch keeps the same color for as long as it exists: it is picked the first time gmd sees the
// branch and remembered (see BranchColorPickerTest), so nothing done around the branch changes it.
// These pin down the picked colors as well, so a change to the hashing shows up as a test failure
// rather than as every new branch in every repo silently getting another color.
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
        Assert.AreEqual(3, b.Config.Get(repo.Path).BranchColors["feat"]);
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

    // A branch that is no longer a git branch is deleted, i.e. not active, and is gray. The color
    // is not derived from the name and a color the user set for it is ignored, as for main.
    [TestMethod]
    public async Task TestDeletedBranchIsGray()
    {
        var b = new RepoBuilder()
            .Commit("c3", "Merge branch 'feat' into main", "c2", "f1")
            .Commit("f1", "Feature work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true); // 'feat' is not a branch anymore
        var repo = await b.ViewRepoAsync(ShowBranches.AllActiveAndDeleted);
        var colors = new BranchColorService(b.Config);

        var feat = repo.ViewBranches.First(bb => !bb.IsGitBranch);
        Assert.AreEqual("feat", feat.NiceName);
        Assert.AreEqual(Color.Dark, colors.GetColor(repo, feat));

        b.Config.Set(repo.Path, c => c.BranchColors[feat.PrimaryName] = 4);
        Assert.AreEqual(Color.Dark, colors.GetColor(repo, feat));
    }

    // The colors of the live branches are remembered, by their names with no 'origin/', main's not
    [TestMethod]
    public async Task TestRememberKeepsTheColorOfEveryLiveBranch()
    {
        var b = Repo();
        var repo = await b.ViewRepoAsync();
        var colors = new BranchColorService(b.Config);

        colors.Remember(repo);

        CollectionAssert.AreEquivalent(
            new Dictionary<string, int> { ["dev"] = 3, ["feat"] = 1 },
            b.Config.Get(repo.Path).BranchColors
        );
    }

    // Showing and hiding branches changes no branch's color, whichever is shown first
    [TestMethod]
    public async Task TestColorsStayWhenBranchesAreShownOrHidden()
    {
        var b = Repo();
        var colors = new BranchColorService(b.Config);
        var repos = new[]
        {
            await b.ViewRepoAsync("feat"),
            await b.ViewRepoAsync(),
            await b.ViewRepoAsync(ShowBranches.AllActive),
            await b.ViewRepoAsync("dev"),
            await b.ViewRepoAsync(ShowBranches.AllActiveAndDeleted),
        };

        foreach (var repo in repos)
        {
            colors.Remember(repo);
            Assert.AreEqual(Color.Red, colors.GetColor(repo, repo.BranchByName["origin/dev"]));
            Assert.AreEqual(Color.Red, colors.GetColor(repo, repo.BranchByName["dev"]));
            Assert.AreEqual(Color.Green, colors.GetColor(repo, repo.BranchByName["feat"]));
        }
    }

    // A branch keeps its color when a sibling it was picked against is gone, e.g. squash merged and
    // deleted, which takes it out of the history: 'origin/api' and 'origin/feat' both hash to red
    [TestMethod]
    public async Task TestBranchKeepsItsColorWhenTheSiblingItAvoidedIsGone()
    {
        var b = new RepoBuilder()
            .Commit("f1", "Feature work", "c1")
            .Commit("a1", "Api work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .BranchWithRemote("api", "a1")
            .BranchWithRemote("feat", "f1");
        var repo = await b.ViewRepoAsync();
        var colors = new BranchColorService(b.Config);
        colors.Remember(repo);
        Assert.AreEqual(Color.Red, colors.GetColor(repo, repo.BranchByName["origin/api"]));
        Assert.AreEqual(Color.Yellow, colors.GetColor(repo, repo.BranchByName["origin/feat"]));

        var later = new RepoBuilder()
            .Commit("f1", "Feature work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .BranchWithRemote("feat", "f1");
        var laterRepo = await later.AtPath(repo.Path).ViewRepoAsync();
        colors = new BranchColorService(b.Config); // As gmd started again
        colors.Remember(laterRepo);

        Assert.AreEqual(Color.Yellow, colors.GetColor(laterRepo, laterRepo.BranchByName["origin/feat"]));
        CollectionAssert.AreEquivalent(
            new Dictionary<string, int> { ["feat"] = 4 },
            b.Config.Get(repo.Path).BranchColors
        );
    }

    // Pushing a branch keeps its color, though the remote branch's name hashes to another: 'fix'
    // hashes to cyan, 'origin/fix' to blue
    [TestMethod]
    public async Task TestPushedBranchKeepsItsColor()
    {
        var b = new RepoBuilder()
            .Commit("f1", "Fix", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("fix", "f1", isCurrent: true);
        var repo = await b.ViewRepoAsync();
        var colors = new BranchColorService(b.Config);
        colors.Remember(repo);
        Assert.AreEqual(Color.Cyan, colors.GetColor(repo, repo.BranchByName["fix"]));

        var pushed = await new RepoBuilder()
            .Commit("f1", "Fix", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .BranchWithRemote("fix", "f1", isCurrent: true)
            .AtPath(repo.Path)
            .ViewRepoAsync();
        colors.Remember(pushed);

        Assert.AreEqual(Color.Cyan, colors.GetColor(pushed, pushed.BranchByName["origin/fix"]));
        Assert.AreEqual(Color.Cyan, colors.GetColor(pushed, pushed.BranchByName["fix"]));
        Assert.AreEqual(
            Color.Blue,
            new BranchColorService(new FakeRepoConfig()).GetColor(pushed, pushed.BranchByName["fix"])
        );
    }

    // The color of a deleted branch is forgotten, so the remembered colors are of the live branches
    // only, a color the user set included
    [TestMethod]
    public async Task TestColorOfDeletedBranchIsForgotten()
    {
        var b = new RepoBuilder()
            .Commit("f1", "Feature work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat", "f1");
        var repo = await b.ViewRepoAsync();
        var colors = new BranchColorService(b.Config);
        colors.ChangeColor(repo, repo.BranchByName["feat"]);
        colors.Remember(repo);
        Assert.IsTrue(b.Config.Get(repo.Path).BranchColors.ContainsKey("feat"));

        var merged = await new RepoBuilder()
            .Commit("c2", "Merge branch 'feat' into main", "c1", "f1")
            .Commit("f1", "Feature work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true) // 'feat' is not a branch anymore
            .AtPath(repo.Path)
            .ViewRepoAsync(ShowBranches.AllActiveAndDeleted);
        colors.Remember(merged);

        Assert.AreEqual(0, b.Config.Get(repo.Path).BranchColors.Count);
    }

    // A search's results hold only the branches of the commits found, so remembering them forgets
    // nothing
    [TestMethod]
    public async Task TestSearchResultsForgetNoColor()
    {
        var b = Repo();
        var repo = await b.ViewRepoAsync();
        var colors = new BranchColorService(b.Config);
        colors.Remember(repo);

        colors.Remember(await b.FilteredViewRepoAsync("Feature"));
        colors.Remember(await b.FilteredViewRepoAsync("no such commit"));

        Assert.AreEqual(2, b.Config.Get(repo.Path).BranchColors.Count);
    }

    // A color an older gmd remembered for a pushed branch, keyed by the remote branch's name, is
    // kept by the branch's name from then on
    [TestMethod]
    public async Task TestColorKeyedByTheRemoteBranchNameIsKeptByTheBranchName()
    {
        var b = Repo();
        var repo = await b.ViewRepoAsync();
        b.Config.Set(repo.Path, c => c.BranchColors["origin/dev"] = 0);
        var colors = new BranchColorService(b.Config);

        colors.Remember(repo);

        Assert.AreEqual(Color.Blue, colors.GetColor(repo, repo.BranchByName["dev"]));
        CollectionAssert.AreEquivalent(
            new Dictionary<string, int> { ["dev"] = 0, ["feat"] = 1 },
            b.Config.Get(repo.Path).BranchColors
        );
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
