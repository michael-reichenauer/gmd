using gmd.Cui;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// One row of the log view: the graph, the stash and current and ahead/behind markers, then the
// subject with its branch tips and tags, and finally the sid, author and time columns that are
// dropped as the view narrows. RepoWriter names no Terminal.Gui type and reads only the shown repo
// and its graph, so a page of rows can be drawn and asserted as a picture.
//
// The widths below are measured against Fixture(), not calculated: the arm is chosen by
// commitWidth = width + 1 - (graphWidth + 3), so the same pane sits in different arms for
// different repos, and showing a branch can push a row down an arm by widening the graph.
[TestClass]
public class RepoWriterTest
{
    // The fixture's graph is 4 columns wide, which is what puts the arm boundaries at the widths
    // used below. Asserted so that a change to the fixture fails here rather than quietly moving
    // every width test into a neighbouring arm.
    [TestMethod]
    public async Task TestTheFixtureGraphIsFourColumnsWide()
    {
        Assert.AreEqual(4, (await View()).Graph.Width);
    }

    [TestMethod]
    public async Task TestAFullWidthRowHasEveryColumn()
    {
        Assert.AreEqual(
            """
            ┣┬┺ ● Merge branch 'dev' into main                                      (^)(● main) c30000 Test Author    24-10-15 12:00
            ┣     Second                                                                        c20000 Test Author    24-10-15 11:58
            ┗╯    Initial                                                                       c10000 Test Author    24-10-15 11:57
            """,
            Page(await View(), 120)
        );
    }

    // The four arms of ColumnWidths, at the width each one starts and ends at

    [TestMethod]
    public async Task TestTheWidestArmKeepsTheSidAndTheFullAuthorAndTime()
    {
        StringAssert.EndsWith(FirstRow(await View(), 116), "c30000 Test Author    24-10-15 12:00");
    }

    [TestMethod]
    public async Task TestOneColumnNarrowerCutsTheAuthorAndTheTime()
    {
        StringAssert.EndsWith(FirstRow(await View(), 115), "c30000 Test Auth 24-10-15");
        StringAssert.EndsWith(FirstRow(await View(), 106), "c30000 Test Auth 24-10-15", "Still the same arm");
    }

    [TestMethod]
    public async Task TestNarrowerStillDropsTheSid()
    {
        StringAssert.EndsWith(FirstRow(await View(), 105), "Test Auth 24-10-15");
        StringAssert.EndsWith(FirstRow(await View(), 76), "Test Auth 24-10-15", "Still the same arm");
    }

    [TestMethod]
    public async Task TestTheNarrowestArmDropsTheSidAuthorAndTime()
    {
        Assert.AreEqual(
            "┣┬┺ ● Merge branch 'dev' into main                              (^)(● main)",
            FirstRow(await View(), 75)
        );
    }

    // The subject marks what it cut with the '┅' that the rest of the UI and gmd/doc/help.md use
    [TestMethod]
    public async Task TestALongSubjectIsCutWithAnEllipsis()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", new string('x', 200))
            .BranchWithRemote("main", "c1", isCurrent: true)
            .ViewRepoAsync();

