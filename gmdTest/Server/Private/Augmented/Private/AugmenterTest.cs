using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Augmented;

// Characterization tests for the augmentation pipeline, i.e. the inference of which branch
// each commit belongs to and how branches relate to each other. This is the core of what gmd
// adds on top of git, and it is the code most likely to regress when refactored, so these
// tests pin down the current behavior rather than assert a specification.
[TestClass]
public class AugmenterTest
{
    // The branch a commit was assigned to
    static string BranchOf(WorkRepo repo, string commitName) =>
        repo.CommitsById[RepoBuilder.Sha(commitName)].Branch?.Name ?? "<none>";

    static WorkCommit CommitOf(WorkRepo repo, string commitName) => repo.CommitsById[RepoBuilder.Sha(commitName)];

    // When a branch exists both locally and on the remote, the remote is the primary branch and
    // is what commits get assigned to.
    [TestMethod]
    public async Task TestCommitsAreAssignedToThePrimaryRemoteBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .AugmentAsync();

        Assert.AreEqual("origin/main", BranchOf(repo, "c2"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c1"));
        Assert.AreEqual("origin/main", repo.Branches["main"].PrimaryName);
        Assert.AreEqual("main", repo.Branches["origin/main"].LocalName);
    }

    // A branch with no corresponding remote branch is its own primary
    [TestMethod]
    public async Task TestLocalOnlyBranchIsItsOwnPrimary()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", "Initial")
            .LocalBranch("main", "c1", isCurrent: true)
            .AugmentAsync();

        Assert.AreEqual("main", BranchOf(repo, "c1"));
        Assert.AreEqual("main", repo.Branches["main"].PrimaryName);
        Assert.IsTrue(repo.Branches["main"].IsPrimary);
    }

    // Commits are returned newest first, the same order as the git log they came from
    [TestMethod]
    public async Task TestCommitsAreOrderedNewestFirst()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Third", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .AugmentAsync();

