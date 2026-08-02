using gmd.Cui;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// The hoover is the branch, or the commit, that the pointer or the cursor is on, and is what most
// keys of the repo view act on. These tests drive it over a real graph, i.e. the GraphBranch
// columns GraphCreater lays out, since the whole of the hoover is where those columns are.
//
// The repo used by most of them is ThreeBranches(), whose graph looks like this, with the branch
// columns of each row written out to the right:
//
//   0  ┣─┺────╮   Merge branch 'feat' into main   origin/main(x0), origin/main(x1)
//   1  ┃      ├┺  Feature work                    origin/main(x0), feat(x4)
//   2  ┣──╮   │   Merge branch 'dev' into main    origin/main(x0)
//   3  ┃  ├┺─┺│   Dev work                        origin/main(x0), origin/dev(x2), origin/dev(x3)
//   4  ┣  │   │   Third                           origin/main(x0)
//   5  ┣──┴───╯   Second                          origin/main(x0)
//   6  ┗          Initial                         origin/main(x0)
//
// A branch and its remote are one branch to the user, so they share a primary name and both of
// their columns hoover as the same branch. Row 3 has such a pair and is where moving left and
// right has something to get wrong.
[TestClass]
public class HooverTest
{
    [TestMethod]
    public async Task TestNoHooverToStartWith()
    {
        var (_, hoover) = await ThreeBranchesGraph();

        Assert.IsFalse(hoover.IsBranch);
        Assert.AreEqual("", hoover.BranchPrimaryName);
        Assert.AreEqual(-1, hoover.RowIndex);
    }

    [TestMethod]
    public async Task TestSetBranch()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var dev = graph.GetRowBranches(3)[1];

        Assert.IsTrue(hoover.SetBranch(dev, 3, 3));

