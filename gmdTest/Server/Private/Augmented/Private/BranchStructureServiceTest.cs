using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Augmented;

// Characterization tests for the branch inference in BranchStructureService: ambiguous commits,
// the user's manual branch choices, the branch hierarchy and the root branch. Driven through the
// whole augmentation pipeline via RepoBuilder, since that is the only way in.
//
// These pin down what the code does today, not what it ought to do. Where the current result is
// surprising it is called out in the comment rather than corrected.
[TestClass]
public class BranchStructureServiceTest
{
    static string BranchOf(WorkRepo repo, string commitName) =>
        repo.CommitsById[RepoBuilder.Sha(commitName)].Branch?.Name ?? "<none>";

    static WorkCommit CommitOf(WorkRepo repo, string commitName) => repo.CommitsById[RepoBuilder.Sha(commitName)];

    // Two branches sharing a commit, with nothing to tell which of them the shared commit belongs
    // to. Git does not record it and the subjects say nothing, so the commit is marked ambiguous
    // and the user gets to resolve it (the 'Φ' symbol in the log).
    //
    //   a1   b1     feat-a and feat-b tips
    //     \ /
    //      d1       shared, could belong to either branch
    //      |
    //      c1       main
    [TestMethod]
    public async Task TestCommitSharedByTwoBranchesIsAmbiguous()
    {
        var repo = await new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1")
            .AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.IsTrue(d1.IsAmbiguous);
        Assert.IsTrue(d1.IsAmbiguousTip, "The topmost ambiguous commit of the branch is the ambiguous tip");
        CollectionAssert.AreEqual(
            new[] { "feat-a", "feat-b" },
            d1.Branches.Select(b => b.Name).ToArray(),
            "Both candidate branches are kept, so the user can choose"
        );

        // The branch of the newest child is picked as the most likely one
        Assert.AreEqual("feat-a", d1.Branch?.Name);

        var branch = repo.Branches["feat-a"];
        Assert.IsTrue(branch.IsAmbiguousBranch);
        Assert.AreEqual(d1.Id, branch.AmbiguousTip?.Id);
        CollectionAssert.AreEqual(new[] { "feat-a", "feat-b" }, branch.AmbiguousBranches.Select(b => b.Name).ToArray());

        // The commits that do have a known branch are unaffected
        Assert.IsFalse(CommitOf(repo, "a1").IsAmbiguous);
        Assert.IsFalse(CommitOf(repo, "b1").IsAmbiguous);
        Assert.IsFalse(CommitOf(repo, "c1").IsAmbiguous);
        Assert.IsFalse(repo.Branches["feat-b"].IsAmbiguousBranch, "Only the picked branch is marked ambiguous");
    }

    // Ambiguity spreads down the branch: every commit below the ambiguous tip is ambiguous too,
    // but only the topmost one is the tip, since that is where the user resolves it.
    [TestMethod]
    public async Task TestAmbiguitySpreadsDownBelowTheAmbiguousTip()
    {
        var repo = await new RepoBuilder()
            .Commit("a1", "Work a", "d2")
            .Commit("b1", "Work b", "d2")
            .Commit("d2", "Shared 2", "d1")
            .Commit("d1", "Shared 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1")
            .AugmentAsync();

        var d2 = CommitOf(repo, "d2");
        var d1 = CommitOf(repo, "d1");

        Assert.IsTrue(d2.IsAmbiguous);
        Assert.IsTrue(d2.IsAmbiguousTip);
        Assert.IsTrue(d1.IsAmbiguous);
        Assert.IsFalse(d1.IsAmbiguousTip, "Only the topmost ambiguous commit is the tip");
        Assert.AreEqual("feat-a", BranchOf(repo, "d1"));

        // The ambiguity stops where a commit has a known branch again
        Assert.IsFalse(CommitOf(repo, "c1").IsAmbiguous);
        Assert.AreEqual(d2.Id, repo.Branches["feat-a"].AmbiguousTip?.Id);
    }

    // Resolving an ambiguity (the resolve menu item) stores the chosen branch name for the
    // ambiguous tip commit in the repo metadata. That is what ResolveAmbiguityAsync and
    // SetBranchManuallyAsync both write, so this covers the read side of both.
    [TestMethod]
    public async Task TestResolveAmbiguitySetsTheBranchAndClearsTheAmbiguity()
    {
        var repo = await new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1")
            .UserSetBranch("d1", "feat-b")
            .AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual("feat-b", d1.Branch?.Name, "The commit is moved to the branch the user chose");
        Assert.IsTrue(d1.IsBranchSetByUser);
        Assert.IsTrue(d1.IsLikely);
        Assert.IsFalse(d1.IsAmbiguous);
        Assert.IsFalse(d1.IsAmbiguousTip);

        // No branch is left ambiguous
        Assert.IsFalse(repo.Branches.Values.Any(b => b.IsAmbiguousBranch));
        Assert.IsNull(repo.Branches["feat-a"].AmbiguousTip);

        // The resolved branch now owns the shared commit, so it reaches further down
        Assert.AreEqual(RepoBuilder.Sha("d1"), repo.Branches["feat-b"].BottomID);
        Assert.AreEqual(RepoBuilder.Sha("a1"), repo.Branches["feat-a"].BottomID);
    }

