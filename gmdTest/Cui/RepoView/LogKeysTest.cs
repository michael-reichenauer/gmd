using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmdTest.Fixtures;
using gmdTest.Server.Private.Augmented.Private;
using GitOp = gmd.Git.GitOperation;

namespace gmdTest.Cui.RepoView;

// What the log view's keys do where the cursor is, or why they do nothing, which the keys say on the
// status line and the key-hint line leaves out. KeyHintsTest asserts the hints the same decisions
// make, so the two cannot disagree.
[TestClass]
public class LogKeysTest
{
    static readonly Selection NoSelection = new(0, 0, 0, 0, 0);

    // Ctrl-C, 'i' and '⇧i' copy a commit, which the uncommitted changes are not yet
    [TestMethod]
    public async Task TestCopyNeedsACommit()
    {
        var view = await ViewOf(Branches().WithStatus(modified: 1), "dev");
        Assert.IsTrue(view.RowCommit.IsUncommitted);

        Assert.AreEqual(
            "The uncommitted changes are no commit yet: move to one first",
            LogKeys.Copy(view, NoSelection).WhyNot
        );

        view.CurrentIndex = 1;
        Assert.AreEqual(new KeyAction("copy"), LogKeys.Copy(view, NoSelection));
    }

    [TestMethod]
    public async Task TestTheBranchKeysNeedABranchHighlighted()
    {
        var view = await ViewOf(Branches());
        var none = new Hoover();

        Assert.AreEqual(
            "Highlight a branch with ← → first, and 'e' merges it into the current branch",
            LogKeys.Merge(view, none).WhyNot
        );
        Assert.AreEqual(
            "Highlight a branch with ← → first, and 'E' merges the current branch into it",
            LogKeys.MergeInto(view, none).WhyNot
        );
        Assert.AreEqual("Highlight a branch with ← → first, and 's' switches to it", LogKeys.Switch(view, none).WhyNot);
    }

    // Another branch is switched to and merged into the current one, and the current one merged
    // into it, which is not hinted, being rarer
    [TestMethod]
    public async Task TestAnotherBranchIsSwitchedToAndMerged()
    {
        var view = await ViewOf(Branches(), "dev");
        var dev = HooverOn(view, "dev", "d1");

        Assert.AreEqual(new KeyAction("switch"), LogKeys.Switch(view, dev));
        Assert.AreEqual(new KeyAction("merge into main"), LogKeys.Merge(view, dev));
        Assert.AreEqual(new KeyAction(""), LogKeys.MergeInto(view, dev), "Acts, with no hint");
    }

    [TestMethod]
    public async Task TestTheCurrentBranchIsMergedFromAndIntoOthers()
    {
        var view = await ViewOf(Branches(), "dev");
        var main = HooverOn(view, "main", "c2");

        Assert.AreEqual("Already on 'main'", LogKeys.Switch(view, main).WhyNot);
        Assert.AreEqual(new KeyAction("merge into main"), LogKeys.Merge(view, main));
        Assert.AreEqual(new KeyAction("merge main into"), LogKeys.MergeInto(view, main));
    }

    [TestMethod]
    public async Task TestMergingWaitsForTheChanges()
    {
        var view = await ViewOf(Branches().WithStatus(modified: 1), "dev");
        var dev = HooverOn(view, "dev", "d1");

        Assert.AreEqual("Commit the changes first, then merge", LogKeys.Merge(view, dev).WhyNot);
        Assert.AreEqual("Commit the changes first, then merge", LogKeys.MergeInto(view, dev).WhyNot);
        Assert.AreEqual(Why.Changes, LogKeys.Diff(view, dev, NoSelection).WhyNot);
    }

    // Git checks a branch out to merge into it, which it refuses for one held by another worktree
    [TestMethod]
    public async Task TestNothingIsMergedIntoABranchOfAnotherWorktree()
    {
        var view = await ViewOf(Branches().Worktree("/home/me/repo-dev", "dev"), "dev");

        Assert.AreEqual(
            "'dev' is checked out in another worktree",
            LogKeys.MergeInto(view, HooverOn(view, "dev", "d1")).WhyNot
        );
    }

    // The current branch is pushed by the command that asks before a force push, and says itself
    // why there is nothing to push, so the key acts, hinted only with something to push
    [TestMethod]
    public async Task TestTheCurrentBranchIsPushedByItsCommand()
    {
        var synced = await ViewOf(Branches());
        Assert.AreEqual(new KeyAction(""), LogKeys.Push(synced, new Hoover()));

        var ahead = await ViewOf(
            new RepoBuilder()
                .Commit("l1", "Local", "c1")
                .Commit("c1", "Initial")
                .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
        );
        Assert.AreEqual(new KeyAction("push"), LogKeys.Push(ahead, new Hoover()));
    }

    // A highlighted branch not on origin yet is not pushed by 'p', which would make it on origin, for
    // everyone, but by Publish in its branch menu, which 'p' says. It pushed it, while it pointed at the
    // menu for the current one, whose menu then said 'Push  p'.
    [TestMethod]
    public async Task TestABranchNotOnOriginIsLeftToPublish()
    {
        var view = await ViewOf(Branches(), "dev");

        Assert.AreEqual(
            KeyAction.No("'dev' is not on origin yet: Publish in its branch menu publishes it"),
            LogKeys.Push(view, HooverOn(view, "dev", "d1"))
        );
    }

