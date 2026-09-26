using gmd.Git;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// What the reflog witnessed about the branches of commits. The entries are what git writes, as the
// canary in GitIntegrationTest checks; each ref's entries are declared latest first, as git lists them.
[TestClass]
public class ReflogWitnessTest
{
    static string Id(string name) => RepoBuilder.Sha(name);

    // Entries of one ref, latest first, each given as (commit name, message)
    static IEnumerable<ReflogEntry> Log(string reference, params (string commit, string message)[] entries) =>
        entries.Select((e, i) => new ReflogEntry(Id(e.commit), reference, i, e.message));

    // A branch's own reflog says which commits were made on it, whatever way they were made, and
    // nothing for the moves that made no commit
    [TestMethod]
    public void TestABranchReflogSaysWhatWasMadeOnIt()
    {
        var madeOn = ReflogWitness.MadeOn([
            .. Log(
                "refs/heads/dev",
                ("a8", "pull: Fast-forward"),
                ("a7", "reset: moving to HEAD~1"),
                ("a6", "rebase (finish): refs/heads/dev onto 1234567"),
                ("a5", "merge feature: Fast-forward"),
                ("a4", "pull: Merge made by the 'ort' strategy."),
                ("a3", "merge feature: Merge made by the 'ort' strategy."),
                ("a2", "cherry-pick: Fix"),
                ("b2", "revert: Revert \"Fix\""),
                ("b1", "commit (amend): Work"),
                ("c3", "commit (merge): Merge branch 'x' into dev"),
                ("c2", "commit: Work"),
                ("c1", "commit (initial): Initial"),
                ("c0", "branch: Created from main")
            ),
        ]);

        CollectionAssert.AreEquivalent(
            new[] { "a4", "a3", "a2", "b2", "b1", "c3", "c2", "c1" }.Select(Id).ToArray(),
            madeOn.Keys.ToArray()
        );
        Assert.IsTrue(madeOn.Values.All(b => b == "dev"));
    }

    // A deleted branch's reflog is gone with it, but HEAD's says what was checked out when each commit
    // was made. Before the first checkout HEAD was on the branch that checkout left.
    [TestMethod]
    public void TestHeadReflogSaysWhatWasMadeOnADeletedBranch()
    {
        var madeOn = ReflogWitness.MadeOn([
            .. Log(
                "HEAD",
                ("e2", "commit: More main"),
                ("f1", "checkout: moving from gone to main"),
                ("f1", "commit: Gone work"),
                ("e1", "checkout: moving from main to gone"),
                ("e1", "commit (initial): Initial")
            ),
        ]);

        Assert.AreEqual("gone", madeOn[Id("f1")]);
        Assert.AreEqual("main", madeOn[Id("e1")]);
        Assert.AreEqual("main", madeOn[Id("e2")]);
    }

    // A detached HEAD and a rebase are on no branch, so what is made there says nothing, until a
    // rebase finishes back on its branch
    [TestMethod]
    public void TestNothingIsMadeOnABranchWhileDetached()
    {
        var madeOn = ReflogWitness.MadeOn([
            .. Log(
                "HEAD",
                ("a4", "commit: After the rebase"),
                ("a3", "rebase (finish): returning to refs/heads/feature"),
                ("a3", "rebase (pick): Picked"),
                ("a2", "rebase (start): checkout main"),
                ("a1", "commit: Detached work"),
                ("c1", "checkout: moving from main to c1c1c1c"),
                ("c1", "commit (initial): Initial")
            ),
        ]);

        Assert.AreEqual("feature", madeOn[Id("a4")]);
        Assert.IsFalse(madeOn.ContainsKey(Id("a3")));
        Assert.IsFalse(madeOn.ContainsKey(Id("a1")));
        Assert.AreEqual("main", madeOn[Id("c1")]);
    }

    // Another worktree's HEAD has a reflog of its own, and is followed the same way
    [TestMethod]
    public void TestAnotherWorktreesHeadIsFollowedToo()
    {
        var madeOn = ReflogWitness.MadeOn([
            .. Log("worktrees/topic/HEAD", ("t1", "commit: Topic work"), ("c1", "checkout: moving from main to topic")),
        ]);

        Assert.AreEqual("topic", madeOn[Id("t1")]);
    }

    // Where a branch was started is the commit of the branch it was started from, by name, as a
    // remote branch too, or through HEAD for 'git checkout -b' and 'git switch -c'. A commit id, an
    // expression, and a HEAD with no checkout to go by ('git branch x') say nothing.
    [TestMethod]
    public void TestWhereABranchWasStartedFrom()
    {
        var startedFrom = ReflogWitness.StartedFrom([
            .. Log("refs/heads/a", ("a1", "branch: Created from dev")),
            .. Log("refs/heads/b", ("b1", "branch: Created from origin/dev")),
            .. Log("refs/heads/c", ("c1", "branch: Created from refs/remotes/origin/release/1.0")),
            .. Log("refs/heads/d", ("d1", "branch: Created from HEAD")),
            .. Log("refs/heads/e", ("e1", "branch: Created from 1234567890abcdef1234567890abcdef12345678")),
            .. Log("refs/heads/f", ("f1", "branch: Created from main~2")),
            .. Log("refs/heads/g", ("a9", "branch: Created from HEAD")),
            .. Log("HEAD", ("d1", "checkout: moving from dev to d")),
        ]);

        Assert.AreEqual("dev", startedFrom[Id("a1")]);
        Assert.AreEqual("dev", startedFrom[Id("b1")]);
        Assert.AreEqual("release/1.0", startedFrom[Id("c1")]);
        Assert.AreEqual("dev", startedFrom[Id("d1")]);
        Assert.AreEqual(4, startedFrom.Count);
    }

    // Where a commit was made is the better fact: a branch started from dev at a commit made on
    // feature (dev fast-forwarded to it) does not make that commit dev's
    [TestMethod]
    public void TestMadeOnComesBeforeStartedFrom()
    {
        var branches = ReflogWitness.BranchByCommit([
            .. Log("refs/heads/feature", ("f1", "commit: Feature work")),
            .. Log("refs/heads/other", ("f1", "branch: Created from dev"), ("d1", "commit: nothing")),
            .. Log("refs/heads/x", ("d1", "branch: Created from dev")),
        ]);

        Assert.AreEqual("feature", branches[Id("f1")]);
        Assert.AreEqual("other", branches[Id("d1")], "Made on other, by its own reflog");
    }
}
