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

    // Which is why renaming a branch has to rename the metadata as well: the stored name is the
    // strongest rule there is, so a stale one does not fall back to the branch git now has, it
    // recreates the old name as a branch of its own. This pins the failure the rename migration in
    // MetaData.RenameBranch exists to prevent.
    [TestMethod]
    public async Task TestABranchNameLeftBehindByARenameIsResurrected()
    {
        var repo = await NewRenamedRepoBuilder("dev").AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual($"dev:{RepoBuilder.Sid("d1")}", d1.Branch?.Name, "The old name is back as a branch");
        Assert.IsFalse(repo.Branches[d1.Branch!.Name].IsGitBranch);
        CollectionAssert.Contains(repo.Branches.Keys.ToList(), "dev2", "While the renamed branch is also there");
    }

    // With the metadata renamed along with the branch, the commit stays on the renamed branch and
    // no branch is left over
    [TestMethod]
    public async Task TestARenamedBranchNameKeepsTheCommitOnThatBranch()
    {
        var repo = await NewRenamedRepoBuilder("dev2").AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual("dev2", d1.Branch?.Name);
        Assert.IsTrue(d1.IsBranchSetByUser);
        CollectionAssert.AreEquivalent(
            new[] { "main", "origin/main", "dev2" },
            repo.Branches.Keys.ToList(),
            "No branch is left over from the old name"
        );
    }

    // A repo where the branch 'dev' was renamed to 'dev2', with the name the metadata still holds
    // for its tip commit left as the caller wants it, i.e. stale or renamed along with the branch
    static RepoBuilder NewRenamedRepoBuilder(string metaDataName) =>
        new RepoBuilder()
            .Commit("d1", "Dev work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .LocalBranch("dev2", "d1")
            .UserSetBranch("d1", metaDataName);

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

    // The commit a branch was started from is shared by both branches, and git records nothing
    // about which of them it belongs to. It is genuinely ambiguous, so gmd marks it and lets the
    // user settle it. What it must not do is silently pick the new branch: the branch that was
    // merged into is the more likely one, and picking the other way also drags the whole hierarchy
    // with it, since the branch would then look branched out of the branch it merged in.
    //
    //   c2      main, merges dev
    //   |\
    //   | d2    dev, merges feature
    //   | |\
    //   | | f1  feature
    //   | |/
    //   | d1    dev or feature? feature was started here
    //   |/
    //   c1      main
    [TestMethod]
    public async Task TestCommitBelowABranchPointIsTheMergedIntoBranchAndAmbiguous()
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

        Assert.AreEqual("origin/main", BranchOf(repo, "c2"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d2"));
        Assert.AreEqual("feature", BranchOf(repo, "f1"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c1"));

        // The shared commit goes to dev, the branch that was merged into, and is marked so the
        // user can move it to feature if that is where it belongs
        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual("origin/dev", BranchOf(repo, "d1"));
        Assert.IsTrue(d1.IsAmbiguous);
        Assert.IsTrue(d1.IsAmbiguousTip);
        CollectionAssert.AreEqual(new[] { "origin/dev", "feature" }, d1.Branches.Select(b => b.Name).ToArray());

        // Which gives the expected hierarchy, main <- dev <- feature
        Assert.AreEqual(RepoBuilder.Sha("d1"), repo.Branches["origin/dev"].BottomID);
        Assert.AreEqual("origin/main", repo.Branches["origin/dev"].ParentBranch?.Name);
        Assert.AreEqual("origin/dev", repo.Branches["feature"].ParentBranch?.Name);
    }

    // Same shape, but with no remote branches. The merged into branch is then the local branch.
    [TestMethod]
    public async Task TestCommitBelowABranchPointIsALocalOnlyMergedIntoBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("c2", "Merge branch 'dev' into main", "c1", "d2")
            .Commit("d2", "Merge branch 'feature' into dev", "d1", "f1")
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .LocalBranch("dev", "d2")
            .LocalBranch("feature", "f1")
            .AugmentAsync();

        Assert.AreEqual("dev", BranchOf(repo, "d1"));
        Assert.IsTrue(CommitOf(repo, "d1").IsAmbiguous);
        Assert.AreEqual("origin/main", repo.Branches["dev"].ParentBranch?.Name);
        Assert.AreEqual("dev", repo.Branches["feature"].ParentBranch?.Name);
    }

    // And the user can settle the branch point for good, which is then shared with other users
    [TestMethod]
    public async Task TestResolvingTheBranchPointClearsTheAmbiguity()
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
            .UserSetBranch("d1", "dev")
            .AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual("origin/dev", d1.Branch?.Name, "The remote branch is preferred over the local one");
        Assert.IsFalse(d1.IsAmbiguous);
        Assert.IsFalse(repo.Branches.Values.Any(b => b.IsAmbiguousBranch));
    }

    // The same rule keeps the commits below a series of merges on the branch they were merged into
    [TestMethod]
    public async Task TestCommitBelowSeveralMergesStaysOnTheMergedIntoBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("m2", "Merge branch 'b' into main", "m1", "b1")
            .Commit("m1", "Merge branch 'a' into main", "c1", "a1")
            .Commit("b1", "Work b", "c1")
            .Commit("a1", "Work a", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "m2", isCurrent: true)
            .AugmentAsync();

        Assert.AreEqual("origin/main", BranchOf(repo, "m1"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c1"), "c1 has a merge child and two branches out");

        // Both merged branches were deleted, so they are recovered from the merge subjects
        Assert.AreEqual($"a:{RepoBuilder.Sid("a1")}", BranchOf(repo, "a1"));
        Assert.AreEqual($"b:{RepoBuilder.Sid("b1")}", BranchOf(repo, "b1"));
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
