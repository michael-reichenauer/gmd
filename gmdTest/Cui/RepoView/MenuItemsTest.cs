using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmd.Server;
using gmdTest.Fixtures;
using GitOp = gmd.Git.GitOperation;

namespace gmdTest.Cui.RepoView;

// What the branch and repo menus offer, and which of it is greyed out. The item builders are on
// the interfaces and read only the shown repo, so they can be asserted as a list without a driver.
//
// Worth pinning because Menu.OnCursorDown skips a disabled item: what is greyed decides how many
// key presses reach an item, which is why the end-to-end menu tests have to count their moves
// against the fixture rather than against the source.
[TestClass]
public class MenuItemsTest
{
    [TestMethod]
    public async Task TestTheBranchMenuOfAnotherBranchOffersEverything()
    {
        Assert.AreEqual(
            """
            Switch/Checkout to Branch  [S]
            Merge to main  [E]
            Merge from main  [Shift-E]
            Rebase and push on >  (disabled)
            Hide Branch  [H]
            Pull/Update  [U]  (disabled)
            Push  [P]
            Create Branch ...  [B]
            Rename Branch ...
            Delete Branch ...
            Diff Branch to >  [D]
            Change Branch Color  [G]
            ---
            Show/Open Branch >  [Shift →]
            Pull/Update All Branches  [Shift-U]
            Push All Branches  [Shift-P]
            Set Commit Branch Manually ...
            Repo Menu >
            """,
            Items(BranchMenuOf(await ViewOf(Fixture())).GetBranchMenuItems("dev"))
        );
    }

    // The current branch cannot be switched to, merged with itself, renamed or deleted, and 'main'
    // is what the branch structure is worked out from, so its color is not the user's to change
    [TestMethod]
    public async Task TestTheCurrentBranchOffersMuchLess()
    {
        Assert.AreEqual(
            """
            Switch/Checkout to Branch  [S]  (disabled)
            Merge from >  [E]  (disabled)
            Merge to >  [Shift-E]  (disabled)
            Rebase and push on >  (disabled)
            Hide Branch  [H]
            Pull/Update  [U]  (disabled)
            Push  [P]
            Create Branch ...  [B]
            Rename Branch ...  (disabled)
            Delete Branch ...  (disabled)
            Diff Branch to >  [D]  (disabled)
            Change Branch Color  [G]  (disabled)
            ---
            Show/Open Branch >  [Shift →]
            Pull/Update All Branches  [Shift-U]
            Push All Branches  [Shift-P]
            Set Commit Branch Manually ...
            Repo Menu >
            """,
            Items(BranchMenuOf(await ViewOf(Fixture())).GetBranchMenuItems("main"))
        );
    }

    // The same menu shown from somewhere that already offers those ways out of it
    [TestMethod]
    public async Task TestTheLimitedBranchMenuDropsTheWaysOutOfIt()
    {
        var menu = BranchMenuOf(await ViewOf(Fixture()));

        var dropped = Titles(menu.GetBranchMenuItems("dev")).Except(Titles(menu.GetBranchMenuItems("dev", true)));

        CollectionAssert.AreEqual(new[] { "Show/Open Branch", "Repo Menu" }, dropped.ToArray());
    }

    // The point of the class comment: the same menu is a different number of key presses away from
    // its items depending on the branch, because the cursor skips what is greyed out
    [TestMethod]
    public async Task TestHowMuchIsEnabledDependsOnTheBranch()
    {
        var menu = BranchMenuOf(await ViewOf(Fixture()));

        Assert.AreEqual(15, EnabledCount(menu.GetBranchMenuItems("dev")));
        Assert.AreEqual(8, EnabledCount(menu.GetBranchMenuItems("main")));
    }