    // A branch rewritten here after it was pushed has nothing to pull, and its push is a force push,
    // which the hint says, since 'p' is what it needs next
    [TestMethod]
    public async Task TestABranchRewrittenHereIsHintedAForcePushAndNoPull()
    {
        var view = await ViewOf(LocalRewritesTest.Squashed(new RepoBuilder()));

        Assert.AreEqual(new KeyAction("force push"), LogKeys.Push(view, new Hoover()));
        Assert.AreEqual(new KeyAction(""), LogKeys.Pull(view, new Hoover()));
    }

    // A branch that is not current is pulled by a fetch, as the branch menu's Pull does, and says
    // why not, as that item does
    [TestMethod]
    public async Task TestABranchWithNothingToPullSaysSo()
    {
        var view = await ViewOf(Branches(), "dev");

        Assert.AreEqual("Nothing to pull on 'dev'", LogKeys.Pull(view, HooverOn(view, "dev", "d1")).WhyNot);
    }

    // The rows selected go first, as for the menu, so the diff of the rows with changes there too
    [TestMethod]
    public async Task TestTheRowsSelectedAreDiffedBeforeTheBranch()
    {
        var view = await ViewOf(Branches().WithStatus(modified: 1), "dev");

        Assert.AreEqual(
            new KeyAction("diff"),
            LogKeys.Diff(view, HooverOn(view, "dev", "d1"), new Selection(0, 1, 0, 2, 1))
        );
    }

    // 'd' on the uncommitted row with files in conflict asks whether to resolve them or to show the
    // diff, and is hinted as resolving; rows selected, or a branch highlighted, are diffed as ever
    [TestMethod]
    public async Task TestDiffOnTheConflictsAsksToResolveThem()
    {
        var view = await ViewOf(Branches().WithStatus(conflicted: 2, operation: GitOp.Merge, isFinishedByCommit: true));
        view.CurrentIndex = 0; // The uncommitted row

        Assert.IsTrue(LogKeys.IsResolve(view, new Hoover(), NoSelection));
        Assert.AreEqual(new KeyAction("resolve"), LogKeys.Diff(view, new Hoover(), NoSelection));
        Assert.IsFalse(LogKeys.IsResolve(view, new Hoover(), new Selection(0, 1, 0, 2, 1)), "Rows selected");
        view.CurrentIndex = view.Repo.CommitById[RepoBuilder.Sha("c2")].ViewIndex;
        Assert.IsFalse(LogKeys.IsResolve(view, new Hoover(), NoSelection), "Another row");
    }

    // 'h' hides the highlighted branch, or the row's, but never the main branch, which is always
    // shown: it did nothing there, and said nothing
    [TestMethod]
    public async Task TestTheMainBranchIsNotHidden()
    {
        var view = await ViewOf(Branches(), "dev");

        Assert.AreEqual(new KeyAction("hide"), LogKeys.Hide(view, HooverOn(view, "dev", "d1")));
        Assert.AreEqual(
            "'main' is the main branch, which is always shown",
            LogKeys.Hide(view, HooverOn(view, "main", "c2")).WhyNot
        );
        view.CurrentIndex = view.Repo.CommitById[RepoBuilder.Sha("c2")].ViewIndex;
        Assert.AreEqual(
            "'main' is the main branch, which is always shown",
            LogKeys.Hide(view, new Hoover()).WhyNot,
            "Nor main's row with no branch highlighted"
        );
    }

    // 'g' colors the highlighted branch, or the row's, but main is always magenta and a deleted
    // branch always gray: it did nothing on them, and said nothing
    [TestMethod]
    public async Task TestMainAndADeletedBranchKeepTheirColors()
    {
        var builder = new RepoBuilder()
            .Commit("c3", "Merge branch 'feature' into main", "c2", "f1")
            .Commit("f1", "Feature work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .LocalBranch("dev", "c2");
        var view = await ViewOf(builder, "dev", "feature:f1000000");

        Assert.AreEqual(new KeyAction(""), LogKeys.Color(view, HooverOn(view, "dev", "c2")), "Acts, with no hint");
        Assert.AreEqual("The main branch is always magenta", LogKeys.Color(view, HooverOn(view, "main", "c3")).WhyNot);
        Assert.AreEqual(
            "A deleted branch is always gray",
            LogKeys.Color(view, HooverOn(view, "feature:f1000000", "f1")).WhyNot
        );
    }

    // The uncommitted row is no commit to start a branch at, a branch highlighted there is
    [TestMethod]
    public async Task TestANewBranchStartsAtACommitOrABranch()
    {
        var view = await ViewOf(Branches().WithStatus(modified: 1), "dev");
        view.CurrentIndex = 0;

        Assert.AreEqual("A branch starts at a commit: move to one first", LogKeys.NewBranch(view, new Hoover()).WhyNot);
        Assert.AreEqual(new KeyAction("new branch"), LogKeys.NewBranch(view, HooverOn(view, "dev", "d1")));
    }

    static Hoover HooverOn(FakeViewRepo view, string branchName, string commit)
    {
        view.CurrentIndex = view.Repo.CommitById[RepoBuilder.Sha(commit)].ViewIndex;
        var hoover = new Hoover();
        hoover.SetBranch(view.Graph.BranchByName(branchName), view.CurrentIndex, view.CurrentIndex);
        return hoover;
    }

    static async Task<FakeViewRepo> ViewOf(RepoBuilder builder, params string[] showBranches) =>
        new FakeViewRepo(await builder.ViewRepoAsync(showBranches), builder.Config, builder.NewServer());

    // 'main', synced, and 'dev', a local branch off it
    static RepoBuilder Branches() =>
        new RepoBuilder()
            .Commit("d1", "Work on dev", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .LocalBranch("dev", "d1");
}
