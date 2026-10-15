using gmd.Cui.Commits;
using gmd.Cui.Common;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Commits;

// The rows of the commit details pane, as text, for the commits each kind of row is there for
[TestClass]
public class CommitDetailsRowsTest
{
    // main, one commit ahead of origin, with dev merged in and tagged, and a change not committed
    static Task<Repo> Fixture() =>
        new RepoBuilder()
            .Commit("c4", "Ahead work", "c3")
            .Commit("c3", "Merge branch 'dev' into main\n\nBody line", "c2", "d1")
            .Commit("d1", "Feature work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true, remoteTipCommit: "c3", ahead: 1)
            .LocalBranch("dev", "d1")
            .Tag("v1.0", "c3")
            .WithStatus(modified: 1)
            .ViewRepoAsync(ShowBranches.AllActive);

    // Two branches sharing a commit, with nothing to tell which of them it is on
    static RepoBuilder AmbiguousFixture() =>
        new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1");

    static IReadOnlyList<Text> RowsOf(Repo repo, string id) =>
        CommitDetailsRows.Of(
            repo,
            repo.CommitById[id],
            repo.BranchByName[repo.CommitById[id].BranchName],
            new BranchColorService(new FakeRepoConfig())
        );

    static string TextOf(Repo repo, string id) =>
        string.Join("\n", RowsOf(repo, id).Select(r => r.ToString().TrimEnd()));

    [TestMethod]
    public async Task TestAMergeCommit()
    {
        Assert.AreEqual(
            """
            Id:         c300000000000000000000000000000000000000  (/test/repo)
            Branch:     main  (origin/main)
            Author:     Test Author, time: 2024-10-15 11:59:00 +00:00
            Children:   c4000000
            Parents:    c2000000, d1000000
            Tags:       [v1.0]
            Tips:       (origin/main)
            Merge branch 'dev' into main

            Body line
            """,
            TextOf(await Fixture(), RepoBuilder.Sha("c3"))
        );
    }

    [TestMethod]
    public async Task TestACommitNotPushed()
    {
        Assert.AreEqual(
            """
            Id:         c400000000000000000000000000000000000000  (/test/repo)
            Branch:     main  (main)
            Author:     Test Author, time: 2024-10-15 12:00:00 +00:00
            Children:
            Parents:    c3000000
            Remote:     ▲ pushable
            Ahead work
            """,
            TextOf(await Fixture(), RepoBuilder.Sha("c4"))
        );
    }

    [TestMethod]
    public async Task TestTheUncommittedChanges()
    {
        var repo = await Fixture();

        Assert.AreEqual(
            """
            Id:         Uncommitted  (/test/repo)
            Branch:     main  (main)
            Children:
            Parents:    c4000000
            Tips:       (main)
            1 uncommitted change
            """,
            TextOf(repo, Repo.UncommittedId)
        );
        Assert.AreEqual(Color.BrightYellow, RowsOf(repo, Repo.UncommittedId)[0].Fragments[1].Color);
    }

    [TestMethod]
    public async Task TestAnAmbiguousCommit()
    {
        Assert.AreEqual(
            """
            Id:         d100000000000000000000000000000000000000  (/test/repo)
            Branch:     feat-a  (feat-a)
            Ambiguous:  feat-a, feat-b
            Author:     Test Author, time: 2024-10-15 11:58:00 +00:00
            Children:   a1000000, b1000000
            Parents:    c1000000
            Shared work
            """,
            TextOf(await AmbiguousFixture().ViewRepoAsync(ShowBranches.AllActive), RepoBuilder.Sha("d1"))
        );
    }

    [TestMethod]
    public async Task TestACommitWhoseBranchTheUserSet()
    {
        var repo = await AmbiguousFixture().UserSetBranch("d1", "feat-a").ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual(
            "Branch:     feat-a  (feat-a)   Φ (ambiguity resolved by user)",
            TextOf(repo, RepoBuilder.Sha("d1")).Split('\n')[1]
        );
    }
}