    [TestMethod]
    public async Task TestTheShownBranchesAreListedWithTheCurrentOneMarked()
    {
        var view = await ViewOf(Fixture(), "dev");

        Assert.AreEqual(
            """
            ●   main >
                dev >
            ---
            Show/Open Branch >  [Shift →]
            Hide All Branches
            """,
            Items(BranchMenuOf(view).GetShownBranchesItems())
        );
    }

    // The order is the current branch, then the branch it was branched from and so on, and the
    // rest by name after those, rather than the order the graph happens to draw them in
    [TestMethod]
    public async Task TestTheShownBranchesStartWithTheCurrentBranchAndItsAncestors()
    {
        var view = await ViewOf(BranchedFixture(), "dev", "feature", "alpha");

        Assert.AreEqual(
            """
            ●   dev >
                main >
                alpha >
                feature >
            ---
            Show/Open Branch >  [Shift →]
            Hide All Branches
            """,
            Items(BranchMenuOf(view).GetShownBranchesItems())
        );
    }

    // The ways of choosing a branch to show: the branches of the commit the cursor is on, then the
    // sub menus of all of them
    [TestMethod]
    public async Task TestTheShowBranchItemsOfferTheCommitsBranchesAndThenAllOfThem()
    {
        // The texts are indented, which is how the list is laid out under the branch it starts with
        var items = Titles(BranchMenuOf(await ViewOf(Fixture())).GetShowBranchItems()).Select(t => t.Trim()).ToList();

        CollectionAssert.AreEqual(
            new[] { "Recent", "Active", "My Active", "Active and Deleted" },
            items.Skip(1).ToArray()
        );
        StringAssert.Contains(items[0], "dev", "The branch of the commit the cursor is on");
    }

    // A stopped operation heads the repo menu, since it is the most urgent thing about the repo
    // while it lasts, and self-hides when there is none

    [TestMethod]
    public async Task TestNoOperationAddsNothingToTheRepoMenu()
    {
        Assert.AreEqual("", Items(RepoMenuOf(await ViewOf(Fixture())).GetOperationItems()));
    }

    [TestMethod]
    public async Task TestAStoppedRebaseIsNamedWithHowFarItHasGot()
    {
        var view = await ViewOf(
            Fixture()
                .WithStatus(
                    conflicted: 2,
                    operation: GitOp.Rebase,
                    operationBranchName: "dev",
                    operationStep: 3,
                    operationTotal: 7,
                    isFinishedByCommit: false
                )
        );

        Assert.AreEqual(
            """
            --- Rebase 'dev' (3 of 7)  ·  2 conflicts ---
            Continue Rebase
            Skip This Commit
            Abort Rebase
            ---
            """,
            Items(RepoMenuOf(view).GetOperationItems())
        );
    }

    // A merge has no 'Continue' — committing is what finishes one — and nothing to skip. They are
    // left out rather than greyed, since neither could ever apply to a merge.
    [TestMethod]
    public async Task TestAStoppedMergeOffersOnlyAbort()
    {
        var view = await ViewOf(Fixture().WithStatus(conflicted: 1, operation: GitOp.Merge, isFinishedByCommit: true));

        Assert.AreEqual(
            """
            --- Merge  ·  1 conflict ---
            Abort Merge
            ---
            """,
            Items(RepoMenuOf(view).GetOperationItems())
        );
    }

    [TestMethod]
    public async Task TestTheOperationItemsHeadTheRepoMenu()
    {
        var view = await ViewOf(Fixture().WithStatus(conflicted: 1, operation: GitOp.Merge, isFinishedByCommit: true));

        StringAssert.StartsWith(Items(RepoMenuOf(view).GetRepoMenuItems()), "--- Merge  ·  1 conflict ---");
    }

    [TestMethod]
    public async Task TestTheRepoMenuIsTheUsualListWhenNothingIsInProgress()
    {
        Assert.AreEqual(
            """
            Pull/Update All Branches  [Shift-U]
            Push All Branches  [Shift-P]
            Search/Filter ...  [F]
            Refresh/Reload  [R]
            Clean/Restore Working Folder
            Open/Clone/Init Repo >  [O]
            Config ...
            Help ...  [?, F1]
            About ...
            Quit  [Q, Esc]
            """,
            Items(RepoMenuOf(await ViewOf(Fixture())).GetRepoMenuItems())
        );
    }