        Assert.IsTrue(hoover.IsBranch);
        Assert.AreEqual("origin/dev", hoover.BranchPrimaryName);
        Assert.AreEqual("origin/dev", hoover.BranchName);
        Assert.AreEqual(3, hoover.RowIndex);
        Assert.AreEqual(4, hoover.ColumnIndex); // The graph draws two runes per branch column
        Assert.AreEqual(3, hoover.CurrentCommitIndex);
    }

    // Only a move needs a redraw, and the mouse reports a position for every character it passes,
    // so most of what the mouse reports is the hoover it already has.
    [TestMethod]
    public async Task TestSetSameBranchAgainIsNotAMove()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var dev = graph.GetRowBranches(3)[1];
        hoover.SetBranch(dev, 3, 3);

        Assert.IsFalse(hoover.SetBranch(dev, 3, 3));
    }

    // The two columns of a branch and its remote are the same branch, but not the same position,
    // so moving between them is still a move.
    [TestMethod]
    public async Task TestSetOtherColumnOfSameBranchIsAMove()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var (devRemote, devLocal) = (graph.GetRowBranches(3)[1], graph.GetRowBranches(3)[2]);
        hoover.SetBranch(devRemote, 3, 3);

        Assert.IsTrue(hoover.SetBranch(devLocal, 3, 3));
        Assert.AreEqual("origin/dev", hoover.BranchPrimaryName);
        Assert.AreEqual(6, hoover.ColumnIndex);
    }

    [TestMethod]
    public async Task TestSetCommitLeavesTheBranch()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetBranch(graph.GetRowBranches(3)[1], 3, 3);

        hoover.SetCommit(3, graph.Width + 2, 3);

        Assert.IsFalse(hoover.IsBranch);
        Assert.AreEqual("", hoover.BranchPrimaryName);
        Assert.AreEqual(3, hoover.RowIndex);
        Assert.AreEqual(graph.Width + 2, hoover.ColumnIndex);
    }

    [TestMethod]
    public async Task TestClear()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetBranch(graph.GetRowBranches(3)[1], 3, 3);

        Assert.IsTrue(hoover.Clear());
        Assert.IsFalse(hoover.IsBranch);
        Assert.AreEqual(-1, hoover.RowIndex);
        Assert.AreEqual(-1, hoover.ColumnIndex);
        Assert.AreEqual(-1, hoover.CurrentCommitIndex);

        Assert.IsFalse(hoover.Clear()); // Already cleared, so nothing to redraw
    }

    [TestMethod]
    public async Task TestColumnOf()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var row3 = graph.GetRowBranches(3);

        Assert.AreEqual(-1, hoover.ColumnOf(row3)); // No hoover branch

        hoover.SetBranch(row3[1], 3, 3);
        Assert.AreEqual(1, hoover.ColumnOf(row3));

        Assert.AreEqual(-1, hoover.ColumnOf(graph.GetRowBranches(4))); // dev is not on that row
    }

    // Left from the commit enters the graph at its right most branch, which is how the cursor gets
    // onto a branch at all, since a commit is hoovered until then.
    [TestMethod]
    public async Task TestMoveLeftFromCommitTakesRightMostBranch()
    {
        var (graph, hoover) = await ThreeBranchesGraph();

        var branch = hoover.NextLeft(graph.GetRowBranches(3));

        Assert.AreEqual("origin/dev", branch!.B.PrimaryName);
        Assert.AreEqual(3, branch.X); // The right most of the two dev columns
    }

    [TestMethod]
    public async Task TestMoveLeftToTheBranchLeftOfTheHoovered()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var row3 = graph.GetRowBranches(3);
        hoover.SetBranch(row3[1], 3, 3);

        Assert.AreEqual("origin/main", hoover.NextLeft(row3)!.B.PrimaryName);
    }

    // Nothing is left of the left most branch, and moving left off the graph does nothing, unlike
    // moving right off it.
    [TestMethod]
    public async Task TestMoveLeftAtLeftMostBranchStaysThere()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var row3 = graph.GetRowBranches(3);
        hoover.SetBranch(row3[0], 3, 3);

        Assert.IsNull(hoover.NextLeft(row3));
    }

    // A branch the cursor moved off of, e.g. because the row moved, is not on the row any more, so
    // left starts over from the right side rather than doing nothing.
    [TestMethod]
    public async Task TestMoveLeftWhenHooveredBranchIsNotOnTheRow()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetBranch(graph.GetRowBranches(1)[1], 1, 1); // feat, which is only on row 1

        Assert.AreEqual("origin/dev", hoover.NextLeft(graph.GetRowBranches(3))!.B.PrimaryName);
    }

    [TestMethod]
    public async Task TestMoveRightToTheBranchRightOfTheHoovered()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var row3 = graph.GetRowBranches(3);
        hoover.SetBranch(row3[0], 3, 3);

        var branch = hoover.NextRight(row3);

        Assert.AreEqual("origin/dev", branch!.B.PrimaryName);
        Assert.AreEqual(2, branch.X); // The left most of the two dev columns
    }

    // Right leaves a branch behind by its *last* column, so a branch drawn in two columns, i.e. a
    // branch and its remote, is stepped over in one move rather than hoovered twice.
    [TestMethod]
    public async Task TestMoveRightLeavesBothColumnsOfABranchAndItsRemote()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var row3 = graph.GetRowBranches(3);
        hoover.SetBranch(row3[1], 3, 3); // The first of the two dev columns

        Assert.IsNull(hoover.NextRight(row3)); // Nothing right of dev, so the commit is selected
    }

    [TestMethod]
    public async Task TestMoveRightAtRightMostBranchLeavesTheGraph()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        var row3 = graph.GetRowBranches(3);
        hoover.SetBranch(row3[2], 3, 3);

        Assert.IsNull(hoover.NextRight(row3));
    }

    [TestMethod]
    public async Task TestMoveRightWhenHooveredBranchIsNotOnTheRow()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetBranch(graph.GetRowBranches(1)[1], 1, 1); // feat, which is only on row 1

        Assert.IsNull(hoover.NextRight(graph.GetRowBranches(3)));
    }

    // Moving the row up or down keeps the hoover on its branch wherever that branch is drawn on
    // the new row, so holding a cursor key follows the branch rather than a column.
    [TestMethod]
    public async Task TestLocateKeepsTheSameBranchOnTheNewRow()
    {
        var (graph, _) = await ThreeBranchesGraph();

        // main is the second column of row 0 and the first of row 1, and stays hoovered
        var branch = Hoover.Locate(graph.GetRowBranches(1), "origin/main", 1);

        Assert.AreEqual("origin/main", branch.B.PrimaryName);
    }

    // A branch that is not on the new row hands the hoover to whichever branch is at the column it
    // had, so the hoover keeps going down the graph instead of being dropped.
    [TestMethod]
    public async Task TestLocateFallsBackToTheBranchAtTheSameColumn()
    {
        var (graph, _) = await ThreeBranchesGraph();

        // feat is the second column of row 1, and row 3 has three columns
        var branch = Hoover.Locate(graph.GetRowBranches(3), "feat", 1);

        Assert.AreEqual("origin/dev", branch.B.PrimaryName);
        Assert.AreEqual(2, branch.X);
    }

    [TestMethod]
    public async Task TestLocateClampsToTheBranchesTheNewRowHas()
    {
        var (graph, _) = await ThreeBranchesGraph();
        var row2 = graph.GetRowBranches(2); // Only main

        Assert.AreEqual("origin/main", Hoover.Locate(row2, "feat", 2).B.PrimaryName); // Beyond the row
        Assert.AreEqual("origin/main", Hoover.Locate(row2, "feat", -1).B.PrimaryName); // No column at all
    }

    // Drawing is where the view notices that the current row moved without the hoover, e.g. after
    // a menu command scrolled to another commit.
    [TestMethod]
    public async Task TestFollowCurrentIndexKeepsABranchThatIsOnTheNewRow()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetBranch(graph.GetRowBranches(3)[0], 3, 3); // main, which is on every row

        Assert.IsFalse(hoover.FollowCurrentIndex(5, graph.GetRowBranches(5)));

        Assert.AreEqual("origin/main", hoover.BranchPrimaryName);
        Assert.AreEqual(5, hoover.RowIndex);
    }

    // The branch is given up, but the row still follows, so the commit of the new row is hoovered.
    // Note that this leaves the row set while the column and the current row index are cleared.
    [TestMethod]
    public async Task TestFollowCurrentIndexGivesUpABranchThatIsGone()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetBranch(graph.GetRowBranches(1)[1], 1, 1); // feat, which is only on row 1

        Assert.IsTrue(hoover.FollowCurrentIndex(3, graph.GetRowBranches(3)));

        Assert.IsFalse(hoover.IsBranch);
        Assert.AreEqual(3, hoover.RowIndex);
        Assert.AreEqual(-1, hoover.ColumnIndex);
        Assert.AreEqual(-1, hoover.CurrentCommitIndex);
    }

    [TestMethod]
    public async Task TestFollowCurrentIndexDoesNothingWhileTheRowIsTheSame()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetBranch(graph.GetRowBranches(1)[1], 1, 1);

        Assert.IsFalse(hoover.FollowCurrentIndex(1, graph.GetRowBranches(1)));
        Assert.AreEqual("feat", hoover.BranchPrimaryName);
    }

    [TestMethod]
    public async Task TestFollowCurrentIndexDoesNothingWithACommitHoovered()
    {
        var (graph, hoover) = await ThreeBranchesGraph();
        hoover.SetCommit(1, graph.Width + 2, 1);

        Assert.IsFalse(hoover.FollowCurrentIndex(3, graph.GetRowBranches(3)));
        Assert.AreEqual(1, hoover.RowIndex); // A hoovered commit is left where it is
    }

    static async Task<(Graph, Hoover)> ThreeBranchesGraph()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);
        var graph = new GraphCreater(new BranchColorService(new FakeRepoConfig())).Create(repo);
        return (graph, new Hoover());
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