    // Unresolving (the unresolve menu item) does not delete the metadata entry, it empties it, so
    // that the removal can be synced to other users. The repo must then look exactly as it did
    // before the ambiguity was resolved.
    [TestMethod]
    public async Task TestUnresolveAmbiguityRestoresTheAmbiguity()
    {
        var repo = await new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1")
            .UnsetBranch("d1")
            .AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.IsTrue(d1.IsAmbiguous);
        Assert.IsTrue(d1.IsAmbiguousTip);
        Assert.IsFalse(d1.IsBranchSetByUser);
        Assert.AreEqual("feat-a", d1.Branch?.Name);
        Assert.AreEqual(d1.Id, repo.Branches["feat-a"].AmbiguousTip?.Id);
    }

    // The user may also name a branch that no longer exists in git. A new non-git branch is then
    // created for it, named "<name>:<bottom commit sid>" like other recovered branches.
    [TestMethod]
    public async Task TestSettingABranchNameThatIsNotAGitBranchCreatesOne()
    {
        var repo = await new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1")
            .UserSetBranch("d1", "invented")
            .AugmentAsync();

        var created = $"invented:{RepoBuilder.Sid("d1")}";
        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual(created, d1.Branch?.Name);
        Assert.IsTrue(d1.IsBranchSetByUser);
        Assert.IsFalse(d1.IsAmbiguous);

        var branch = repo.Branches[created];
        Assert.IsFalse(branch.IsGitBranch);
        Assert.AreEqual("invented", branch.NiceName);
        Assert.AreEqual("origin/main", branch.ParentBranch?.Name);

        // Both feature branches now branch out of the created branch
        Assert.AreEqual(created, repo.Branches["feat-a"].ParentBranch?.Name);
        Assert.AreEqual(created, repo.Branches["feat-b"].ParentBranch?.Name);
    }

    // gmd also records where a branch was created from (metadata written by CreateBranchAsync).
    // That resolves the same ambiguity, but is not flagged as a user choice, so the UI does not
    // offer to unresolve it.
    [TestMethod]
    public async Task TestBranchedMetadataResolvesAmbiguityWithoutBeingAUserChoice()
    {
        var repo = await new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1")
            .Branched("d1", "feat-b")
            .AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual("feat-b", d1.Branch?.Name);
        Assert.IsFalse(d1.IsAmbiguous);
        Assert.IsFalse(d1.IsBranchSetByUser, "Branched-out metadata is not a user choice");
    }

