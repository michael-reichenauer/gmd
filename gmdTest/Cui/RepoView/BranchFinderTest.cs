using gmd.Cui.RepoView;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// Which branches the Find Branch dialog lists for what has been typed, and in what order
[TestClass]
public class BranchFinderTest
{
    // With nothing typed, every branch: the ones that exist, newest first, then the deleted ones
    [TestMethod]
    public async Task TestNothingTypedListsEveryBranchNewestFirst()
    {
        var repo = await Fixture().ViewRepoAsync();

        Assert.AreEqual(
            """
            feature/logout
            bugfix/login-crash
            main
            feature/login
            dev
            ~feature/old
            """,
            Found(repo, "")
        );
    }

    // A name where what was typed starts a part of it comes first, even before a branch that still
    // exists: 'o' is how 'feature/old' starts after the '/', which is how names are remembered
    [TestMethod]
    public async Task TestAMatchAtTheStartOfAPartOfTheNameComesFirst()
    {
        var repo = await Fixture().ViewRepoAsync();

        Assert.AreEqual(
            """
            ~feature/old
            feature/logout
            bugfix/login-crash
            feature/login
            """,
            Found(repo, "o")
        );
    }

    // Every word has to be in the name, in any order, and case does not matter
    [TestMethod]
    public async Task TestEveryWordHasToMatch()
    {
        var repo = await Fixture().ViewRepoAsync();

        Assert.AreEqual("feature/logout\nfeature/login", Found(repo, "feat log"));
        Assert.AreEqual("feature/logout\nfeature/login", Found(repo, "LOG FEAT"));
    }

    [TestMethod]
    public async Task TestNoMatchIsNoBranch()
    {
        var repo = await Fixture().ViewRepoAsync();

        Assert.AreEqual("", Found(repo, "xyz"));
    }

    static string Found(gmd.Server.Repo repo, string text) =>
        string.Join("\n", BranchFinder.Find(repo, text).Select(b => (b.IsGitBranch ? "" : "~") + b.NiceNameUnique));

    // Newest first: 'feature/logout' has the newest commit and 'feature/old' was deleted, gmd knows
    // it only from the message of the merge that brought it into main
    static RepoBuilder Fixture() =>
        new RepoBuilder()
            .Commit("a1", "Logout", "c1")
            .Commit("b1", "Crash fix", "c1")
            .Commit("m1", "Merge branch 'feature/old' into main", "c2", "o1")
            .Commit("o1", "Old work", "c1")
            .Commit("l1", "Login", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "m1", isCurrent: true)
            .LocalBranch("feature/logout", "a1")
            .LocalBranch("bugfix/login-crash", "b1")
            .LocalBranch("feature/login", "l1")
            .LocalBranch("dev", "c2");
}
