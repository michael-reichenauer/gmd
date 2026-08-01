using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// The repo view copies a selection of rows as text. A selection is a range of rows, but the rows
// in between belong to whichever branches the graph happens to draw there, so what is copied is
// only the commits of the branch the selection started on.
[TestClass]
public class RepoViewInputTest
{
    [TestMethod]
    public async Task TestCopySelectedCommits()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual(
            $"""
            {RepoBuilder.Sid("c3")} Third
            {RepoBuilder.Sid("c2")} Second
            {RepoBuilder.Sid("c1")} Initial
            """,
            RepoViewInput.SelectedCommitsText(repo.ViewCommits, 4, 6)
        );
    }

    // Rows 0 to 3 are two main commits with a feat and a dev commit drawn in between them, and
    // those two are skipped rather than copied as if they were main commits.
    [TestMethod]
    public async Task TestCopySkipsTheCommitsOfOtherBranches()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual(
            $"""
            {RepoBuilder.Sid("c5")} Merge branch 'feat' into main
            {RepoBuilder.Sid("c4")} Merge branch 'dev' into main
            """,
            RepoViewInput.SelectedCommitsText(repo.ViewCommits, 0, 3)
        );
    }

    // The branch of the first selected commit is the one that is copied, so a selection started on
    // another branch copies that branch instead.
    [TestMethod]
    public async Task TestCopyFollowsTheBranchOfTheFirstSelectedCommit()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual(
            $"{RepoBuilder.Sid("f1")} Feature work",
            RepoViewInput.SelectedCommitsText(repo.ViewCommits, 1, 3)
        );
    }

    // main with two branches, dev and feat, both branched out of 'Second' and merged back
    static RepoBuilder ThreeBranches() =>
        new RepoBuilder()
            .Commit("c5", "Merge branch 'feat' into main", "c4", "f1")
            .Commit("f1", "Feature work", "c2")
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d1")
            .Commit("d1", "Dev work", "c2")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c5", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .LocalBranch("feat", "f1");
}