    // Ancestors are the parent chain of a branch, nearest parent first. A local branch has its
    // remote branch as parent, so the local branch has one more ancestor than the remote one.
    [TestMethod]
    public async Task TestAncestorsAreTheParentChainNearestFirst()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature work", "d2")
            .Commit("d2", "More dev work", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .BranchWithRemote("dev", "d2")
            .LocalBranch("feature", "f1", isCurrent: true)
            .Branched("d2", "dev") // Records that feature was created from dev
            .AugmentAsync();

        CollectionAssert.AreEqual(
            new[] { "origin/dev", "origin/main" },
            repo.Branches["feature"].Ancestors.Select(b => b.Name).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "origin/main" },
            repo.Branches["origin/dev"].Ancestors.Select(b => b.Name).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "origin/dev", "origin/main" },
            repo.Branches["dev"].Ancestors.Select(b => b.Name).ToArray(),
            "A local branch has its own remote branch as first ancestor"
        );
        Assert.AreEqual(0, repo.Branches["origin/main"].Ancestors.Count, "The root branch has no ancestors");
        Assert.IsFalse(repo.Branches.Values.Any(b => b.IsCircularAncestors));
    }

    // A commit below a branch point is claimed by whichever child branch has a name parsed from a
    // merge subject. Here d1 is a dev commit, but the feature branch is the one with a known name,
    // so d1 ends up on feature and dev ends up branched out of feature rather than the other way
    // around. Pinned as current behavior, see MODERNIZATION.md step 2.
    [TestMethod]
    public async Task TestCommitBelowABranchPointFollowsTheChildWithAKnownName()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Merge branch 'dev' into main", "c1", "d2")
            .Commit("d2", "Merge branch 'feature' into dev", "d1", "f1")
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("dev", "d2")
            .LocalBranch("feature", "f1")
            .AugmentAsync();

        Assert.AreEqual("origin/dev", BranchOf(repo, "d2"));
        Assert.AreEqual("feature", BranchOf(repo, "f1"));
        Assert.AreEqual("feature", BranchOf(repo, "d1"), "Surprising: d1 is a dev commit");
        Assert.AreEqual("feature", repo.Branches["origin/dev"].ParentBranch?.Name);
    }

    // An orphan branch, e.g. a docs or gh-pages branch, has its own first commit and so is a root
    // branch too. When no branch is named main, master or trunk, the one whose history reaches
    // furthest back is picked as the repo's main branch, not the first one git happened to list.
    // It matters: the main branch is always shown in the log, is always magenta and cannot be
    // deleted or recolored.
    [TestMethod]
    public async Task TestRootBranchWithoutAMainNameIsTheOldestBranch()
    {
        var devFirst = await new RepoBuilder()
            .Commit("d1", "Docs") // An unrelated history, i.e. a second root commit
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("dev", "c2", isCurrent: true)
            .LocalBranch("docs", "d1")
            .AugmentAsync();

        Assert.IsTrue(devFirst.Branches["dev"].IsMainBranch);
        Assert.IsFalse(devFirst.Branches["docs"].IsMainBranch);

        // Same repo, only the order the branches are listed in differs
        var docsFirst = await new RepoBuilder()
            .Commit("d1", "Docs")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .LocalBranch("docs", "d1")
            .LocalBranch("dev", "c2", isCurrent: true)
            .AugmentAsync();

        Assert.IsTrue(docsFirst.Branches["dev"].IsMainBranch, "The branch order must not matter");
        Assert.IsFalse(docsFirst.Branches["docs"].IsMainBranch);
    }

    // An orphan branch can easily hold more commits than the trunk (a gh-pages branch tends to),
    // so the age of the history decides, not the number of commits
    [TestMethod]
    public async Task TestRootBranchIsTheOldestEvenWhenAnotherHasMoreCommits()
    {
        var repo = await new RepoBuilder()
            .Commit("p3", "Pages 3", "p2")
            .Commit("p2", "Pages 2", "p1")
            .Commit("p1", "Pages 1") // Unrelated history, started after dev
            .Commit("c1", "Initial")
            .LocalBranch("gh-pages", "p3")
            .LocalBranch("dev", "c1", isCurrent: true)
            .AugmentAsync();

        Assert.IsTrue(repo.Branches["dev"].IsMainBranch, "dev has the oldest commit, gh-pages has more");
        Assert.IsFalse(repo.Branches["gh-pages"].IsMainBranch);
    }

    // A branch left pointing at an older commit, with no commits of its own, is not a second root
    // branch: it owns the commit it points at, so the branch above it becomes its child. There is
    // then only one root branch to choose, whatever order the branches are listed in.
    [TestMethod]
    public async Task TestBranchPointingAtAnOlderCommitOwnsIt()
    {
        var repo = await new RepoBuilder()
            .Commit("b1", "Second work", "a1")
            .Commit("a1", "Initial")
            .LocalBranch("zeta", "a1")
            .LocalBranch("alpha", "b1", isCurrent: true)
            .AugmentAsync();

        Assert.AreEqual("zeta", BranchOf(repo, "a1"));
        Assert.AreEqual("zeta", repo.Branches["alpha"].ParentBranch?.Name);
        Assert.IsNull(repo.Branches["zeta"].ParentBranch);
        Assert.IsTrue(repo.Branches["zeta"].IsMainBranch, "The only root branch is the main branch");
        Assert.IsFalse(repo.Branches["alpha"].IsMainBranch);
    }

    // In a truncated log the virtual truncated branch is only a scaffold. It is removed once the
    // root branch is known, and everything that hung off it is redirected to the root branch.
    [TestMethod]
    public async Task TestTruncatedBranchIsReplacedByTheRootBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Oldest known", "c0") // c0 is not in the log
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("dev", "d1")
            .Truncated()
            .AugmentAsync();

        Assert.IsFalse(repo.Branches.ContainsKey("<truncated-branch>"), "The virtual branch is removed again");

        var truncated = repo.CommitsById[gmd.Server.Repo.TruncatedLogCommitId];
        Assert.AreEqual("origin/main", truncated.Branch?.Name, "The truncated commit joins the root branch");

        var root = repo.Branches["origin/main"];
        Assert.IsTrue(root.IsMainBranch);
        Assert.IsNull(root.ParentBranch);
        Assert.AreEqual(truncated.Id, root.BottomID, "The root branch now reaches down to the truncated commit");
        Assert.AreEqual("origin/main", repo.Branches["dev"].ParentBranch?.Name);
    }
}
