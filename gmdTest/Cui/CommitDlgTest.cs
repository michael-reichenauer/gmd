using gmd.Cui;
using gmdTest.Fixtures;

namespace gmdTest.Cui;

// What Ctrl-A in the commit dialog adds to the message of a merge. The commits are what a merge
// hands the dialog: those of the merged branch, newest first, as git log lists them.
[TestClass]
public class CommitDlgTest
{
    // Only the subject of each commit, oldest first, however long the rest of its message is
    [TestMethod]
    public async Task TestOnlyTheSubjectsAreAdded()
    {
        var repo = await new RepoBuilder()
            .Commit("f3", "Say why it failed\n\nThe error was swallowed, so the user saw nothing.", "f2")
            .Commit("f2", "Fix the parser\n\nIt dropped the last line\nof a file with no final newline.", "f1")
            .Commit("f1", "Add a parser", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feature", "f3")
            .ViewRepoAsync("feature");

        Assert.AreEqual(
            """
            - Add a parser
            - Fix the parser
            - Say why it failed
            """,
            CommitDlg.MergedSubjects(Commits(repo, "f3", "f2", "f1"))
        );
    }

    // Merging dev into main: what dev holds is mostly the merges of the branches merged into it,
    // whose subjects say nothing, so their lists are taken in their place. Other lines of such a
    // body are left out, and so is a merge with nothing listed.
    [TestMethod]
    public async Task TestAMergeAddsTheListInItsBody()
    {
        var repo = await new RepoBuilder()
            .Commit("d4", "Merge branch 'mr/empty' into dev", "d3", "e1")
            .Commit("e1", "Try something", "d3")
            .Commit("d3", "Update packages\n\nAll of them, minor versions only.", "d2")
            .Commit(
                "d2",
                "Merge branch 'mr/parser' into dev\n\nThe parser, at last:\n- Add a parser\n- Fix the parser",
                "d1",
                "p2"
            )
            .Commit("p2", "Fix the parser\n\nIt dropped the last line.", "p1")
            .Commit("p1", "Add a parser\n\nA long story.", "d1")
            .Commit("d1", "Start dev", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("dev", "d4")
            .ViewRepoAsync("dev");

        Assert.AreEqual(
            """
            - Start dev
            - Add a parser
            - Fix the parser
            - Update packages
            """,
            CommitDlg.MergedSubjects(Commits(repo, "d4", "d3", "d2", "d1"))
        );
    }

    static IReadOnlyList<gmd.Server.Commit> Commits(gmd.Server.Repo repo, params string[] names) =>
        names.Select(n => repo.CommitById[RepoBuilder.Sha(n)]).ToList();
}
