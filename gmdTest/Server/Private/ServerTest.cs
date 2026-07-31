using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Server;

// Choosing which branches are shown is what gmd is for, and these are the two commands the branch
// menu uses for it. Both work on an existing view repo and return a new one, i.e. showing a branch
// is 'the branches shown so far, plus this one'.
[TestClass]
public class ServerTest
{
    static string[] BranchNames(Repo repo) => repo.ViewBranches.Select(b => b.Name).ToArray();

    // Showing a branch keeps the ones already shown
    [TestMethod]
    public async Task TestShowBranchAddsToTheBranchesAlreadyShown()
    {
        var b = ThreeBranches();
        var server = b.NewServer();
        var repo = await b.ViewRepoAsync();
        CollectionAssert.AreEqual(new[] { "origin/main", "main" }, BranchNames(repo));

        repo = server.ShowBranch(repo, "dev", includeAmbiguous: false);
        CollectionAssert.AreEqual(new[] { "origin/main", "main", "origin/dev", "dev" }, BranchNames(repo));

        repo = server.ShowBranch(repo, "feat", includeAmbiguous: false);
        CollectionAssert.AreEqual(new[] { "origin/main", "main", "origin/dev", "dev", "feat" }, BranchNames(repo));
    }

    // Hiding a branch leaves the other shown branches alone
    [TestMethod]
    public async Task TestHideBranchRemovesOnlyThatBranch()
    {
        var b = ThreeBranches();
        var server = b.NewServer();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);

        repo = server.HideBranch(repo, "dev");

        CollectionAssert.AreEqual(new[] { "origin/main", "main", "feat" }, BranchNames(repo));
    }

    // Hiding a branch hides the branches that hang off it too, since they would otherwise have
    // nothing left to be drawn relative to
    [TestMethod]
    public async Task TestHideBranchAlsoHidesItsDescendants()
    {
        var b = new RepoBuilder()
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("dev", "d1")
            .LocalBranch("feat", "f1");
        var server = b.NewServer();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);
        CollectionAssert.AreEqual(new[] { "origin/main", "main", "dev", "feat" }, BranchNames(repo));

        repo = server.HideBranch(repo, "dev");

        CollectionAssert.AreEqual(new[] { "origin/main", "main" }, BranchNames(repo), "feat hangs off dev");
    }

    // 'Hide all branches' goes back to just the main branch
    [TestMethod]
    public async Task TestHideAllBranchesLeavesTheMainBranch()
    {
        var b = ThreeBranches();
        var server = b.NewServer();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);

        repo = server.HideBranch(repo, "dev", hideAllBranches: true);

        CollectionAssert.AreEqual(new[] { "origin/main", "main" }, BranchNames(repo));
    }

    // A commit two branches could equally own is ambiguous, and the user resolves it by looking at
    // the candidates. Showing the branch with 'include ambiguous' brings all of them into the view
    // so they can be compared.
    [TestMethod]
    public async Task TestShowBranchCanIncludeTheAmbiguousBranches()
    {
        var b = new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1");
        var server = b.NewServer();
        var repo = await b.ViewRepoAsync();

        CollectionAssert.AreEqual(
            new[] { "feat-a", "feat-b" },
            repo.BranchByName["feat-a"].AmbiguousBranchNames.ToArray()
        );

        var shown = server.ShowBranch(repo, "feat-a", includeAmbiguous: false);
        CollectionAssert.AreEqual(new[] { "origin/main", "main", "feat-a" }, BranchNames(shown));

        var withAmbiguous = server.ShowBranch(repo, "feat-a", includeAmbiguous: true);
        CollectionAssert.AreEqual(new[] { "origin/main", "main", "feat-a", "feat-b" }, BranchNames(withAmbiguous));
    }

    static RepoBuilder ThreeBranches() =>
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