        CollectionAssert.AreEqual(
            new[] { "Third", "Second", "Initial" },
            repo.Commits.Select(c => c.Subject).ToArray()
        );
    }

    // Parents and children are linked both ways so the graph can be traversed in either direction
    [TestMethod]
    public async Task TestParentsAndChildrenAreLinked()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .AugmentAsync();

        var c1 = CommitOf(repo, "c1");
        var c2 = CommitOf(repo, "c2");

        Assert.AreEqual(c1.Id, c2.FirstParent?.Id);
        CollectionAssert.Contains(c1.FirstChildIds, c2.Id);
        CollectionAssert.Contains(c1.AllChildIds, c2.Id);
    }

    // A feature branch keeps its own commits, and is recorded as branched out of the branch it
    // was created from
    [TestMethod]
    public async Task TestFeatureBranchCommitsAndHierarchy()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d1")
            .Commit("d1", "Feature work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .LocalBranch("dev", "d1")
            .AugmentAsync();

        Assert.AreEqual("dev", BranchOf(repo, "d1"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c3"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c2"));
        Assert.AreEqual("origin/main", repo.Branches["dev"].ParentBranch?.Name);
    }

    // Git forgets which branch a merged commit was on once the branch is deleted. The branch is
    // recovered by parsing the merge commit subject, and becomes a non-git branch named
    // "<name>:<bottom commit sid>".
    [TestMethod]
    public async Task TestDeletedBranchIsRecoveredFromMergeSubject()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'gone' into main", "c2", "d1")
            .Commit("d1", "Work on gone", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .AugmentAsync();

        var recovered = $"gone:{RepoBuilder.Sid("d1")}";
        Assert.AreEqual(recovered, BranchOf(repo, "d1"));

        var branch = repo.Branches[recovered];
        Assert.IsFalse(branch.IsGitBranch, "A recovered branch has no git ref");
        Assert.AreEqual("gone", branch.NiceName);
        Assert.AreEqual("origin/main", branch.ParentBranch?.Name);
    }

    // A pull merge is remote commits merged into the local branch. The parents are swapped so
    // the local commits look merged into the remote branch, which keeps the remote branch's
    // commit order stable. The local commits end up on their own pull merge branch.
    [TestMethod]
    public async Task TestPullMergePutsLocalCommitsOnTheirOwnBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("c4", "Merge branch 'main' of https://github.com/x/y into main", "c2", "c3")
            .Commit("c3", "Remote work", "c1")
            .Commit("c2", "Local work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true)
            .AugmentAsync();

        var pullMergeBranch = $"main:{RepoBuilder.Sid("c2")}";
        Assert.AreEqual(pullMergeBranch, BranchOf(repo, "c2"), "Local commit should be on a pull merge branch");
        Assert.AreEqual("origin/main", BranchOf(repo, "c3"), "Remote commit stays on the remote branch");
        Assert.AreEqual("origin/main", BranchOf(repo, "c4"));

        // The pull merge branch is shown as the same branch as its primary
        Assert.AreEqual("origin/main", repo.Branches[pullMergeBranch].PrimaryName);
    }

    // The main branch is picked by name priority, not by which branch is checked out
    [TestMethod]
    public async Task TestMainBranchIsChosenByNamePriorityNotByCurrent()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "On master", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("master", "c2", isCurrent: true)
            .BranchWithRemote("main", "c1")
            .AugmentAsync();

        Assert.IsTrue(repo.Branches["origin/main"].IsMainBranch, "origin/main outranks origin/master");
        Assert.IsFalse(repo.Branches["origin/master"].IsMainBranch);
    }

    [TestMethod]
    public async Task TestTagsAreAttachedToTheirCommits()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .Tag("v1.0", "c1")
            .Tag("v2.0", "c2")
            .AugmentAsync();

        CollectionAssert.AreEqual(new[] { "v1.0" }, CommitOf(repo, "c1").Tags.Select(t => t.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "v2.0" }, CommitOf(repo, "c2").Tags.Select(t => t.Name).ToArray());
    }

    // With a detached HEAD the checked out commit is current and detached, but still keeps the
    // branch it belongs to
    [TestMethod]
    public async Task TestDetachedHeadMarksTheCheckedOutCommit()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .RemoteBranch("origin/main", "c2")
            .DetachedHead("c1")
            .AugmentAsync();

        var c1 = CommitOf(repo, "c1");
        Assert.IsTrue(c1.IsCurrent);
        Assert.IsTrue(c1.IsDetached);
        Assert.AreEqual("origin/main", c1.Branch?.Name, "A detached commit still belongs to its branch");
        Assert.IsFalse(CommitOf(repo, "c2").IsCurrent);
    }

    // A branch the user set manually overrides the name recovered from the merge subject, and is
    // flagged so the UI can show it (the 'Φ' symbol)
    [TestMethod]
    public async Task TestUserSetBranchOverridesTheRecoveredName()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'gone' into main", "c2", "d1")
            .Commit("d1", "Work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .UserSetBranch("d1", "chosen")
            .AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual($"chosen:{RepoBuilder.Sid("d1")}", d1.Branch?.Name, "Should use the user's name, not 'gone'");
        Assert.IsTrue(d1.IsBranchSetByUser);
        Assert.AreEqual("chosen", repo.Branches[d1.Branch!.Name].NiceName);
    }

    // For very large repos the log is truncated. Commits whose parents fall outside the log get
    // a virtual truncated commit as parent, so the graph stays connected.
    [TestMethod]
    public async Task TestTruncatedLogAddsAVirtualTruncatedCommit()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Oldest known", "c0") // c0 is not in the log
            .BranchWithRemote("main", "c2", isCurrent: true)
            .Truncated()
            .AugmentAsync();

        var truncated = repo.Commits.Last();
        Assert.IsTrue(truncated.IsTruncatedLogCommit, "A virtual truncated commit is added at the bottom");
        StringAssert.Contains(truncated.Subject, "truncated");

        // The oldest known commit now has the virtual commit as its parent instead of c0
        Assert.AreEqual(truncated.Id, CommitOf(repo, "c1").FirstParent?.Id);
    }

    // Stash commits are part of the git log but are not shown as commits, they only mark the
    // commit they were created from
    [TestMethod]
    public async Task TestStashIsNotShownAsACommitButMarksItsParent()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .Stash("stash@{0}", "c2")
            .AugmentAsync();

        Assert.AreEqual(2, repo.Commits.Count, "The stash commit should not be in the log");
        Assert.IsTrue(CommitOf(repo, "c2").HasStash);
        Assert.IsFalse(CommitOf(repo, "c1").HasStash);
    }

    // A repo with no commits at all. The AugmentedService substitutes a virtual commit and a main
    // branch for it, so the rest of the pipeline has something to work with.
    [TestMethod]
    public async Task TestEmptyRepoHasAVirtualCommitOnMain()
    {
        var repo = await new RepoBuilder().EmptyRepo().AugmentAsync();

        Assert.AreEqual(1, repo.Commits.Count);
        var commit = repo.CommitsById[gmd.Server.Repo.EmptyRepoCommitId];
        StringAssert.Contains(commit.Subject, "empty repo");
        Assert.AreEqual("main", commit.Branch?.Name);
        Assert.IsTrue(commit.IsCurrent);

        var main = repo.Branches["main"];
        Assert.IsTrue(main.IsMainBranch);
        Assert.IsNull(main.ParentBranch);
        Assert.AreEqual(main.TipID, main.BottomID);
    }

    // A brand new repo, one commit on a local branch with no remote
    [TestMethod]
    public async Task TestSingleCommitRepo()
    {
        var repo = await new RepoBuilder()
            .Commit("c1", "Initial")
            .LocalBranch("main", "c1", isCurrent: true)
            .AugmentAsync();

        var c1 = CommitOf(repo, "c1");
        Assert.AreEqual("main", c1.Branch?.Name);
        Assert.IsTrue(c1.IsCurrent);
        Assert.IsNull(c1.FirstParent);
        Assert.AreEqual(0, c1.FirstChildren.Count);

        var main = repo.Branches["main"];
        Assert.IsTrue(main.IsMainBranch);
        Assert.IsTrue(main.IsPrimary);
        Assert.IsNull(main.ParentBranch, "The bottom commit has no parent, so the branch is a root branch");
    }

    // Git status is carried into the repo as it is, including the merge in progress state that the
    // UI turns into the uncommitted commit's subject
    [TestMethod]
    public async Task TestStatusIsCarriedIntoTheRepo()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .WithStatus(
                modified: 2,
                added: 1,
                conflicted: 1,
                isMerging: true,
                mergeMessage: "Merge branch 'dev'",
                mergeHeadCommit: "c1"
            )
            .AugmentAsync();

        var status = repo.Status;
        Assert.AreEqual(4, status.ChangesCount, "Modified, added, deleted, conflicted and renamed are summed");
        Assert.IsFalse(status.IsOk);
        Assert.IsTrue(status.IsMerging);
        Assert.AreEqual("Merge branch 'dev'", status.MergeMessage);
        Assert.AreEqual(RepoBuilder.Sha("c1"), status.MergeHeadId);

        // The uncommitted commit itself is not added here, it is added by AugmentedService after
        // the repo has been converted, so the log holds only the real commits
        Assert.AreEqual(2, repo.Commits.Count);
        Assert.IsFalse(repo.CommitsById.ContainsKey(gmd.Server.Repo.UncommittedId));
    }

    // A local branch with unpushed commits. The local commits stay on the local branch while
    // everything the remote branch has is on the remote branch.
    [TestMethod]
    public async Task TestLocalBranchAheadOfItsRemote()
    {
        var repo = await new RepoBuilder()
            .Commit("l1", "Not yet pushed", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "c1", ahead: 1)
            .AugmentAsync();

        Assert.AreEqual("main", BranchOf(repo, "l1"), "The unpushed commit is on the local branch");
        Assert.AreEqual("origin/main", BranchOf(repo, "c1"));
        Assert.AreEqual("origin/main", repo.Branches["main"].ParentBranch?.Name);
    }

    // Local and remote branch have both moved on. Each keeps its own commits, and they meet at
    // the commit they share.
    [TestMethod]
    public async Task TestDivergedLocalAndRemoteBranchKeepTheirOwnCommits()
    {
        var repo = await new RepoBuilder()
            .Commit("l1", "Only local", "c2")
            .Commit("r1", "Only on remote", "c2")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "l1", isCurrent: true, remoteTipCommit: "r1", ahead: 1, behind: 1)
            .AugmentAsync();

        Assert.AreEqual("main", BranchOf(repo, "l1"));
        Assert.AreEqual("origin/main", BranchOf(repo, "r1"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c2"), "The shared commit belongs to the remote branch");

        var local = repo.Branches["main"];
        var remote = repo.Branches["origin/main"];
        Assert.AreEqual(RepoBuilder.Sha("l1"), local.TipID);
        Assert.AreEqual(RepoBuilder.Sha("l1"), local.BottomID, "The local branch is only the unpushed commit");
        Assert.AreEqual(RepoBuilder.Sha("r1"), remote.TipID);
        Assert.AreEqual(RepoBuilder.Sha("c1"), remote.BottomID);

        // Whether the branches are ahead or behind is decided later, when the view repo is built
        Assert.IsFalse(local.HasLocalOnly);
        Assert.IsFalse(remote.HasRemoteOnly);
    }

    // Two branches with the same nice name get a counter so the UI can tell them apart
    [TestMethod]
    public async Task TestDuplicateNiceNamesGetACounter()
    {
        var repo = await new RepoBuilder()
            .Commit("c4", "Merge branch 'dev' into main", "c3", "d1")
            .Commit("c3", "Merge branch 'dev' into main", "c2", "e1")
            .Commit("d1", "Second dev work", "c1")
            .Commit("e1", "First dev work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true)
            .AugmentAsync();

        var niceNames = repo
            .Branches.Values.Where(b => b.NiceName == "dev")
            .Select(b => b.NiceNameUnique)
            .OrderBy(n => n)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "dev", "dev(2)" }, niceNames);
    }
}
