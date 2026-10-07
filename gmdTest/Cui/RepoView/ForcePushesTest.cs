using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What is said of a remote branch a force push rewrote
[TestClass]
public class ForcePushesTest
{
    static RemoteRewrite Rewrite(int own = 1, int dropped = 0, bool isByYou = false, string tip = "b2") =>
        new RemoteRewrite(
            "dev",
            "origin/dev",
            RepoBuilder.Sha("c1"),
            RepoBuilder.Sha("a3"),
            RepoBuilder.Sha("a3"),
            RepoBuilder.Sha(tip),
            own,
            2,
            3,
            Enumerable.Range(0, dropped).Select(i => RepoBuilder.Sha($"d{i}")).ToList(),
            isByYou,
            true
        );

    [TestMethod]
    public void TestWhatIsSaidWhenItIsFound()
    {
        Assert.AreEqual(
            "'origin/dev' was rewritten by a force push: Pull moves your 1 commit onto it",
            ForcePushes.Found(Rewrite())
        );
        Assert.AreEqual(
            "'origin/dev' was rewritten by a force push, dropping 2 commits: Pull takes the new version",
            ForcePushes.Found(Rewrite(own: 0, dropped: 2))
        );
    }

    // Each rewrite is told once, and one pushed from here not at all
    [TestMethod]
    public void TestEachRewriteIsToldOnce()
    {
        var notes = new ForcePushNotes();
        Repo RepoWith(params RemoteRewrite[] rewrites) =>
            Repo.Empty with
            {
                RemoteRewrites = rewrites.ToDictionary(r => r.BranchName),
            };

        Assert.IsNotNull(notes.Untold(RepoWith(Rewrite())));
        Assert.IsNull(notes.Untold(RepoWith(Rewrite())));
        Assert.IsNotNull(notes.Untold(RepoWith(Rewrite(tip: "b3"))), "Rewritten again");
        Assert.IsNull(notes.Untold(RepoWith(Rewrite(isByYou: true, tip: "b4"))));
    }

    // One fetch can bring force pushes on several branches, and each is told, since each is marked so
    [TestMethod]
    public void TestRewritesFoundTogetherAreToldTogether()
    {
        var notes = new ForcePushNotes();
        var feature = Rewrite() with { BranchName = "feature", RemoteName = "origin/feature" };
        var repo = Repo.Empty with { RemoteRewrites = new[] { Rewrite(), feature }.ToDictionary(r => r.BranchName) };

        Assert.AreEqual(
            "'origin/dev', 'origin/feature' were rewritten by force pushes: pull each to take the new version",
            notes.Untold(repo)
        );
        Assert.IsNull(notes.Untold(repo));
    }

    // dev had a1 <- a2 <- a3 on origin and Own work on top here; a force push copied a2 onto b1 and
    // dropped a3
    static Task<Repo> RewrittenAsync() =>
        new RepoBuilder()
            .Commit("e1", "Main", "a1")
            .Commit("c1", "Own work", "a3")
            .CopyOf("b2", "a2", "b1")
            .Commit("b1", "Moved on", "a1")
            .Commit("a3", "Dropped", "a2")
            .Commit("a2", "Kept", "a1")
            .Commit("a1", "Initial")
            .LocalBranch("main", "e1")
            .LocalBranch("dev", "c1", isCurrent: true, remoteName: "origin/dev", ahead: 3, behind: 2)
            .RemoteBranch("origin/dev", "b2")
            .RemoteReflog("origin/dev", "b2", "fetch: forced-update")
            .RemoteReflog("origin/dev", "a3", "update by push")
            .ViewRepoAsync();

    [TestMethod]
    public async Task TestThePullQuestion()
    {
        var repo = await RewrittenAsync();

        Assert.AreEqual(
            """
            'origin/dev' was rewritten by a force push,
            so 'dev' has the old version of it.

            Pull moves your 1 commit onto the new version, and leaves out
            the 2 commits of the old one, which Recover Lost Commits finds.
            It does not merge the two, which would put every commit in twice.

            The force push dropped 1 commit, which the new version does not have:
              a30000 Dropped
            If that was a mistake, Restore origin/dev in the branch menu puts it back.
            """,
            ForcePushes.PullQuestion(repo, repo.RemoteRewrites["dev"])
        );
    }

    [TestMethod]
    public void TestWhatIsSaidOncePulled()
    {
        Assert.AreEqual("Moved your 1 commit onto the rewritten 'origin/dev'", ForcePushes.Pulled(Rewrite()));
        Assert.AreEqual("Updated 'dev' to the rewritten 'origin/dev'", ForcePushes.Pulled(Rewrite(own: 0)));
    }

    [TestMethod]
    public async Task TestWhatRestoringOriginSays()
    {
        var repo = await RewrittenAsync();
        var rewrite = repo.RemoteRewrites["dev"];

        Assert.AreEqual("Restore origin/dev from before the Force Push ...", ForcePushes.RestoreLabel(rewrite));
        Assert.AreEqual(
            """
            Put 'origin/dev' back as it was before the force push?

            It gets a30000 Dropped again, and the 2 commits
            of the new version leave it. This is a force push too: everyone
            who pulled the new version has to deal with it in turn.
            """,
            ForcePushes.RestoreQuestion(repo, rewrite)
        );
        Assert.AreEqual(
            "Put 'origin/dev' back at a30000, as it was before the force push",
            ForcePushes.Restored(rewrite)
        );
        Assert.AreEqual(
            "Commits were pushed to 'origin/dev' after the force push: putting it back would drop them",
            ForcePushes.WhyNoRestore(rewrite)
        );
    }
}
