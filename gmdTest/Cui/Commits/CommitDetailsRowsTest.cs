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

    static IReadOnlyList<Text> RowsOf(Repo repo, string name)
    {
        var commit = repo.CommitById[name == "" ? Repo.UncommittedId : RepoBuilder.Sha(name)];
        return CommitDetailsRows.Of(repo, commit, repo.BranchByName[commit.BranchName], Colors);
    }

    static readonly BranchColorService Colors = new(new FakeRepoConfig());

    static string TextOf(Repo repo, string name) =>
        string.Join("\n", RowsOf(repo, name).Select(r => r.ToString().TrimEnd()));

    static string RowOf(Repo repo, string name, string label) =>
        RowsOf(repo, name).Select(r => r.ToString()).First(r => r.StartsWith(label));

    // The merge names the branch merged in, the branch the commit is on is said once, and the tag is
    // beside the id. The message has the rest of the pane, below a blank row.
    [TestMethod]
    public async Task TestAMergeCommit()
    {
        Assert.AreEqual(
            """
            Id:       c300000000000000000000000000000000000000  [v1.0]
            Branch:   main
            Author:   Test Author, 2024-10-15 11:59:00 +00:00
            Parents:  c2000000, d1000000 dev    Children: c4000000

            Merge branch 'dev' into main

            Body line
            """,
            TextOf(await Fixture(), "c3")
        );
    }

    [TestMethod]
    public async Task TestTheBranchMergedInIsInItsColor()
    {
        var repo = await Fixture();
        var parents = RowsOf(repo, "c3")[3];

        var dev = parents.Fragments.Single(f => f.Text == "dev");
        Assert.AreEqual(Colors.GetColor(repo, repo.BranchByName["dev"]), dev.Color);
    }

    [TestMethod]
    public async Task TestACommitNotPushed()
    {
        Assert.AreEqual("Branch:   main  ▲ pushable", RowOf(await Fixture(), "c4", "Branch:"));
    }

    // The first commit of a branch says where it was started from, and where it was merged
    [TestMethod]
    public async Task TestTheFirstCommitOfABranch()
    {
        Assert.AreEqual("Parents:  c1000000 main    Children: c3000000 main", RowOf(await Fixture(), "d1", "Parents:"));
    }

    [TestMethod]
    public async Task TestTheFirstCommitOfTheRepo()
    {
        Assert.AreEqual("Parents:  none    Children: d1000000 dev, c2000000", RowOf(await Fixture(), "c1", "Parents:"));
    }

    // No author or time, and the id is said in yellow, as the log's © is
    [TestMethod]
    public async Task TestTheUncommittedChanges()
    {
        var repo = await Fixture();

        Assert.AreEqual(
            """
            Id:       Uncommitted
            Branch:   main
            Parents:  c4000000

            1 uncommitted change
            """,
            TextOf(repo, "")
        );
        Assert.AreEqual(Color.BrightYellow, RowsOf(repo, "")[0].Fragments[1].Color);
    }

    // The files below the count of them, the conflicted first and the rest by path, each with the
    // letter the commit dialog gives it
    [TestMethod]
    public async Task TestTheUncommittedFiles()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .WithStatus(
                modifiedFiles: ["src/b.cs"],
                addedFiles: ["new.txt"],
                deletedFiles: ["a.txt"],
                conflictedFiles: ["z.cs", "m.cs"],
                renamedFiles: [("old.md", "doc/new.md")]
            )
            .ViewRepoAsync();

        Assert.AreEqual(
            """
            Id:       Uncommitted
            Branch:   main
            Parents:  c1000000

            CONFLICTS: 2, 6 uncommitted changes

            C m.cs
            C z.cs
            D a.txt
            A new.txt
            R old.md → doc/new.md
            M src/b.cs
            """,
            TextOf(repo, "")
        );
        var rows = RowsOf(repo, "");
        Assert.AreEqual(Color.BrightRed, rows[6].Fragments[0].Color);
        Assert.AreEqual(Color.Red, rows[8].Fragments[0].Color);
        Assert.AreEqual(Color.Green, rows[9].Fragments[0].Color);
        Assert.AreEqual(Color.Yellow, rows[11].Fragments[0].Color);
    }

    [TestMethod]
    public async Task TestAnAmbiguousCommit()
    {
        var repo = await AmbiguousFixture().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("Branch:   feat-a  ambiguous, could be: feat-a, feat-b", RowOf(repo, "d1", "Branch:"));
    }

    [TestMethod]
    public async Task TestACommitWhoseBranchTheUserSet()
    {
        var repo = await AmbiguousFixture().UserSetBranch("d1", "feat-a").ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("Branch:   feat-a  Φ (ambiguity resolved by user)", RowOf(repo, "d1", "Branch:"));
    }

    // A branch gmd recovered from a merge subject, git having deleted it long ago, and a branch of
    // origin's whose tip is on main
    [TestMethod]
    public async Task TestADeletedBranchAndAnotherBranchsTip()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'gone' into main", "c2", "g1")
            .Commit("g1", "Gone work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .RemoteBranch("origin/remote-only", "c2")
            .ViewRepoAsync(ShowBranches.AllActiveAndDeleted);

        Assert.AreEqual("Branch:   gone  (deleted)", RowOf(repo, "g1", "Branch:"));
        Assert.AreEqual("Branch:   main  also tip of remote-only", RowOf(repo, "c2", "Branch:"));
    }

    [TestMethod]
    public async Task TestADetachedHead()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .DetachedHead("c1")
            .ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual("Branch:   main  also tip of detached HEAD", RowOf(repo, "c1", "Branch:"));
    }
}
