using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui;

// Snapshot tests of the drawn branch graph, i.e. GraphCreater laying branches out in columns and
// GraphWriter turning the signs into runes. The expected values are pictures of the graph, so they
// can be reviewed by looking at them.
//
// These are characterization tests: they pin down what is drawn today, not what ought to be drawn.
//
// The runes, as GraphWriter names them:
//   ┏ ┣ ┗   branch tip, commit, bottom          ╊ ┲ ┺ ╂   the same with a line passing on the left
//   ┃       the branch, on a row of another     ╼         a branch with a single commit
//   Φ       a commit the user assigned          ─ │       connections between branches
//   ╭ ╮ ╰ ╯ merged from / branched out to       ├ ┤ ┬ ┴ ┼ crossing connections
// A branch that is not shown gets a dark ╮ (merged in from) or ╯ (branched out to) marker in the
// column right of the branch, which is how the user sees that there is more to show.
[TestClass]
public class GraphTest
{
    // A branch and its remote are two columns, the remote first, so a synced branch is drawn as
    // '┣─┺': the remote branch tip, and the local branch tip on the same commit.
    [TestMethod]
    public async Task TestLinearHistory()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .ViewRepoAsync();

        Assert.AreEqual(
            """
            ┣─┺  Third
            ┣    Second
            ┗    Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // The dev branch is not shown, so its commits are not in the graph. All the user sees of it is
    // the dark '╯' where dev branched out and the '┬' where it was merged back in.
    [TestMethod]
    public async Task TestHiddenBranchIsOnlyMoreMarkers()
    {
        var repo = await BranchAndMerge().ViewRepoAsync();

        Assert.AreEqual(
            """
            ┣┬┺  Merge branch 'dev' into main
            ┣    Third
            ┣╯   Second
            ┗    Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // Showing dev moves its commits into the graph, in their own column to the right of main. The
    // '╭' is where dev branched out of main and the '╮' is where it was merged back in.
    [TestMethod]
    public async Task TestBranchOutAndMerge()
    {
        var repo = await BranchAndMerge().ViewRepoAsync("dev");

        Assert.AreEqual(
            """
            ┣─┺╮     Merge branch 'dev' into main
            ┃  ╰╊─┺  Dev work 2
            ┃  ╭┺    Dev work 1
            ┣  │     Third
            ┣──╯     Second
            ┗        Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // Two branches that were both branched out of the same commit get columns of their own, so the
    // branch lines never overlap. dev has a remote, so it takes two columns like main does.
    [TestMethod]
    public async Task TestSeveralConcurrentBranches()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual(
            """
            ┣─┺────╮   Merge branch 'feat' into main
            ┃      ├┺  Feature work
            ┣──╮   │   Merge branch 'dev' into main
            ┃  ├┺─┺│   Dev work
            ┣  │   │   Third
            ┣──┴───╯   Second
            ┗          Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // A branch deleted after being merged is recovered from the merge subject, and is drawn like
    // any other branch. It is not a git branch, so its single commit is a '╼' rather than a '┺':
    // the branch both starts and ends there and cannot get more commits.
    [TestMethod]
    public async Task TestMergeFromDeletedBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'gone' into main", "c2", "d1")
            .Commit("d1", "Work on gone", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .ViewRepoAsync(ShowBranches.AllActiveAndDeleted);

        Assert.AreEqual(
            """
            ┣─┺╮   Merge branch 'gone' into main
            ┃  ├╼  Work on gone
            ┣  │   Second
            ┗──╯   Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // When the log is truncated, the oldest commit gets a virtual parent, so the graph ends in a
    // '┗' that says the history continues rather than pretending the repo starts there.
    [TestMethod]
    public async Task TestTruncatedLog()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial", "c0") // c0 is missing, i.e. below the truncation
            .BranchWithRemote("main", "c3", isCurrent: true)
            .Truncated()
            .ViewRepoAsync();

        Assert.AreEqual(
            """
            ┣─┺  Third
            ┣    Second
            ┣    Initial
            ┗    < ... log truncated, more commits exists ... >
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // Uncommitted changes become a virtual commit on top of the current branch, which is the local
    // main here. The remote column has no commit on that row, hence the leading blank.
    [TestMethod]
    public async Task TestUncommittedChanges()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .WithStatus(modified: 2, added: 1)
            .ViewRepoAsync();

        Assert.AreEqual(
            """
             ╭┺  3 uncommitted changes
            ┣╯   Second
            ┗    Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // A local branch ahead of its remote and a remote ahead of its local are drawn as two lines
    // out of the commit they last shared.
    [TestMethod]
    public async Task TestDivergedLocalAndRemote()
    {
        var repo = await new RepoBuilder()
            .Commit("l1", "Local only", "c2")
            .Commit("r1", "Remote only", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "r1", ahead: 1, behind: 1)
            .ViewRepoAsync();

        Assert.AreEqual(
            """
             ╭┺  Local only
            ┣│   Remote only
            ┣╯   Second
            ┗    Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // Git does not record which branch the commit below a branch point belongs to, so a commit two
    // branches could equally own is ambiguous and is drawn white for the user to resolve.
    [TestMethod]
    public async Task TestAmbiguousCommitIsDrawnWhite()
    {
        var b = Ambiguous();
        var repo = await b.ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual(
            """
                ┣
                ┃╭┺
               ╭┺╯
            ┗─┺╯
            """,
            GraphText.Of(repo, b.Config)
        );
        Assert.AreEqual(
            """
                R
                RCC
               WWC
            MWMW
            """,
            GraphText.ColorsOf(repo, b.Config)
        );
    }

    // Once the user has picked a branch for the ambiguous commit, it is drawn as a 'Φ' instead.
    [TestMethod]
    public async Task TestCommitAssignedByUser()
    {
        var repo = await Ambiguous().UserSetBranch("d1", "feat-b").ViewRepoAsync(ShowBranches.AllActive);

        Assert.AreEqual(
            """
                 ╭┺  Work a
                ┣│   Work b
               ╭Φ╯   Shared work
            ┗─┺╯     Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // Choosing which branches are shown is what gmd is for, so this walks a repo through showing
    // and hiding them, the same calls the branch menu makes.
    [TestMethod]
    public async Task TestShowAndHideBranches()
    {
        var b = ThreeBranches();
        var server = b.NewServer();
        var repo = await b.ViewRepoAsync();

        // Only main, both other branches are just more markers
        Assert.AreEqual(
            """
            ┣┬┺  Merge branch 'feat' into main
            ┣╮   Merge branch 'dev' into main
            ┣    Third
            ┣╯   Second
            ┗    Initial
            """,
            GraphText.WithSubjects(repo)
        );

        repo = server.ShowBranch(repo, "dev", false);
        Assert.AreEqual(
            """
            ┣┬┺      Merge branch 'feat' into main
            ┣──╮     Merge branch 'dev' into main
            ┃  ├┺─┺  Dev work
            ┣  │     Third
            ┣┴─╯     Second
            ┗        Initial
            """,
            GraphText.WithSubjects(repo)
        );

        repo = server.ShowBranch(repo, "feat", false);
        Assert.AreEqual(
            """
            ┣─┺────╮   Merge branch 'feat' into main
            ┃      ├┺  Feature work
            ┣──╮   │   Merge branch 'dev' into main
            ┃  ├┺─┺│   Dev work
            ┣  │   │   Third
            ┣──┴───╯   Second
            ┗          Initial
            """,
            GraphText.WithSubjects(repo)
        );

        // Hiding dev leaves feat shown, and dev is a more marker again
        repo = server.HideBranch(repo, "dev");
        Assert.AreEqual(
            """
            ┣─┺╮   Merge branch 'feat' into main
            ┃  ├┺  Feature work
            ┣╮ │   Merge branch 'dev' into main
            ┣  │   Third
            ┣┴─╯   Second
            ┗      Initial
            """,
            GraphText.WithSubjects(repo)
        );

        // Hiding all branches goes back to main only
        repo = server.HideBranch(repo, "feat", hideAllBranches: true);
        Assert.AreEqual(
            """
            ┣┬┺  Merge branch 'feat' into main
            ┣╮   Merge branch 'dev' into main
            ┣    Third
            ┣╯   Second
            ┗    Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // 'Show all recent branches' takes the branches with the newest tips. feat has a newer tip
    // than dev, so it is the one shown when only one more branch is asked for.
    [TestMethod]
    public async Task TestShowAllRecentBranches()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllRecent, 2);

        Assert.AreEqual(
            """
            ┣─┺╮   Merge branch 'feat' into main
            ┃  ├┺  Feature work
            ┣╮ │   Merge branch 'dev' into main
            ┣  │   Third
            ┣┴─╯   Second
            ┗      Initial
            """,
            GraphText.WithSubjects(repo)
        );
    }

    // main with a dev branch, branched out of 'Second' and merged back into 'Merge branch dev'
    static RepoBuilder BranchAndMerge() =>
        new RepoBuilder()
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d2")
            .Commit("d2", "Dev work 2", "d1")
            .Commit("d1", "Dev work 1", "c2")
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true)
            .BranchWithRemote("dev", "d2");

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

    // 'Shared work' could belong to either feat-a or feat-b, so it is ambiguous
    static RepoBuilder Ambiguous() =>
        new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1");
}