        StringAssert.Contains(FirstRow(new FakeViewRepo(repo), 120), "x┅(^)(● main)");
    }

    // Characterization, and a wart: the sid, author and time columns are cut by Txt
    // (RepoWriter.cs:328) with a plain text[..width] and no '┅', so a shortened time reads as if it
    // were meant to be a date and a shortened author as if that were their name. Pinned rather
    // than changed — the marker would cost a column the arm has already run out of.
    [TestMethod]
    public async Task TestACutAuthorAndTimeAreNotMarkedAsCut()
    {
        var row = FirstRow(await View(), 115);

        StringAssert.EndsWith(row, "Test Auth 24-10-15");
        Assert.IsFalse(row.EndsWith('┅'), "The time is cut to look like a date, with nothing saying so");
    }

    // Which rows are drawn, i.e. the window the view asks for

    [TestMethod]
    public async Task TestOnlyTheAskedForRowsAreDrawn()
    {
        Assert.AreEqual(
            """
            ┣     Second                                                                        c20000 Test Author    24-10-15 11:58
            ┗╯    Initial                                                                       c10000 Test Author    24-10-15 11:57
            """,
            Page(await View(), 120, firstRow: 1, count: 2)
        );
    }

    [TestMethod]
    public async Task TestACountBeyondTheEndIsClamped()
    {
        Assert.AreEqual(3, Rows(await View(), 120, count: 99).Count);
    }

    // The view can ask for a row a refresh has since removed, so this must clamp rather than
    // throw — it is read straight out of ViewCommits
    [TestMethod]
    public async Task TestACurrentIndexBeyondTheEndIsClamped()
    {
        Assert.AreEqual(3, Rows(await View(), 120, currentIndex: 99).Count);
        Assert.AreEqual(3, Rows(await View(), 120, currentIndex: -5).Count);
    }

    [TestMethod]
    public async Task TestNothingIsDrawnForACountOfZero()
    {
        Assert.AreEqual(0, Rows(await View(), 120, count: 0).Count);
    }

    [TestMethod]
    public void TestNothingIsDrawnForARepoWithNoCommitsInView()
    {
        Assert.AreEqual(0, Rows(new FakeViewRepo(Repo.Empty), 120).Count);
    }

    // The markers between the graph and the subject

    [TestMethod]
    public async Task TestTheCurrentCommitIsMarked()
    {
        StringAssert.Contains(FirstRow(await View(), 120), "┺ ● Merge branch");
    }

    [TestMethod]
    public async Task TestADetachedHeadIsMarkedWithAStarRatherThanADot()
    {
        var rows = Page(await Detached(), 120).Split('\n');

        StringAssert.Contains(rows[1], "┺ * Initial");
        StringAssert.Contains(rows[1], "(● DETACHED)");
    }

    [TestMethod]
    public async Task TestAheadAndBehindCommitsAreMarkedAndColored()
    {
        // Hoovering a branch turns the current row's highlight off, which would otherwise give the
        // top row a background and color its spaces along with its text
        var rows = Rows(await Diverged(), 120, hooverBranch: "main", hooverIndex: 0);

        StringAssert.Contains(rows[0].ToString(), "▼Remote work");
        StringAssert.Contains(rows[1].ToString(), "▲Local work");

        // The subject takes the color of its marker: bright blue behind, bright green ahead
        StringAssert.Contains(TextColors.Of(rows[0]), "bbbbbbb bbbb");
        StringAssert.Contains(TextColors.Of(rows[1]), "gggggg gggg");
    }

    [TestMethod]
    public async Task TestTheUncommittedRowIsMarkedAndColored()
    {
        var repo = await Fixture().WithStatus(modified: 2).ViewRepoAsync();
        var row = Rows(new FakeViewRepo(repo), 120)[0];

        StringAssert.Contains(row.ToString(), "©2 uncommitted changes");
        StringAssert.Contains(TextColors.Of(row), "yyyyyyyy", "Bright yellow while there is nothing conflicted");
    }

    [TestMethod]
    public async Task TestAConflictedUncommittedRowIsBrightRed()
    {
        var repo = await Fixture().WithStatus(modified: 2, conflicted: 1).ViewRepoAsync();
        var row = Rows(new FakeViewRepo(repo), 120)[0];

        StringAssert.Contains(row.ToString(), "CONFLICTS: 1, 3 uncommitted changes");
        StringAssert.Contains(TextColors.Of(row), "rrrrrrrr");
    }

    [TestMethod]
    public async Task TestACommitWithAStashIsMarked()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .Stash("stash@{0}", "c2")
            .ViewRepoAsync();

        StringAssert.Contains(FirstRow(new FakeViewRepo(repo), 120), "┺ß● Second");
    }

    // The branch tips, i.e. the '(name)' after the subject saying which branches end at this commit

    [TestMethod]
    public async Task TestALocalAndRemoteBranchOnTheSameCommitAreCombined()
    {
        StringAssert.Contains(FirstRow(await View(), 120), "(^)(● main)");
    }

    // Diverged, so each names itself: '^' for the remote, the current marker for the local
    [TestMethod]
    public async Task TestADivergedLocalAndRemoteEachNameThemselves()
    {
        var rows = Page(await Diverged(), 120).Split('\n');

        StringAssert.Contains(rows[0], "(^/main)");
        StringAssert.Contains(rows[1], "(● main)");
    }

    [TestMethod]
    public async Task TestALocalOnlyBranchNamesItselfAlone()
    {
        var repo = await new RepoBuilder()
            .Commit("d1", "Work", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("main", "c1", isCurrent: true)
            .LocalBranch("dev", "d1")
            .ViewRepoAsync();

        StringAssert.Contains(FirstRow(new FakeViewRepo(repo), 120), "(● main)");
    }

    // A branch git no longer has, inferred rather than read, is named with a '~'
    [TestMethod]
    public async Task TestABranchGitNoLongerHasIsNamedWithATilde()
    {
        StringAssert.Contains(FirstRow(await Detached(), 120), "(~branch)");
    }

    // A commit two branches could equally own is named as ambiguous, which is what the '*' filter
    // searches for
    [TestMethod]
    public async Task TestAnAmbiguousTipIsNamedAsAmbiguous()
    {
        var repo = await new RepoBuilder()
            .Commit("b2", "Two", "a1")
            .Commit("b1", "One", "a1")
            .Commit("a1", "Shared")
            .LocalBranch("one", "b1", isCurrent: true)
            .LocalBranch("two", "b2")
            .ViewRepoAsync(ShowBranches.AllActive);

        StringAssert.Contains(Page(new FakeViewRepo(repo), 120), "(~ambiguous)");
    }

    [TestMethod]
    public async Task TestTagsAreDrawnAfterTheBranchTips()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .Tag("v1.0", "c2")
            .Tag("v0.9", "c1")
            .ViewRepoAsync();

        var rows = Page(new FakeViewRepo(repo), 120).Split('\n');

        StringAssert.Contains(rows[0], "(^)(● main)[v1.0]");
        StringAssert.Contains(rows[1], "[v0.9]");
    }

    // Highlighting and selection are backgrounds rather than colors, so the row keeps its own
    // colors and only what is behind them changes

    [TestMethod]
    public async Task TestTheCurrentRowIsHighlighted()
    {
        var rows = Rows(await View(), 120, currentIndex: 1);

        Assert.IsFalse(IsHighlighted(rows[0]));
        Assert.IsTrue(IsHighlighted(rows[1]));
    }

    // The highlight is put on the subject, sid, author and time, not on the row: the graph is
    // built separately and the two are joined afterwards, so the graph keeps its branch colors on
    // the terminal's own background while the rest of the row is lifted onto the highlight
    [TestMethod]
    public async Task TestTheGraphColumnIsNotHighlightedWithItsRow()
    {
        var row = Rows(await View(), 120, currentIndex: 0)[0];

        Assert.AreNotEqual(
            Color.Dark.Foreground,
            row.Fragments[0].Color.Background,
            "The row opens with a graph rune, which keeps the terminal's own background"
        );
        Assert.AreEqual(
            Color.Dark.Foreground,
            row.Fragments[^1].Color.Background,
            "It ends with the time column, which is on the highlight"
        );
    }

    // Hoovering a branch is the other way of saying where the user is, so the current row stops
    // being highlighted for as long as a branch is hoovered
    [TestMethod]
    public async Task TestTheCurrentRowIsNotHighlightedWhileABranchIsHoovered()
    {
        var rows = Rows(await View(), 120, currentIndex: 1, hooverBranch: "dev", hooverIndex: 1);

        Assert.IsFalse(IsHighlighted(rows[1]));
    }

    [TestMethod]
    public async Task TestSelectedRowsAreDrawnAsSelected()
    {
        var rows = Rows(await View(), 120, selection: new Selection(0, 0, 0, 1, 0));

        Assert.IsTrue(IsSelected(rows[0]));
        Assert.IsTrue(IsSelected(rows[1]));
        Assert.IsFalse(IsSelected(rows[2]));
    }

    // A selected row is marked with a '|' where the current marker would otherwise be
    [TestMethod]
    public async Task TestASelectedRowIsMarkedInsteadOfTheCurrentCommit()
    {
        var rows = Page(await View(), 120, selection: new Selection(0, 0, 0, 1, 0)).Split('\n');

        StringAssert.Contains(rows[0], "┺ | Merge branch");
    }

    // Both are applied to the columns after the graph rather than to the whole row, so the graph
    // keeps its own background either way — see TestTheGraphColumnIsNotHighlightedWithItsRow
    static bool IsHighlighted(Text row) => row.Fragments.Any(f => f.Color.Background == Color.Dark.Foreground);

    static bool IsSelected(Text row) => row.Fragments.Any(f => f.Color.Background == Color.White.Foreground);

    static string Page(FakeViewRepo view, int width, int firstRow = 0, int count = 20, Selection? selection = null) =>
        string.Join("\n", Rows(view, width, firstRow, count, selection: selection).Select(t => t.ToString().TrimEnd()));

    static string FirstRow(FakeViewRepo view, int width) => Rows(view, width)[0].ToString().TrimEnd();

    static IReadOnlyList<Text> Rows(
        FakeViewRepo view,
        int width,
        int firstRow = 0,
        int count = 20,
        int currentIndex = 0,
        string hooverBranch = "",
        int hooverIndex = -1,
        Selection? selection = null
    ) =>
        new RepoWriter(new BranchColorService(new FakeRepoConfig()), new GraphWriter())
            .ToPage(view, firstRow, count, currentIndex, hooverBranch, hooverIndex, width, selection ?? NoSelection)
            .ToList();

    static readonly Selection NoSelection = new Selection(0, 0, 0, 0, 0);

    static async Task<FakeViewRepo> View() => new FakeViewRepo(await Fixture().ViewRepoAsync());

    static async Task<FakeViewRepo> Detached() =>
        new FakeViewRepo(
            await new RepoBuilder()
                .Commit("c2", "Second", "c1")
                .Commit("c1", "Initial")
                .DetachedHead("c1")
                .ViewRepoAsync()
        );

    static async Task<FakeViewRepo> Diverged() =>
        new FakeViewRepo(
            await new RepoBuilder()
                .Commit("r1", "Remote work", "c1")
                .Commit("l1", "Local work", "c1")
                .Commit("c1", "Initial")
                .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "r1", ahead: 1, behind: 1)
                .ViewRepoAsync()
        );

    // A merge of a second branch, so the graph is 4 columns wide and there are branch tips to draw
    static RepoBuilder Fixture() =>
        new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d1")
            .Commit("d1", "Work on dev", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .LocalBranch("dev", "d1");
}
