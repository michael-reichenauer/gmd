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

        Assert.AreEqual("origin/feature new 3", Text(news));
    }

    // A new branch is news even with no commits of its own yet, e.g. one made for a release
    [TestMethod]
    public async Task TestANewBranchWithNoCommitsOfItsOwnIsNew()
    {
        var repo = await Fixture().RemoteBranch("origin/release", "c1").ViewRepoAsync();

        var news = HiddenNews.Of(repo, Seen(("origin/main", "c1"), ("origin/feature", "f3")));

        Assert.AreEqual("origin/release new 0", Text(news));
    }

    // A branch never pushed, e.g. one Claude Code made in a worktree of its own, is news as a remote
    // branch is
    [TestMethod]
    public async Task TestALocalBranchNeverPushedIsNew()
    {
        var repo = await Fixture().Commit("x1", "Agent work", "c1").LocalBranch("agent", "x1").ViewRepoAsync();

        var news = HiddenNews.Of(repo, Seen(("origin/main", "c1"), ("origin/feature", "f1")));

        Assert.AreEqual("origin/feature 2, agent new 1", Text(news));
    }

    // A local branch with a remote branch is told of by the remote one, rather than twice
    [TestMethod]
    public async Task TestABranchWithARemoteIsToldOfOnce()
    {
        var repo = await Fixture().ViewRepoAsync();

        var news = HiddenNews.Of(repo, Seen(("origin/main", "c1"), ("origin/feature", "f1")));

        Assert.AreEqual("origin/feature 2", Text(news));
    }

    // The branch the user is on is their own work, also when they hid it, and also with uncommitted
    // changes, which make the uncommitted row its tip in the log but are no new tip
    [TestMethod]
    public async Task TestTheCurrentBranchIsNeverNew()
    {
        var repo = await new RepoBuilder()
            .Commit("x1", "Own work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .LocalBranch("own", "x1", isCurrent: true)
            .WithStatus(modified: 1)
            .ViewRepoAsync("main");
        Assert.IsFalse(repo.BranchByName["own"].IsInView, "Hidden");

        var seen = HiddenNews.Seen(repo, Seen(("origin/main", "c1")));

        Assert.AreEqual(Sha("x1"), seen["own"]);
        Assert.AreEqual("", Text(HiddenNews.Of(repo, Seen(("origin/main", "c1")))));
    }

    [TestMethod]
    public async Task TestADetachedHeadIsNoBranchToTellOf()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .DetachedHead("c1")
            .ViewRepoAsync("main");

        var seen = HiddenNews.Seen(repo, Seen(("origin/main", "c2")));

        Assert.IsFalse(seen.ContainsKey("DETACHED"));
        Assert.AreEqual("", Text(HiddenNews.Of(repo, seen)));
    }

    // A local branch the user has seen and then pushed from a terminal has a remote branch it did
    // not have, which is new by name only: it is seen as far as the local one was
    [TestMethod]
    public async Task TestAPushedBranchIsSeenAsFarAsItWas()
    {
        var repo = await Fixture().Commit("x1", "Own work", "c1").BranchWithRemote("own", "x1").ViewRepoAsync();

        var seen = HiddenNews.Seen(repo, Seen(("origin/main", "c1"), ("origin/feature", "f3"), ("own", "x1")));

        Assert.AreEqual(Sha("x1"), seen["origin/own"]);
        Assert.AreEqual("", Text(HiddenNews.Of(repo, seen)));
    }

    // A local branch whose remote branch was deleted, e.g. once its pull request was merged, is
    // what is left of a branch the user knew, and its own commits are the user's
    [TestMethod]
    public async Task TestABranchLeftWhenItsRemoteWasDeletedIsNotNew()
    {
        var repo = await Fixture()
            .Commit("x2", "Own work not pushed", "x1")
            .Commit("x1", "Own work", "c1")
            .LocalBranch("own", "x2")
            .ViewRepoAsync();

        var seen = HiddenNews.Seen(repo, Seen(("origin/main", "c1"), ("origin/feature", "f3"), ("origin/own", "x1")));

        Assert.AreEqual(Sha("x2"), seen["own"]);
        Assert.AreEqual("", Text(HiddenNews.Of(repo, seen)));
    }

    // An older gmd remembered only remote branches, and one with no commits of its own was no news,
    // so what it did not remember of those is taken as seen once, rather than all of it being news
    // at once. A remote branch with commits of its own was news then too, and still is.
    [TestMethod]
    public async Task TestWhatAnOlderGmdRememberedIsTakenAsSeenOnce()
    {
        var repo = await Fixture()
            .Commit("x1", "Agent work", "c1")
            .LocalBranch("agent", "x1")
            .RemoteBranch("origin/release", "c1")
            .ViewRepoAsync();
        var remembered = Seen(("origin/main", "c1"));

        var seen = HiddenNews.Seen(repo, remembered, isForAll: false);

        Assert.AreEqual("origin/feature new 3", Text(HiddenNews.Of(repo, seen)));
        Assert.AreEqual(
            "origin/feature new 3, origin/release new 0, agent new 1",
            Text(HiddenNews.Of(repo, HiddenNews.Seen(repo, remembered))),
            "Once remembered for all, they are news"
        );
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

    // Listing a branch as news is seeing it, as far as it was when listed: more pushed to it since is
    // news again
    [TestMethod]
    public async Task TestAListedBranchIsSeenAsItWasWhenListed()
    {
        var remembered = Seen(("origin/main", "c1"), ("origin/feature", "f1"));
        var repo = await Fixture().ViewRepoAsync();
        var listed = HiddenNews.Of(repo, remembered);

        var seen = HiddenNews.Looked(repo, remembered, listed);

        Assert.AreEqual("", Text(HiddenNews.Of(repo, seen)));
        var later = await new RepoBuilder()
            .Commit("f4", "Feature 4", "f3")
            .Commit("f3", "Feature 3", "f2")
            .Commit("f2", "Feature 2", "f1")
            .Commit("f1", "Feature 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .BranchWithRemote("feature", "f4")
            .ViewRepoAsync();
        Assert.AreEqual("origin/feature 1", Text(HiddenNews.Of(later, HiddenNews.Seen(later, seen))));
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
        string.Join(", ", news.Select(n => $"{n.Branch.Name} {(n.IsNew ? "new " : "")}{n.Count}"));

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
