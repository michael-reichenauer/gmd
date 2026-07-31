using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Server;

// The view repo is the subset of the augmented repo the user chose to see: the branches shown,
// their ancestors and related branches, and the commits on them. It is also where the properties
// that depend on what is shown are set, i.e. which commits are ahead or behind and the virtual
// uncommitted commit on top.
//
// These are characterization tests, pinning down what the code does today.
[TestClass]
public class ViewRepoCreaterTest
{
    static string[] BranchNames(Repo repo) => repo.ViewBranches.Select(b => b.Name).ToArray();

    static string[] Subjects(Repo repo) => repo.ViewCommits.Select(c => c.Subject).ToArray();

    // Showing a branch also shows what it hangs off, i.e. its ancestors, otherwise it would have
    // nothing to be drawn next to. Local and remote branches are related, so they come as a pair.
    [TestMethod]
    public async Task TestShownBranchBringsItsAncestorsAndRelatedBranches()
    {
        var repo = await ThreeBranches().ViewRepoAsync("dev");

        CollectionAssert.AreEqual(new[] { "origin/main", "main", "origin/dev", "dev" }, BranchNames(repo));
        CollectionAssert.AreEqual(
            new[]
            {
                "Merge branch 'feat' into main",
                "Merge branch 'dev' into main",
                "Dev work",
                "Third",
                "Second",
                "Initial",
            },
            Subjects(repo),
            "Only the commits of the shown branches, i.e. 'Feature work' is left out"
        );
    }

    // With no branches specified, the current branch is shown. The main branch is always included,
    // since it is what everything else is drawn relative to.
    [TestMethod]
    public async Task TestNoSpecifiedBranchesShowsTheCurrentAndMainBranch()
    {
        var repo = await ThreeBranches().ViewRepoAsync();

        CollectionAssert.AreEqual(new[] { "origin/main", "main" }, BranchNames(repo));
    }

