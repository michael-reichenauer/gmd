using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmdTest.Fixtures;
using GitOp = gmd.Git.GitOperation;

namespace gmdTest.Cui.RepoView;

// What the key-hint line at the bottom of the log view offers where the cursor is. The hints are a
// function of the shown repo, the hoover, the selection and whether the details are shown, so they
// are asserted as a list, most useful first, with no view.
[TestClass]
public class KeyHintsTest
{
    [TestMethod]
    public async Task TestACommitOffersItsDiffDetailsAndMenuAndTheWaysToOtherBranches()
    {
        var view = await ViewOf(Fixture());

        Assert.AreEqual(
            "d diff  Enter details  m menu  ←→ branch  ⇧→ show branch  f search  b new branch",
            Hints(view)
        );
    }

    // With the details open, Enter closes them
    [TestMethod]
    public async Task TestEnterHidesTheDetailsWhenTheyAreShown()
    {
        var view = await ViewOf(Fixture());

        StringAssert.Contains(Hints(view, isDetailsShown: true), "Enter hide details");
    }

    // Uncommitted changes put committing first, wherever the cursor is, since 'c' commits from any
    // row. On the uncommitted row itself there is no commit to make a branch from.
    [TestMethod]
    public async Task TestUncommittedChangesOfferCommittingFirst()
    {
        var view = await ViewOf(Fixture().WithStatus(modified: 1));

        Assert.AreEqual("c commit  d diff  Enter details  m menu  ←→ branch  ⇧→ show branch  f search", Hints(view));
    }

    // A merge stopped on conflicts: the diff of the uncommitted row is where they are resolved
    [TestMethod]
    public async Task TestConflictsAreResolvedFromTheDiff()
    {
        var view = await ViewOf(Fixture().WithStatus(conflicted: 1, operation: GitOp.Merge, isFinishedByCommit: true));

        StringAssert.StartsWith(Hints(view), "c commit  ⇧m abort…  d resolve  ");
    }

    // A rebase is finished by continuing it, not by a commit, and 'c' offers that instead
    [TestMethod]
    public async Task TestAStoppedRebaseIsContinued()
    {
        var view = await ViewOf(
            Fixture().WithStatus(conflicted: 1, operation: GitOp.Rebase, isFinishedByCommit: false)
        );

        StringAssert.StartsWith(Hints(view), "c continue  ⇧m abort…  d resolve  ");
    }

    // 'p' and 'u' only while the current branch has something to push or pull
    [TestMethod]
    public async Task TestPushIsOfferedOnlyWithSomethingToPush()
    {
        var view = await ViewOf(Ahead());

        StringAssert.Contains(Hints(view), "m menu  p push  ←→ branch");
        Assert.IsFalse(Hints(await ViewOf(Fixture())).Contains("p push"), "Nothing to push when synced");
    }

    [TestMethod]
    public async Task TestPullIsOfferedOnlyWithSomethingToPull()
    {
        var view = await ViewOf(Behind());

        StringAssert.Contains(Hints(view), "m menu  u pull  ←→ branch");
    }

    // Another branch hoovered, named first: what the keys do to it. 'p' pushes it, as the branch
    // menu's Push does, which for 'dev', not on origin yet, publishes it. Enter is offered since
    // this is the tip of 'dev', off 'main': it opens the show/hide menu of the two.
    [TestMethod]
    public async Task TestAHooveredBranchOffersSwitchingMergingAndHidingIt()
    {
        var view = await ViewOf(Ahead().LocalBranch("dev", "d1"), "dev");
        var hoover = HooverOn(view, "dev", "d1");

        Assert.AreEqual(
            "dev:  s switch  e merge  Enter show/hide  m menu  h hide  d diff  p push  b new branch",
            Hints(view, hoover)
        );
    }

    // The current branch hoovered: merging from and to it, and pushing it. The main branch is
    // always shown, so it has no 'h hide', and no Enter at its tip, where it is the only branch.
    [TestMethod]
    public async Task TestTheCurrentBranchHooveredOffersMergingFromAndToItAndPushing()
    {
        var view = await ViewOf(Ahead());
        var hoover = HooverOn(view, "main", "l1");

        Assert.AreEqual("main:  e merge from  ⇧e merge to  m menu  d diff  p push  b new branch", Hints(view, hoover));
    }

    // Merging and diffing a branch need a clean working tree, so with changes they give way to
    // committing. Pushing it does not, which for 'feature', not on origin yet, publishes it.
    [TestMethod]
    public async Task TestUncommittedChangesLeaveOutMergingAndDiffingABranch()
    {
        var view = await ViewOf(Fixture().LocalBranch("feature", "d1").WithStatus(modified: 1), "feature");
        view.CurrentIndex = view.Repo.CommitById[RepoBuilder.Sha("d1")].ViewIndex;
        var hoover = HooverOn(view, "feature", "d1");

        Assert.AreEqual(
            "feature:  s switch  Enter show/hide  m menu  h hide  p push  c commit  b new branch",
            Hints(view, hoover)
        );
    }

