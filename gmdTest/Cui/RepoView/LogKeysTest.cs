using gmd.Cui.Common;
using gmd.Cui.RepoView;
using gmdTest.Fixtures;

namespace gmdTest.Cui.RepoView;

// What the log view's keys do where the cursor is, or why they do nothing, which the keys say on the
// status line and the key-hint line leaves out. KeyHintsTest asserts the hints the same decisions
// make, so the two cannot disagree.
[TestClass]
public class LogKeysTest
{
    static readonly Selection NoSelection = new(0, 0, 0, 0, 0);

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
