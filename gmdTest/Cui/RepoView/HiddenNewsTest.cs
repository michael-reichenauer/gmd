using gmd.Cui.RepoView;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What is new on the hidden branches: their own commits above the tip they had when last shown
[TestClass]
public class HiddenNewsTest
{
    static string Sha(string name) => RepoBuilder.Sha(name);

    // The first time nothing is remembered, and every tip is taken as seen, rather than every
    // branch the repository ever had being news
    [TestMethod]
    public async Task TestTheFirstTimeNothingIsNew()
    {
        var repo = await Fixture().ViewRepoAsync();

        var seen = HiddenNews.Seen(repo, new Dictionary<string, string>());

        Assert.AreEqual(Sha("f3"), seen["origin/feature"]);
        Assert.AreEqual(0, HiddenNews.Of(repo, seen).Count);
    }

    [TestMethod]
    public async Task TestTheCommitsAboveTheSeenTipAreNew()
    {
        var repo = await Fixture().ViewRepoAsync();

        var news = HiddenNews.Of(repo, Seen(("origin/main", "c1"), ("origin/feature", "f1")));

        Assert.AreEqual("origin/feature 2", Text(news));
    }

    // Showing a branch is seeing it: its tip is remembered, and it is news no more
    [TestMethod]
    public async Task TestAShownBranchIsSeen()
    {
        var repo = await Fixture().ViewRepoAsync("feature");

        var seen = HiddenNews.Seen(repo, Seen(("origin/main", "c1"), ("origin/feature", "f1")));

        Assert.AreEqual(Sha("f3"), seen["origin/feature"]);
        Assert.AreEqual(0, HiddenNews.Of(repo, seen).Count);
    }

    // A branch pushed since, which was never seen, is new with all of its own commits, and none of
    // the main branch's it was made from
    [TestMethod]
    public async Task TestANewBranchIsNewWithAllItsOwnCommits()
    {
        var repo = await Fixture().ViewRepoAsync();

        var news = HiddenNews.Of(repo, Seen(("origin/main", "c1")));

        Assert.AreEqual("origin/feature 3", Text(news));
    }

    // Merging main into the branch is one new commit, the merge; what it brought in is main's
    [TestMethod]
    public async Task TestAMergeIntoTheBranchIsOneCommit()
    {
        var repo = await new RepoBuilder()
            .Commit("f2", "Merge branch 'main' into feature", "f1", "c3")
            .Commit("c3", "Main 3", "c2")
            .Commit("c2", "Main 2", "c1")
            .Commit("f1", "Feature 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .BranchWithRemote("feature", "f2")
            .ViewRepoAsync();

        var news = HiddenNews.Of(repo, Seen(("origin/main", "c3"), ("origin/feature", "f1")));

        Assert.AreEqual("origin/feature 1", Text(news));
    }

    // A branch that is gone is forgotten, so that one made again with the same name is new
    [TestMethod]
    public async Task TestAGoneBranchIsForgotten()
    {
        var repo = await Fixture().ViewRepoAsync();

        var seen = HiddenNews.Seen(repo, Seen(("origin/main", "c1"), ("origin/old", "c1")));

        Assert.IsFalse(seen.ContainsKey("origin/old"));
    }

    static Dictionary<string, string> Seen(params (string Branch, string Commit)[] tips) =>
        tips.ToDictionary(t => t.Branch, t => Sha(t.Commit));

    static string Text(IReadOnlyList<HiddenBranchNews> news) =>
        string.Join(", ", news.Select(n => $"{n.Branch.Name} {n.Count}"));

    // 'main', and 'feature' with three commits of its own, both pushed
    static RepoBuilder Fixture() =>
        new RepoBuilder()
            .Commit("f3", "Feature 3", "f2")
            .Commit("f2", "Feature 2", "f1")
            .Commit("f1", "Feature 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .BranchWithRemote("feature", "f3");
}
