using gmd.Git;
using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;
using GitCommit = gmd.Git.Commit;

namespace gmdTest.Server.Private.Augmented.Private;

// The lost commits made into lines of work, and what each line is: the branch it was made on and
// what took it out of the history, from reflog entries as git writes them, latest first per ref
[TestClass]
public class LostWorkFinderTest
{
    static readonly DateTime BaseTime = new(2024, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    static string Id(string name) => RepoBuilder.Sha(name);

    // A lost commit, made at its own minute
    static GitCommit Lost(string name, string subject, int minute, params string[] parents) =>
        new GitCommit(
            Id(name),
            Id(name).Sid(),
            parents.Select(Id).ToArray(),
            subject,
            subject,
            "Test Author",
            BaseTime.AddMinutes(minute),
            BaseTime.AddMinutes(minute)
        );

    // Entries of one ref, latest first, each given as (commit name, message)
    static IEnumerable<ReflogEntry> Log(string reference, params (string commit, string message)[] entries) =>
        entries.Select((e, i) => new ReflogEntry(Id(e.commit), reference, i, e.message));

    static IReadOnlyList<LostWork> Find(
        IEnumerable<ReflogEntry> reflog,
        IReadOnlyList<GitCommit> lost,
        params string[] localBranches
    ) => Find(reflog, lost, [], localBranches);

    static IReadOnlyList<LostWork> Find(
        IEnumerable<ReflogEntry> reflog,
        IReadOnlyList<GitCommit> lost,
        IEnumerable<(string, DateTime)> reachable,
        params string[] localBranches
    ) => LostWorkFinder.Find(reflog.ToList(), lost, reachable, localBranches.ToHashSet());

    // 'reset --hard HEAD~2' leaves two commits behind, one line of work, made on the branch
    [TestMethod]
    public void TestTheCommitsAResetLeftBehind()
    {
        var works = Find(
            Log(
                "refs/heads/main",
                ("a1", "reset: moving to HEAD~2"),
                ("a3", "commit: Three"),
                ("a2", "commit: Two"),
                ("a1", "commit (initial): One")
            ),
            [Lost("a3", "Three", 3, "a2"), Lost("a2", "Two", 2, "a1")],
            "main"
        );

        Assert.AreEqual(1, works.Count);
        var work = works[0];
        Assert.AreEqual(
            (Id("a3"), "Three", "main", LostBy.Reset),
            (work.TipId, work.Subject, work.BranchName, work.LostBy)
        );
        CollectionAssert.AreEqual(new[] { Id("a3"), Id("a2") }, work.CommitIds.ToArray());
        Assert.AreEqual(Id("a2"), work.OldestId);
        Assert.AreEqual(BaseTime.AddMinutes(3), work.Time);
        Assert.IsFalse(work.IsRewritten);
    }

    // A deleted branch's reflog went with it, but HEAD's says what was made on it
    [TestMethod]
    public void TestTheWorkOfADeletedBranch()
    {
        var works = Find(
            Log(
                "HEAD",
                ("e1", "checkout: moving from gone to main"),
                ("f2", "commit: Gone two"),
                ("f1", "commit: Gone one"),
                ("e1", "checkout: moving from main to gone")
            ),
            [Lost("f2", "Gone two", 2, "f1"), Lost("f1", "Gone one", 1, "e1")],
            "main"
        );

        Assert.AreEqual(("gone", LostBy.Deleted, 2), (works[0].BranchName, works[0].LostBy, works[0].CommitIds.Count));
    }

    // Work made on a detached HEAD belongs to no branch, and a checkout left it
    [TestMethod]
    public void TestTheWorkLeftOnADetachedHead()
    {
        var works = Find(
            Log(
                "HEAD",
                ("e1", "checkout: moving from d100000000000000000000000000000000000000 to main"),
                ("d1", "commit: Detached work"),
                ("e1", "checkout: moving from main to e100000000000000000000000000000000000000")
            ),
            [Lost("d1", "Detached work", 1, "e1")],
            "main"
        );

        Assert.AreEqual(("", LostBy.Detached), (works[0].BranchName, works[0].LostBy));
    }

    // An amend leaves the commit it replaced, whose author and author time the amended one keeps:
    // an older version of work that is still there, listed after the work that is gone
    [TestMethod]
    public void TestAnAmendLeavesAnOlderVersion()
    {
        var works = Find(
            Log(
                "refs/heads/main",
                ("b2", "commit (amend): Two again"),
                ("a2", "commit: Two"),
                ("a1", "commit (initial): One")
            ),
            [Lost("a2", "Two", 2, "a1"), Lost("c1", "Older but gone", 1, "a1")],
            [("Test Author", BaseTime.AddMinutes(2))],
            "main"
        );

        Assert.AreEqual(2, works.Count);
        Assert.AreEqual((Id("c1"), false), (works[0].TipId, works[0].IsRewritten));
        Assert.AreEqual((Id("a2"), true, LostBy.Amend), (works[1].TipId, works[1].IsRewritten, works[1].LostBy));
    }

    // A rebase replaced the branch's commits with copies, and the branch's own reflog says so
    [TestMethod]
    public void TestTheCommitsARebaseReplaced()
    {
        var works = Find(
            Log(
                "refs/heads/feature",
                ("b2", "rebase (finish): refs/heads/feature onto e200000000000000000000000000000000000000"),
                ("a2", "commit: Feature"),
                ("e1", "branch: Created from main")
            ),
            [Lost("a2", "Feature", 2, "e1")],
            "main",
            "feature"
        );

        Assert.AreEqual(("feature", LostBy.Rebase), (works[0].BranchName, works[0].LostBy));
    }

    // Two lines that grew from one lost commit are two tips, each with the shared commit, and the
    // newest is listed first
    [TestMethod]
    public void TestTwoTipsOnOneLostCommit()
    {
        var works = Find(
            Log("refs/heads/main", ("e1", "reset: moving to e1"), ("a2", "commit: Two"), ("e1", "commit: One")),
            [Lost("b3", "Other three", 4, "a2"), Lost("a3", "Three", 3, "a2"), Lost("a2", "Two", 2, "e1")],
            "main"
        );

        CollectionAssert.AreEqual(new[] { Id("b3"), Id("a3") }, works.Select(w => w.TipId).ToArray());
        CollectionAssert.AreEqual(new[] { Id("b3"), Id("a2") }, works[0].CommitIds.ToArray());
        CollectionAssert.AreEqual(new[] { Id("a3"), Id("a2") }, works[1].CommitIds.ToArray());
    }
}
