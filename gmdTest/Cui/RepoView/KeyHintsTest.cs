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
            "commit on main:  m menu  d diff  Enter details  ←→ branch  ⇧→ show branch  f search  b new branch",
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

    // With no branch highlighted, the line starts with the commit on the row and the branch it is on,
    // which the application bar used to show, unlabelled, in parentheses like a branch tip. The name
    // is in the color of the branch's line in the graph, as a highlighted branch's name is.
    [TestMethod]
    public async Task TestTheRowsCommitIsNamedWithItsBranchInItsColor()
    {
        var view = await ViewOf(Fixture(), "dev");
        view.CurrentIndex = view.Repo.CommitById[RepoBuilder.Sha("d1")].ViewIndex;

        StringAssert.StartsWith(Hints(view), "commit on dev:  m menu  ");
        var devColor = ColorOf(Line(view), "dev");
        Assert.AreEqual(view.Graph.BranchByName("dev").Color, devColor);

        var mainColor = ColorOf(Line(view, HooverOn(view, "main", "c3")), "main");
        Assert.AreEqual(view.Graph.BranchByName("main").Color, mainColor);
        Assert.AreNotEqual(devColor, mainColor, "The two branches in one color would prove nothing");
    }

    // Uncommitted changes put committing first, wherever the cursor is, since 'c' commits from any
    // row. On the uncommitted row itself there is no commit to make a branch from.
    [TestMethod]
    public async Task TestUncommittedChangesOfferCommittingFirst()
    {
        var view = await ViewOf(Fixture().WithStatus(modified: 1));

        Assert.AreEqual(
            "changes on main:  m menu  c commit  d diff  Enter details  ←→ branch  ⇧→ show branch  f search",
            Hints(view)
        );
    }

    // A merge stopped on conflicts: the diff of the uncommitted row is where they are resolved
    [TestMethod]
    public async Task TestConflictsAreResolvedFromTheDiff()
    {
        var view = await ViewOf(Fixture().WithStatus(conflicted: 1, operation: GitOp.Merge, isFinishedByCommit: true));

        StringAssert.StartsWith(Hints(view), "changes on main:  m menu  c commit  ⇧m abort…  d resolve  ");
    }

    // A rebase is finished by continuing it, not by a commit, and 'c' offers that instead
    [TestMethod]
    public async Task TestAStoppedRebaseIsContinued()
    {
        var view = await ViewOf(
            Fixture().WithStatus(conflicted: 1, operation: GitOp.Rebase, isFinishedByCommit: false)
        );

        StringAssert.StartsWith(Hints(view), "changes on main:  m menu  c continue  ⇧m abort…  d resolve  ");
    }

    // 'p' and 'u' only while the current branch has something to push or pull
    [TestMethod]
    public async Task TestPushIsOfferedOnlyWithSomethingToPush()
    {
        var view = await ViewOf(Ahead());

        StringAssert.Contains(Hints(view), "Enter details  p push  ←→ branch");
        Assert.IsFalse(Hints(await ViewOf(Fixture())).Contains("p push"), "Nothing to push when synced");
    }

    [TestMethod]
    public async Task TestPullIsOfferedOnlyWithSomethingToPull()
    {
        var view = await ViewOf(Behind());

        StringAssert.Contains(Hints(view), "Enter details  u pull  ←→ branch");
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
            "dev:  m menu  s switch  e merge into main  Enter show/hide  h hide  d diff  p push  b new branch",
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

        Assert.AreEqual(
            "main:  m menu  e merge into main  ⇧e merge main into  d diff  p push  b new branch",
            Hints(view, hoover)
        );
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
            "feature:  m menu  s switch  Enter show/hide  h hide  p push  c commit  b new branch",
            Hints(view, hoover)
        );
    }

    // Backspace, which undoes the last show or hide of branches, is left out of the hints even once
    // there is something to undo. The key is in the help, and in the Branches menu (Undo …).
    [TestMethod]
    public async Task TestBackspaceIsNotOfferedOnceABranchHasBeenShownOrHidden()
    {
        var view = await ViewOf(Fixture().LocalBranch("feature", "d1"), "feature");

        view.ShownHistory.Add(["main"], ["main", "feature"], "Show", "'feature'", "feature");
        StringAssert.Contains(Hints(view), "⇧→ show branch  f search");

        view.ShownHistory.Add(["main", "feature"], ["main"], "Hide", "'feature'", "feature");
        Assert.IsFalse(Hints(view, HooverOn(view, "feature", "d1")).Contains("Bksp"));
    }

    // After a search, n steps on to its next match in the log
    [TestMethod]
    public async Task TestNextMatchIsOfferedAfterASearch()
    {
        var view = await ViewOf(Fixture());
        Assert.IsFalse(Hints(view).Contains("next match"));

        view.SearchMatches.Set("work", [RepoBuilder.Sha("c3"), RepoBuilder.Sha("d1")], RepoBuilder.Sha("c3"));

        StringAssert.Contains(Hints(view), "⇧→ show branch  n next match  f search");
    }

    // Several rows selected with Shift-↑↓: the range is what the keys act on
    [TestMethod]
    public async Task TestSelectedRowsOfferTheirDiffMenuAndCopy()
    {
        var view = await ViewOf(Fixture());

        var hints = KeyHints.For(view, new Hoover(), new Selection(0, 0, 0, 2, 0), false);

        Assert.AreEqual("m menu  d diff  Ctrl-C copy", Hints(hints));
    }

    // The line: the border, the hints from the left, the border on to the help at the right, and what
    // does not fit dropped from the end rather than cut, so every hint shown is whole
    // The diff, blame and conflict views have lines of their own, each its view's keys, the way back
    // to the log early on. The diff offers commit and discard for the uncommitted changes only, and
    // Enter for a conflict; blame offers 'p' for a line with an older version and Backspace after
    // stepping back to one.
    [TestMethod]
    public void TestTheSideViewsHaveHintsOfTheirOwn()
    {
        static string Line(IReadOnlyList<KeyHint> hints) => string.Join("  ", hints.Select(h => $"{h.Key} {h.Text}"));

        Assert.AreEqual(
            "m menu  Esc close  + - context  s to file  ←→ columns",
            Line(KeyHints.ForDiff(isUncommitted: false, hasConflicts: false))
        );
        Assert.AreEqual(
            "m menu  Esc close  Enter resolve  c commit  + - context  s to file  u discard  ←→ columns",
            Line(KeyHints.ForDiff(isUncommitted: true, hasConflicts: true))
        );
        Assert.AreEqual(
            "m menu  Esc close  Enter details  d diff  l line history  i copy id  g gutter",
            Line(KeyHints.ForBlame(isDetailsShown: false, hasPrevious: false, canGoBack: false))
        );
        Assert.AreEqual(
            "m menu  Esc close  Enter hide details  d diff  l line history  p previous  Backspace back  i copy id  g gutter",
            Line(KeyHints.ForBlame(isDetailsShown: true, hasPrevious: true, canGoBack: true))
        );
        Assert.AreEqual(
            "m menu  Esc close  ] [ next, previous  s save  e edit  b ancestor  a whole file  u clear",
            Line(KeyHints.ForConflict())
        );
    }

    [TestMethod]
    public void TestTheLineDropsTheHintsThatDoNotFit()
    {
        List<KeyHint> hints = [new("m", "menu"), new("d", "diff"), new("f", "search")];

        Assert.AreEqual("────────── m menu  d diff  f search ──── ? help ──", KeyHints.ToText(hints, 50).ToString());
        Assert.AreEqual("────────── m menu  d diff ── ? help ──", KeyHints.ToText(hints, 38).ToString());
        Assert.AreEqual("────────────── ? help ──", KeyHints.ToText(hints, 24).ToString());
        Assert.AreEqual("────────── ? help ──", KeyHints.ToText(hints, 20).ToString(), "The help is kept to the last");
    }

    // Set into a border in the color of the line under the application bar, so that it reads as the
    // frame of the log rather than as one more row of it: the keys in cyan, what they do in dark gray,
    // like the shortcuts in a menu, and the border in bright magenta
    [TestMethod]
    public void TestTheLineIsABorderWithTheKeysInCyan()
    {
        var text = KeyHints.ToText([new("d", "diff")], 34);

        Assert.AreEqual("────────── d diff ────── ? help ──", text.ToString());
        Assert.AreEqual("MMMMMMMMMMDCDDDDDDMMMMMMDCDDDDDDMM", Colors(text));
    }

    // A status message takes the line, set into the border the same way: green for what was done,
    // yellow for why nothing was, red for a failure, cyan for what is being done, and cut with '┅'
    // when it is longer than the line
    [TestMethod]
    public void TestAStatusMessageIsDrawnInTheColorOfItsKind()
    {
        var info = KeyHints.ToText(new StatusMessage("Pushed 'main'", StatusKind.Info, DateTime.UtcNow), 28);
        var notice = KeyHints.ToText(new StatusMessage("Nothing to commit", StatusKind.Notice, DateTime.UtcNow), 32);
        var failure = KeyHints.ToText(new StatusMessage("Fetch failed", StatusKind.Failure, DateTime.UtcNow), 28);
        var progress = KeyHints.ToText(
            new StatusMessage("Pushing 'main'...", StatusKind.Progress, DateTime.UtcNow),
            32
        );

        Assert.AreEqual("────────── Pushed 'main' ───", info.ToString());
        Assert.AreEqual(Color.Green, ColorOf(info, "Pushed"));
        Assert.AreEqual(Color.Yellow, ColorOf(notice, "Nothing"));
        Assert.AreEqual(Color.BrightRed, ColorOf(failure, "Fetch"));
        Assert.AreEqual(Color.Cyan, ColorOf(progress, "Pushing"));
    }

    [TestMethod]
    public void TestALongStatusMessageIsCut()
    {
        var text = KeyHints.ToText(
            new StatusMessage("Nothing to push on 'feature/login'", StatusKind.Notice, DateTime.UtcNow),
            28
        );

        Assert.AreEqual("────────── Nothing to pu┅ ──", text.ToString());
    }

    // One letter per cell: 'M' the magenta border, 'C' a cyan key, 'D' the dark rest
    static string Colors(Text text) =>
        string.Concat(
            text.Fragments.Select(f => new string(
                f.Color == Color.BrightMagenta ? 'M'
                    : f.Color == Color.Cyan ? 'C'
                    : 'D',
                f.Text.Length
            ))
        );

    static Color ColorOf(Text text, string part) => text.Fragments.First(f => f.Text.Contains(part)).Color;

    static string Hints(FakeViewRepo view, Hoover? hoover = null, bool isDetailsShown = false) =>
        Hints(KeyHints.For(view, hoover ?? new Hoover(), new Selection(0, 0, 0, 0, 0), isDetailsShown));

    // The line as drawn, colors and all
    static Text Line(FakeViewRepo view, Hoover? hoover = null) =>
        KeyHints.ToText(KeyHints.For(view, hoover ?? new Hoover(), new Selection(0, 0, 0, 0, 0), false), 140);

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