    // Once a branch has been shown or hidden, Backspace is offered for going back, next to the key
    // that shows branches and, on a hoovered branch, next to the one that hides it
    [TestMethod]
    public async Task TestBackspaceIsOfferedOnceABranchHasBeenShownOrHidden()
    {
        var view = await ViewOf(Fixture().LocalBranch("feature", "d1"), "feature");
        Assert.IsFalse(Hints(view).Contains("Bksp"), "Nothing to undo yet");

        view.ShownHistory.Add(["main"], ["main", "feature"], "Show", "'feature'", "feature");
        StringAssert.Contains(Hints(view), "⇧→ show branch  Bksp undo show  f search");

        view.ShownHistory.Add(["main", "feature"], ["main"], "Hide", "'feature'", "feature");
        StringAssert.Contains(Hints(view, HooverOn(view, "feature", "d1")), "h hide  Bksp undo hide  ");
    }

    // Several rows selected with Shift-↑↓: the range is what the keys act on
    [TestMethod]
    public async Task TestSelectedRowsOfferTheirDiffMenuAndCopy()
    {
        var view = await ViewOf(Fixture());

        var hints = KeyHints.For(view, new Hoover(), new Selection(0, 0, 0, 2, 0), false);

        Assert.AreEqual("d diff  m menu  Ctrl-C copy", Hints(hints));
    }

    // The line: hints from the left, help at the right, and what does not fit dropped from the end
    // rather than cut, so every hint shown is whole
    [TestMethod]
    public void TestTheLineDropsTheHintsThatDoNotFit()
    {
        List<KeyHint> hints = [new("d", "diff"), new("m", "menu"), new("f", "search")];

        Assert.AreEqual(" d diff  m menu  f search          ? help ", KeyHints.ToText(hints, 42).ToString());
        Assert.AreEqual(" d diff  m menu        ? help ", KeyHints.ToText(hints, 30).ToString());
        Assert.AreEqual(" d diff  ? help ", KeyHints.ToText(hints, 16).ToString());
        Assert.AreEqual(" ? help ", KeyHints.ToText(hints, 8).ToString(), "The help is kept to the last");
    }

    // The keys in cyan, what they do in dark gray, like the shortcuts in a menu
    [TestMethod]
    public void TestTheKeysAreDrawnInCyan()
    {
        var text = KeyHints.ToText([new("d", "diff")], 20);

        var colors = string.Concat(
            text.Fragments.Select(f => new string(f.Color == Color.Cyan ? 'C' : 'D', f.Text.Length))
        );
        Assert.AreEqual(" d diff      ? help ", text.ToString());
        Assert.AreEqual("DCDDDDDDDDDDDCDDDDDD", colors);
    }

    // A status message takes the line: green for what was done, yellow for why nothing was, red for
    // a failure, and cut with '┅' when it is longer than the line
    [TestMethod]
    public void TestAStatusMessageIsDrawnInTheColorOfItsKind()
    {
        var info = KeyHints.ToText(new StatusMessage("Pushed 'main'", StatusKind.Info, DateTime.UtcNow), 20);
        var notice = KeyHints.ToText(new StatusMessage("Nothing to commit", StatusKind.Notice, DateTime.UtcNow), 20);
        var failure = KeyHints.ToText(new StatusMessage("Fetch failed", StatusKind.Failure, DateTime.UtcNow), 20);

        Assert.AreEqual(" Pushed 'main'      ", info.ToString());
        Assert.AreEqual(Color.Green, info.Fragments[1].Color);
        Assert.AreEqual(Color.Yellow, notice.Fragments[1].Color);
        Assert.AreEqual(Color.BrightRed, failure.Fragments[1].Color);
    }

    [TestMethod]
    public void TestALongStatusMessageIsCut()
    {
        var text = KeyHints.ToText(
            new StatusMessage("Nothing to push on 'feature/login'", StatusKind.Notice, DateTime.UtcNow),
            20
        );

        Assert.AreEqual(" Nothing to push o┅ ", text.ToString());
    }

    static string Hints(FakeViewRepo view, Hoover? hoover = null, bool isDetailsShown = false) =>
        Hints(KeyHints.For(view, hoover ?? new Hoover(), new Selection(0, 0, 0, 0, 0), isDetailsShown));

    // A hint with no key is a label, e.g. the branch the keys act on
    static string Hints(IReadOnlyList<KeyHint> hints) =>
        string.Join("  ", hints.Select(h => h.Key == "" ? h.Text : $"{h.Key} {h.Text}"));

    // The cursor on the commit, and the branch hoovered there, as ← → leaves them
    static Hoover HooverOn(FakeViewRepo view, string branchName, string commit)
    {
        view.CurrentIndex = view.Repo.CommitById[RepoBuilder.Sha(commit)].ViewIndex;
        var hoover = new Hoover();
        hoover.SetBranch(view.Graph.BranchByName(branchName), view.CurrentIndex, view.CurrentIndex);
        return hoover;
    }

    static async Task<FakeViewRepo> ViewOf(RepoBuilder builder, params string[] showBranches) =>
        new FakeViewRepo(await builder.ViewRepoAsync(showBranches), builder.Config, builder.NewServer());

    // 'main' with 'dev' merged into it and hidden, synced with its remote
    static RepoBuilder Fixture() =>
        new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d1")
            .Commit("d1", "Work on dev", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .LocalBranch("dev", "d1");

    // 'main' with one commit not yet pushed, and 'd1' branched off below it for a branch to hoover
    static RepoBuilder Ahead() =>
        new RepoBuilder()
            .Commit("l1", "Local 1", "c1")
            .Commit("d1", "Work on dev", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "c1", ahead: 1);

    static RepoBuilder Behind() =>
        new RepoBuilder()
            .Commit("r1", "Remote 1", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true, remoteTipCommit: "r1", behind: 1);
}