    // 'Show all active branches' shows every branch that still exists in git
    [TestMethod]
    public async Task TestAllActiveShowsEveryGitBranch()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllActive);

        CollectionAssert.AreEqual(new[] { "origin/main", "main", "origin/dev", "dev", "feat" }, BranchNames(repo));
    }

    // 'Show all recent branches' shows the given number of branches with the newest tips. 'feat'
    // was committed after 'dev', so it is the one that comes along.
    [TestMethod]
    public async Task TestAllRecentShowsTheBranchesWithTheNewestTips()
    {
        var repo = await ThreeBranches().ViewRepoAsync(ShowBranches.AllRecent, 2);

        CollectionAssert.AreEqual(new[] { "origin/main", "main", "feat" }, BranchNames(repo));
    }

    // A branch deleted after being merged is not a git branch, so it only shows up in the mode
    // that asks for deleted branches too. It is named '<nice name>:<sid of its bottom commit>',
    // since several deleted branches can have had the same name.
    [TestMethod]
    public async Task TestDeletedBranchesAreOnlyShownWhenAskedFor()
    {
        var b = WithDeletedBranch();
        var deleted = $"gone:{RepoBuilder.Sid("d1")}";

        CollectionAssert.AreEqual(
            new[] { "origin/main", "main" },
            BranchNames(await b.ViewRepoAsync(ShowBranches.AllActive))
        );
        CollectionAssert.AreEqual(
            new[] { "origin/main", "main", deleted },
            BranchNames(await b.ViewRepoAsync(ShowBranches.AllActiveAndDeleted))
        );
    }

    // The commits a local branch has and its remote does not are 'ahead', and the other way around
    // 'behind'. Both are properties of the view, since they depend on which commits are shown.
    [TestMethod]
    public async Task TestAheadAndBehindCommitsAreMarked()
    {
        var repo = await Diverged().ViewRepoAsync();

        CollectionAssert.AreEqual(
            new[] { "Local 2", "Local 1" },
            repo.ViewCommits.Where(c => c.IsAhead).Select(c => c.Subject).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "Remote 1" },
            repo.ViewCommits.Where(c => c.IsBehind).Select(c => c.Subject).ToArray()
        );
    }

    // A branch pair with commits on both sides has local only and remote only commits, which is
    // what enables the push and pull menu items.
    //
    // Note the local branch is missing HasRemoteOnly, see the finding in MODERNIZATION.md: the
    // remote branch is handled first and writes the flag on both branches, then the local branch
    // is handled from a stale copy taken before that write, and overwrites it.
    [TestMethod]
    public async Task TestDivergedBranchesHaveLocalOnlyAndRemoteOnlyCommits()
    {
        var repo = await Diverged().ViewRepoAsync();

        var remote = repo.BranchByName["origin/main"];
        Assert.IsTrue(remote.HasLocalOnly);
        Assert.IsTrue(remote.HasRemoteOnly);

        var local = repo.BranchByName["main"];
        Assert.IsTrue(local.HasLocalOnly);
        Assert.IsFalse(local.HasRemoteOnly, "Lost, the local branch is updated from a stale copy");
    }

    // A branch in sync with its remote has neither
    [TestMethod]
    public async Task TestSyncedBranchHasNoLocalOrRemoteOnlyCommits()
    {
        var repo = await ThreeBranches().ViewRepoAsync();

        Assert.IsFalse(repo.BranchByName["origin/main"].HasLocalOnly);
        Assert.IsFalse(repo.BranchByName["origin/main"].HasRemoteOnly);
        Assert.IsFalse(repo.ViewCommits.Any(c => c.IsAhead || c.IsBehind));
    }

    // Uncommitted changes are shown as a virtual commit on top of the current branch, so they can
    // be selected and diffed like any other commit
    [TestMethod]
    public async Task TestUncommittedChangesBecomeAVirtualCommit()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .WithStatus(modified: 2, added: 1)
            .ViewRepoAsync();

        var uncommitted = repo.ViewCommits[0];
        Assert.AreEqual(Repo.UncommittedId, uncommitted.Id);
        Assert.AreEqual("3 uncommitted changes", uncommitted.Subject);
        Assert.IsTrue(uncommitted.IsUncommitted);
        Assert.AreEqual("main", uncommitted.BranchName, "On the current branch, i.e. the local one");
        CollectionAssert.AreEqual(new[] { RepoBuilder.Sha("c2") }, uncommitted.ParentIds.ToArray());
        Assert.AreEqual(Repo.UncommittedId, repo.BranchByName["main"].TipId, "It becomes the branch tip");
    }

    // With no changes there is no uncommitted commit
    [TestMethod]
    public async Task TestNoChangesGivesNoUncommittedCommit()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .ViewRepoAsync();

        Assert.IsFalse(repo.CommitById.ContainsKey(Repo.UncommittedId));
        CollectionAssert.AreEqual(new[] { "Initial" }, Subjects(repo));
    }

    // During a merge the uncommitted commit gets the merge source as a second parent, so the merge
    // is drawn while it is still in progress, and the subject says what is going on.
    [TestMethod]
    public async Task TestUncommittedCommitWhileMerging()
    {
        var repo = await new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .LocalBranch("dev", "d1")
            .WithStatus(conflicted: 2, isMerging: true, mergeMessage: "Merge branch 'dev'", mergeHeadCommit: "d1")
            .ViewRepoAsync("dev");

        var uncommitted = repo.ViewCommits[0];
        Assert.AreEqual("CONFLICTS: 2, Merge branch 'dev', 2 uncommitted changes", uncommitted.Subject);
        Assert.IsTrue(uncommitted.IsConflicted);
        CollectionAssert.AreEqual(
            new[] { RepoBuilder.Sha("c2"), RepoBuilder.Sha("d1") },
            uncommitted.ParentIds.ToArray(),
            "The merge source is the second parent, as for a real merge commit"
        );
    }

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

    static RepoBuilder WithDeletedBranch() =>
        new RepoBuilder()
            .Commit("c3", "Merge branch 'gone' into main", "c2", "d1")
            .Commit("d1", "Work on gone", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true);

    // main where the local branch has two commits the remote does not, and the remote one the
    // local does not
    static RepoBuilder Diverged() =>
        new RepoBuilder()
            .Commit("l2", "Local 2", "l1")
            .Commit("l1", "Local 1", "c2")
            .Commit("r1", "Remote 1", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l2", isCurrent: true, remoteTipCommit: "r1", ahead: 2, behind: 1);
}
