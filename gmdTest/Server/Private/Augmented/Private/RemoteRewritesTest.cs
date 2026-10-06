using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// A force push on origin told from new commits on both sides, by the remote branch's reflog: where
// origin was each time it was fetched or pushed, latest first, as git writes it
[TestClass]
public class RemoteRewritesTest
{
    static string Id(string name) => RepoBuilder.Sha(name);

    static IReadOnlyDictionary<string, RemoteRewrite> Find(RepoBuilder builder) =>
        RemoteRewrites.Find(builder.ToGitRepo());

    // dev had a1 <- a2 <- a3 on origin, and a commit of its own, c1, on top here. Someone rebased it
    // onto b1, keeping a2 (copied as b2) and dropping a3, and force pushed; then it was fetched here.
    static RepoBuilder Rewritten() => Rewritten(new RepoBuilder());

    static RepoBuilder Rewritten(RepoBuilder builder) =>
        builder
            .Commit("c1", "Own work", "a3")
            .CopyOf("b2", "a2", "b1")
            .Commit("b1", "Moved on", "a1")
            .Commit("a3", "Dropped", "a2")
            .Commit("a2", "Kept", "a1")
            .Commit("a1", "Initial")
            .LocalBranch("dev", "c1", isCurrent: true, remoteName: "origin/dev", ahead: 3, behind: 2)
            .RemoteBranch("origin/dev", "b2")
            .RemoteReflog("origin/dev", "b2", "fetch: forced-update")
            .RemoteReflog("origin/dev", "a3", "update by push");

    [TestMethod]
    public void TestAForcePushFetchedHere()
    {
        var rewrite = Find(Rewritten())["dev"];

        Assert.AreEqual(
            new RemoteRewrite(
                "dev",
                "origin/dev",
                Id("c1"),
                Id("a3"),
                Id("a3"),
                Id("b2"),
                1,
                2,
                2,
                rewrite.DroppedIds,
                false,
                true
            ),
            rewrite
        );
        CollectionAssert.AreEqual(new[] { Id("a3") }, rewrite.DroppedIds.ToArray());
    }

    // New commits on both sides is no rewrite: what the local branch was built on is still in
    // origin's history
    [TestMethod]
    public void TestNewCommitsOnBothSidesAreNoRewrite()
    {
        var builder = new RepoBuilder()
            .Commit("c1", "Own work", "a2")
            .Commit("b1", "Theirs", "a2")
            .Commit("a2", "Shared", "a1")
            .Commit("a1", "Initial")
            .LocalBranch("dev", "c1", isCurrent: true, remoteName: "origin/dev", ahead: 1, behind: 1)
            .RemoteBranch("origin/dev", "b1")
            .RemoteReflog("origin/dev", "b1", "fetch: fast-forward")
            .RemoteReflog("origin/dev", "a2", "update by push");

        Assert.AreEqual(0, Find(builder).Count);
    }

    // A rewrite pushed from here, e.g. gmd's Rebase, which force pushes, with the rebase then undone
    [TestMethod]
    public void TestARewritePushedFromHere()
    {
        var builder = new RepoBuilder()
            .CopyOf("b2", "a2", "b1")
            .Commit("b1", "Moved on", "a1")
            .Commit("a2", "Feature", "a1")
            .Commit("a1", "Initial")
            .LocalBranch("dev", "a2", isCurrent: true, remoteName: "origin/dev", ahead: 1, behind: 2)
            .RemoteBranch("origin/dev", "b2")
            .RemoteReflog("origin/dev", "b2", "update by push")
            .RemoteReflog("origin/dev", "a2", "update by push");

        var rewrite = Find(builder)["dev"];

        Assert.AreEqual(
            (true, true, 0, 0),
            (rewrite.IsByYou, rewrite.IsRestorable, rewrite.OwnCount, rewrite.DroppedIds.Count)
        );
    }

    // Commits pushed on top of the rewrite would be lost if origin were put back as it was
    [TestMethod]
    public void TestCommitsOnTopOfTheRewriteMakeItNotRestorable()
    {
        var builder = new RepoBuilder()
            .Commit("b3", "On top", "b2")
            .Commit("c1", "Own work", "a3")
            .CopyOf("b2", "a2", "b1")
            .Commit("b1", "Moved on", "a1")
            .Commit("a3", "Dropped", "a2")
            .Commit("a2", "Kept", "a1")
            .Commit("a1", "Initial")
            .LocalBranch("dev", "c1", isCurrent: true, remoteName: "origin/dev", ahead: 3, behind: 3)
            .RemoteBranch("origin/dev", "b3")
            .RemoteReflog("origin/dev", "b3", "fetch: fast-forward")
            .RemoteReflog("origin/dev", "b2", "fetch: forced-update")
            .RemoteReflog("origin/dev", "a3", "update by push");

        var rewrite = Find(builder)["dev"];

        Assert.AreEqual((Id("a3"), Id("b3"), false), (rewrite.OldTipId, rewrite.NewTipId, rewrite.IsRestorable));
    }

    // With no reflog of the remote branch, e.g. an expired one, there is nothing to tell by
    [TestMethod]
    public void TestNothingWithoutTheRemoteBranchesReflog()
    {
        var builder = new RepoBuilder()
            .Commit("c1", "Own work", "a2")
            .CopyOf("b2", "a2", "a1")
            .Commit("a2", "Feature", "a1")
            .Commit("a1", "Initial")
            .LocalBranch("dev", "c1", isCurrent: true, remoteName: "origin/dev", ahead: 2, behind: 1)
            .RemoteBranch("origin/dev", "b2");

        Assert.AreEqual(0, Find(builder).Count);
    }

    // The rewrites are carried all the way to the repo the UI renders
    [TestMethod]
    public async Task TestTheRewritesReachTheViewRepo()
    {
        var repo = await Rewritten(new RepoBuilder().Commit("m1", "Main", "a1"))
            .LocalBranch("main", "m1")
            .ViewRepoAsync();

        Assert.AreEqual(Id("a3"), repo.RemoteRewrites["dev"].ForkPointId);
    }
}