    // Uncommitted changes block everything that would move the working tree from under them
    [TestMethod]
    public async Task TestUncommittedChangesDisableThePullAndPushItems()
    {
        var items = Items(RepoMenuOf(await ViewOf(Fixture().WithStatus(modified: 1))).GetRepoMenuItems());

        StringAssert.Contains(items, "Pull/Update All Branches  [Shift-U]  (disabled)");
        StringAssert.Contains(items, "Push All Branches  [Shift-P]  (disabled)");
    }

    [TestMethod]
    public async Task TestNoNewReleaseAddsNothingToTheMenu()
    {
        Assert.AreEqual("", Items(RepoMenuOf(await ViewOf(Fixture())).GetNewReleaseItems()));
    }

    // One line per item: the text, its shortcut, and '(disabled)' where the menu would grey it out.
    // Mirrors the rule in Menu.Show, which is IsDisabled, CanExecute and an empty sub menu.
    static string Items(IEnumerable<MenuItem> items) =>
        string.Join(
            "\n",
            items.Select(i =>
            {
                // A separator is never something to pick, so it is drawn as one rather than as the
                // disabled item its 'CanExecute' returning false would otherwise make it
                if (i is MenuSeparator)
                    return i.Text == "" ? "---" : $"--- {i.Text} ---";

                var shortcut = i.Shortcut == "" ? "" : $"  [{i.Shortcut}]";
                return $"{i.Text}{(i is SubMenu ? " >" : "")}{shortcut}{(IsDisabled(i) ? "  (disabled)" : "")}";
            })
        );

    static bool IsDisabled(MenuItem i) =>
        i.IsDisabled || !(i.CanExecute?.Invoke() ?? true) || (i is SubMenu sm && !sm.Children.Any());

    static IReadOnlyList<string> Titles(IEnumerable<MenuItem> items) =>
        items.Where(i => i is not MenuSeparator).Select(i => i.Text).ToList();

    static int EnabledCount(IEnumerable<MenuItem> items) => items.Count(i => i is not MenuSeparator && !IsDisabled(i));

    // The show/hide branch items ask the server which branches a commit is on, which is a pure
    // function of the repo, so the real one is used
    static async Task<FakeViewRepo> ViewOf(RepoBuilder builder, params string[] showBranches) =>
        new FakeViewRepo(await builder.ViewRepoAsync(showBranches), builder.Config, builder.NewServer());

    static IBranchMenu BranchMenuOf(FakeViewRepo view) => new BranchMenu(RepoMenuOf(view), view);

    // Only OperationName and OperationSummary are called while the items are built, and both read
    // nothing but repo.Repo.Status, so the rest of RepoCommands is never reached
    static IRepoMenu RepoMenuOf(FakeViewRepo view) =>
        new RepoMenu(
            view,
            new RepoCommands(view, null!, null!, null!, null!, null!, null!, null!, new Config(), null!, null!),
            new Config(),
            null!
        );

    // 'dev' is current and branched from 'main', 'feature' from 'dev' and 'alpha' from 'main',
    // so the branch order the graph draws is not the order the chain of the current branch is
    static RepoBuilder BranchedFixture() =>
        new RepoBuilder()
            .Commit("f1", "Work on feature", "d1")
            .Commit("a1", "Work on alpha", "c2")
            .Commit("d1", "Work on dev", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2")
            .LocalBranch("dev", "d1", isCurrent: true)
            .LocalBranch("feature", "f1")
            .LocalBranch("alpha", "a1");

    static RepoBuilder Fixture() =>
        new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d1")
            .Commit("d1", "Work on dev", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .LocalBranch("dev", "d1");
}
