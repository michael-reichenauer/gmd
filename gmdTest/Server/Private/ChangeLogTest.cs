using gmd.Server.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server;

// The change log is made from the commits of main: each commit tagged with a version starts a
// release, whose notes are the lists written in the merge commits (and the subjects of plain
// commits) back to the previous release. CI makes it in the release commit on main, naming the
// commits since the latest release by the version it is about to release.
[TestClass]
public class ChangeLogTest
{
    static readonly DateTime Now = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    // Main as CI leaves it: a merge from dev with the notes, then the release commit, tagged. The
    // newest merge is not released yet. The oldest tags are on a merge commit and on plain commits,
    // as they were before CI made release commits.
    static RepoBuilder Releases() =>
        new RepoBuilder()
            .Commit("a3", "Merge branch 'dev'\n\n- Add worktree support\n- Fix progress", "a2", "d2")
            .Commit("d2", "Worktree work", "d1")
            .Commit("a2", "Release v0.94.1431.496", "a1")
            .Commit("a1", "Merge branch 'dev'\n\n- Update packages", "b1", "d1")
            .Commit("d1", "Package work", "b1")
            .Commit("b1", "Fix typo", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .Tag("v0.94.1431.496", "a2")
            .Tag("v0.93.1400.100", "b1")
            .Tag("v0.92.1.1", "c1")
            .BranchWithRemote("main", "a3", isCurrent: true)
            .LocalBranch("dev", "d2");

    [TestMethod]
    public async Task TestNewReleaseHeadsTheUnreleasedCommits()
    {
        var repo = await Releases().ViewRepoAsync("main");

        Assert.AreEqual(
            """

            4 releases:

            ## [v0.95.1440.123] - 2026-10-06
            - Added worktree support
            - Fixed progress

            ## [v0.94.1431.496] - 2024-10-15
            - Updated packages

            ## [v0.93.1400.100] - 2024-10-15
            - Fixed typo
            - Second

            """,
            ChangeLog.Create(repo, "v0.95.1440.123", Now),
            "The release commit has no line, the dev commits are not on main, and the oldest release "
                + "has no newer tag to end it, so it is left out"
        );
    }

    // Without a new release, e.g. 'gmd --changelog', the unreleased commits are "Current", which
    // is no release and not counted.
    [TestMethod]
    public async Task TestUnreleasedCommitsAreCurrent()
    {
        var repo = await Releases().ViewRepoAsync("main");

        var log = ChangeLog.Create(repo, null, Now);

        StringAssert.StartsWith(log, "\n3 releases:\n\n## [Current] - 2026-10-06\n- Added worktree support\n");
    }

    // CI makes the change log with the version bumped and the build time injected, i.e. with
    // uncommitted changes, which show as a commit at the top of main.
    [TestMethod]
    public async Task TestUncommittedChangesAreNoNotes()
    {
        var repo = await Releases().WithStatus(modified: 2).ViewRepoAsync("main");
        Assert.IsTrue(repo.ViewCommits[0].IsUncommitted, "The fixture should have the uncommitted commit");

        var log = ChangeLog.Create(repo, "v0.95.1440.123", Now);

        StringAssert.StartsWith(log, "\n4 releases:\n\n## [v0.95.1440.123] - 2026-10-06\n- Added worktree support\n");
        Assert.IsFalse(log.Contains("uncommitted"), log);
    }

    // A release with nothing but the release commit, e.g. a manual run of the workflow on main,
    // has no notes, so it gets no section and is not counted.
    [TestMethod]
    public async Task TestReleaseCommitAloneIsNoSection()
    {
        var repo = await new RepoBuilder()
            .Commit("a2", "Release v0.94.1431.496", "a1")
            .Commit("a1", "Merge branch 'dev'\n\n- Update packages", "c1", "d1")
            .Commit("d1", "Package work", "c1")
            .Commit("c1", "Initial")
            .Tag("v0.94.1431.496", "a2")
            .Tag("v0.93.1400.100", "c1")
            .BranchWithRemote("main", "a2", isCurrent: true)
            .ViewRepoAsync("main");

        Assert.AreEqual(
            """

            2 releases:

            ## [v0.94.1431.496] - 2024-10-15
            - Updated packages

            """,
            ChangeLog.Create(repo, "v0.95.1440.123", Now)
        );
    }
}
