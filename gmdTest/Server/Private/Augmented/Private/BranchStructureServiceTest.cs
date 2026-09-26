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

    // A GitHub pull request subject names the head branch with its owner, 'owner/dev', which is
    // matched to 'dev' by its ending. Both dev and origin/dev end that way, and dev is listed first:
    // taking it put the commit on the local branch, whose parent is its remote branch, whose parent
    // was then the local branch again, since it owned nothing. The ancestors of such a pair never
    // end, so reading the repo never did either (hence the wait: a hang fails rather than never
    // ending). The commit goes to the primary branch, the remote.
    //
    //   e1      main, merges dev by pull request
    //   |\
    //   | | f1  feature, started at dev's tip, which gives d1 a third candidate
    //   | |/
    //   | d1    dev and origin/dev
    //   |/
    //   c1
    [TestMethod]
    public async Task TestPullRequestNameGoesToThePrimaryBranchWithoutACycle()
    {
        var repo = await new RepoBuilder()
            .Commit("e1", "Merge pull request #1 from owner/dev", "c1", "d1")
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "e1", isCurrent: true)
            .BranchWithRemote("dev", "d1")
            .LocalBranch("feature", "f1")
            .AugmentAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("origin/dev", BranchOf(repo, "d1"));
        Assert.AreEqual("origin/dev", repo.Branches["dev"].ParentBranch?.Name);
        Assert.AreEqual("origin/main", repo.Branches["origin/dev"].ParentBranch?.Name);
        Assert.AreEqual("origin/dev", repo.Branches["feature"].ParentBranch?.Name);
        Assert.IsFalse(repo.Branches.Values.Any(b => b.IsCircularAncestors));
    }

    // 'git checkout -b feature' on dev, outside gmd, so nothing records where feature started. Dev's
    // tip then had three candidates, which no rule decided, and dev was ambiguous from its tip down
    // to where it started. The tip of a published branch is the last commit made on it, and a local
    // branch pointing there came later.
    [TestMethod]
    public async Task TestLocalBranchStartedAtAPublishedTipLeavesItToThatBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("d2", "Dev 2", "d1")
            .Commit("d1", "Dev 1", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("dev", "d2")
            .LocalBranch("feature", "d2")
            .AugmentAsync();

        Assert.AreEqual("origin/dev", BranchOf(repo, "d2"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d1"));
        Assert.IsFalse(repo.Commits.Any(c => c.IsAmbiguous));
        Assert.AreEqual("origin/dev", repo.Branches["feature"].ParentBranch?.Name, "feature owns nothing yet");
    }

    // The same once feature has a commit of its own: the commit it started at is still dev's
    [TestMethod]
    public async Task TestLocalBranchWithCommitsStartedAtAPublishedTipLeavesItToThatBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature work", "d2")
            .Commit("d2", "Dev 2", "d1")
            .Commit("d1", "Dev 1", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("dev", "d2")
            .LocalBranch("feature", "f1")
            .AugmentAsync();

        Assert.AreEqual("feature", BranchOf(repo, "f1"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d2"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d1"));
        Assert.IsFalse(repo.Commits.Any(c => c.IsAmbiguous));
        Assert.AreEqual("origin/dev", repo.Branches["feature"].ParentBranch?.Name);
    }

    // A branch with unpushed commits is published too: the local tip is where its next push goes
    [TestMethod]
    public async Task TestLocalBranchStartedAtAnUnpushedTipLeavesItToThatBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature work", "d2")
            .Commit("d2", "Dev 2, not pushed", "d1")
            .Commit("d1", "Dev 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1")
            .BranchWithRemote("dev", "d2", isCurrent: true, remoteTipCommit: "d1", ahead: 1)
            .LocalBranch("feature", "f1")
            .AugmentAsync();

        Assert.AreEqual("dev", BranchOf(repo, "d2"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d1"));
        Assert.IsFalse(repo.Commits.Any(c => c.IsAmbiguous));
        Assert.AreEqual("dev", repo.Branches["feature"].ParentBranch?.Name);
    }

    // Not the other way round: a local branch left pointing at an older commit of a published
    // branch did not make that commit, so it must not take the published branch's history from
    // there down. It stays as undecided as it was. (Not named dev here, since an integration branch
    // keeps such a commit by its name, see TestBranchPointOfALiveMergedBranchGoesToTheIntegrationBranch.)
    [TestMethod]
    public async Task TestLocalBranchLeftAtAnOlderCommitDoesNotTakeThePublishedBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("d2", "Work 2", "d1")
            .Commit("d1", "Work 1", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("work", "d2")
            .LocalBranch("old", "d1")
            .AugmentAsync();

        Assert.AreEqual("origin/work", BranchOf(repo, "d1"));
        Assert.IsTrue(CommitOf(repo, "d1").IsAmbiguous);
        Assert.AreEqual("Ambiguous", CommitOf(repo, "d1").DecidedBy);
    }

    // Should the hierarchy ever hold a cycle anyway, the ancestors stop where they come around again
    // and the branches in it are marked, which leaves them out of the view, rather than never ending
    [TestMethod]
    public async Task TestACycleInTheHierarchyEndsTheAncestors()
    {
        var repo = new WorkRepo(DateTime.UtcNow, "/test/repo", StatusConverter.ToStatus(RepoBuilder.NoChanges));
        var a = new WorkBranch("a", "a", "a", RepoBuilder.Sha("a1"));
        var b = new WorkBranch("b", "b", "b", RepoBuilder.Sha("b1"));
        a.ParentBranch = b;
        b.ParentBranch = a;
        repo.Branches["a"] = a;
        repo.Branches["b"] = b;

        await Task.Run(() => new BranchHierarchyService().DetermineAncestors(repo)).WaitAsync(TimeSpan.FromSeconds(5));

        CollectionAssert.AreEqual(new[] { "b" }, a.Ancestors.Select(x => x.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "a" }, b.Ancestors.Select(x => x.Name).ToArray());
        Assert.IsTrue(a.IsCircularAncestors);
        Assert.IsTrue(b.IsCircularAncestors);
    }

    // The commit a branch was started from is shared by both branches, and git records nothing
    // about which of them it belongs to. What gmd must not do is pick the new branch: picking that
    // way also drags the whole hierarchy with it, since dev would then look branched out of the
    // branch it merged in. It used to be marked ambiguous for the user to settle; dev is an
    // integration branch by its name, so it is decided now (see TestBranchPointOf... below for the
    // shapes where the merge subjects alone cannot tell).
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
    public async Task TestCommitBelowABranchPointIsTheMergedIntoBranch()
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

        // The shared commit goes to dev, the branch feature was started from and merged into
        var d1 = CommitOf(repo, "d1");
        Assert.AreEqual("origin/dev", BranchOf(repo, "d1"));
        Assert.IsFalse(d1.IsAmbiguous);
        Assert.AreEqual("DecideBranchPoint", d1.DecidedBy);

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
        Assert.IsFalse(CommitOf(repo, "d1").IsAmbiguous);
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

    // A branch point where the branch kept going after its merge into dev, i.e. was not deleted. The
    // merged branch's tip is named by the merge subject, which counted as likely, while dev's commit
    // above the branch point, a plain one, was not. So the branch point, and all of dev below it,
    // went to feature, and dev looked branched out of the branch it had merged in. Dev is an
    // integration branch by its name, and the branch point is where other branches start from it.
    //
    //   m       dev, merges feature
    //   |\
    //   d3 |    dev, a commit of its own
    //   | f1    feature, still there
    //   |/
    //   d2      dev or feature?
    //   | c2    main
    //   |/
    //   c1
    [TestMethod]
    public async Task TestBranchPointOfALiveMergedBranchGoesToTheIntegrationBranch()
    {
        var repo = await new RepoBuilder()
            .Commit("ee", "Merge branch 'feature' into dev", "d3", "f1")
            .Commit("d3", "Dev direct", "d2")
            .Commit("f1", "Feature work", "d2")
            .Commit("d2", "Dev 2", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("dev", "ee")
            .BranchWithRemote("feature", "f1")
            .AugmentAsync();

        Assert.AreEqual("origin/dev", BranchOf(repo, "d3"));
        Assert.AreEqual("origin/feature", BranchOf(repo, "f1"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d2"));
        Assert.IsFalse(repo.Commits.Any(c => c.IsAmbiguous));
        Assert.AreEqual("origin/main", repo.Branches["origin/dev"].ParentBranch?.Name);
        Assert.AreEqual("origin/dev", repo.Branches["origin/feature"].ParentBranch?.Name);
    }

    // The mirror image, a back-merge: dev merged into feature to bring it up to date. Merge subjects
    // and the graph cannot tell the two apart, since in both the branch point's two children are one
    // branch merged into the other. Here the branch named by the merge subject is dev, which is right,
    // and must stay right.
    //
    //   ff      feature, merges dev
    //   |\
    //   | d2    dev
    //   f1 |    feature
    //   |/
    //   d1      dev or feature?
    //   | c2    main
    //   |/
    //   c1
    [TestMethod]
    public async Task TestBranchPointOfABackMergeGoesToTheBranchMergedFrom()
    {
        var repo = await new RepoBuilder()
            .Commit("ff", "Merge branch 'dev' into feature", "f1", "d2")
            .Commit("d2", "Dev 2", "d1")
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev 1", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("dev", "d2")
            .BranchWithRemote("feature", "ff")
            .AugmentAsync();

        Assert.AreEqual("origin/feature", BranchOf(repo, "f1"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d2"));
        Assert.AreEqual("origin/dev", BranchOf(repo, "d1"));
        Assert.IsFalse(repo.Commits.Any(c => c.IsAmbiguous));
        Assert.AreEqual("origin/dev", repo.Branches["origin/feature"].ParentBranch?.Name);
    }

    // With names that say nothing, a branch that several other branches were merged into is the one
    // others start from, rather than a branch that nothing was merged into. One merged branch is not
    // enough: that is also what a branch brought up to date from the branch it started from has.
    //
    //   t4      team, merges b
    //   t3      team, merges a
    //   t2 |    team, a commit of its own
    //   | f1    feature
    //   |/
    //   t1      team or feature?
    [TestMethod]
    public async Task TestBranchPointGoesToTheBranchSeveralBranchesWereMergedInto()
    {
        var repo = await new RepoBuilder()
            .Commit("e4", "Merge branch 'b' into team", "e3", "b1")
            .Commit("e3", "Merge branch 'a' into team", "e2", "a1")
            .Commit("e2", "Team 2", "e1")
            .Commit("f1", "Feature work", "e1")
            .Commit("b1", "B work", "c1")
            .Commit("a1", "A work", "c1")
            .Commit("e1", "Team 1", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("team", "e4")
            .BranchWithRemote("feature", "f1")
            .AugmentAsync();

        Assert.AreEqual("origin/team", BranchOf(repo, "e1"));
        Assert.IsFalse(CommitOf(repo, "e1").IsAmbiguous);
        Assert.AreEqual("origin/team", repo.Branches["origin/feature"].ParentBranch?.Name);
    }

    // A branch brought up to date from the trunk, here from two remotes, has had branches merged
    // into it, but not ones that make it a branch others start from. So the branch point stays as
    // undecided as it was, rather than going to feature.
    [TestMethod]
    public async Task TestTrunkMergedIntoABranchDoesNotMakeItSenior()
    {
        var repo = await new RepoBuilder()
            .Commit("f3", "Merge remote-tracking branch 'upstream/main' into feature", "f2", "c3")
            .Commit("f2", "Merge branch 'main' into feature", "f1", "c2")
            .Commit("e2", "Team 2", "e1")
            .Commit("f1", "Feature work", "e1")
            .Commit("c3", "Main 3", "c2")
            .Commit("c2", "Main 2", "c1")
            .Commit("e1", "Team 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .BranchWithRemote("team", "e2")
            .BranchWithRemote("feature", "f3")
            .AugmentAsync();

        Assert.AreEqual(0, repo.Branches["origin/feature"].MergedFromNames.Count);
        Assert.IsTrue(CommitOf(repo, "e1").IsAmbiguous);
    }

    // A pull request is a contribution even when it comes from a fork's own trunk, as it does when
    // contributors work on their fork's main
    [TestMethod]
    public async Task TestPullRequestsFromForkTrunksMakeABranchSenior()
    {
        var repo = await new RepoBuilder()
            .Commit("e4", "Merge pull request #2 from bob/main", "e3", "b1")
            .Commit("e3", "Merge pull request #1 from alice/main", "e2", "a1")
            .Commit("e2", "Team 2", "e1")
            .Commit("f1", "Feature work", "e1")
            .Commit("b1", "Bob's work", "c1")
            .Commit("a1", "Alice's work", "c1")
            .Commit("e1", "Team 1", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("team", "e4")
            .BranchWithRemote("feature", "f1")
            .AugmentAsync();

        CollectionAssert.AreEquivalent(
            new[] { "alice/main", "bob/main" },
            repo.Branches["origin/team"].MergedFromNames.ToArray()
        );
        Assert.AreEqual("origin/team", BranchOf(repo, "e1"));
        Assert.IsFalse(CommitOf(repo, "e1").IsAmbiguous);
    }

    // A release branch is started from develop and features are started from it, e.g. a fix for the
    // release, so where a release branch and another branch meet, the release branch goes on
    [TestMethod]
    public async Task TestBranchPointOfAReleaseBranchGoesToIt()
    {
        var repo = await new RepoBuilder()
            .Commit("e2", "Release 2", "e1")
            .Commit("f1", "Fix for the release", "e1")
            .Commit("e1", "Release 1", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("release/1.0", "e2")
            .BranchWithRemote("fix-for-release", "f1")
            .AugmentAsync();

        Assert.AreEqual("origin/release/1.0", BranchOf(repo, "e1"));
        Assert.IsFalse(CommitOf(repo, "e1").IsAmbiguous);
        Assert.AreEqual("origin/release/1.0", repo.Branches["origin/fix-for-release"].ParentBranch?.Name);
    }

    // Two integration branches of one name, a deleted dev recovered once per merge of it, and a
    // feature: the feature was started from dev, and which of the two devs goes on does not matter
    [TestMethod]
    public async Task TestBranchPointOfIntegrationBranchesOfOneNameGoesToOneOfThem()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d2")
            .Commit("c2", "Merge branch 'dev' into main", "c1", "e1")
            .Commit("d2", "Dev 2", "d1")
            .Commit("e1", "Dev other", "d1")
            .Commit("f1", "Feature work", "d1")
            .Commit("d1", "Dev 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .BranchWithRemote("feature", "f1")
            .AugmentAsync();

        Assert.AreEqual("dev", repo.Branches[BranchOf(repo, "d1")].NiceName);
        Assert.IsFalse(CommitOf(repo, "d1").IsAmbiguous);
        Assert.AreEqual(BranchOf(repo, "d1"), repo.Branches["origin/feature"].ParentBranch?.Name);
    }

    // A repo can name its own integration branches, e.g. staging, which are then taken like develop
    [TestMethod]
    public async Task TestConfiguredIntegrationBranchIsTakenLikeDevelop()
    {
        RepoBuilder Repo() =>
            new RepoBuilder()
                .Commit("e2", "Staging 2", "e1")
                .Commit("f1", "Feature work", "e1")
                .Commit("e1", "Staging 1", "c1")
                .Commit("c2", "Main 2", "c1")
                .Commit("c1", "Initial")
                .BranchWithRemote("main", "c2", isCurrent: true)
                .BranchWithRemote("staging", "e2")
                .BranchWithRemote("feature", "f1");

        Assert.IsTrue(CommitOf(await Repo().AugmentAsync(), "e1").IsAmbiguous, "Nothing to go by");

        var repo = await Repo().IntegrationBranches("staging").AugmentAsync();
        Assert.AreEqual("origin/staging", BranchOf(repo, "e1"));
        Assert.IsFalse(CommitOf(repo, "e1").IsAmbiguous);
    }

    // Two integration branches of different names and a feature, and nothing to tell the two apart:
    // the commit stays ambiguous, but it is drawn on an integration branch, which the feature was
    // started from either way, rather than on the feature, although the feature's commit is the newest
    [TestMethod]
    public async Task TestUndecidedBranchPointIsDrawnOnTheMostSeniorName()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature work", "d1")
            .Commit("e1", "Develop work", "d1")
            .Commit("d2", "Dev work", "d1")
            .Commit("d1", "Shared", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("dev", "d2")
            .BranchWithRemote("develop", "e1")
            .BranchWithRemote("feature", "f1")
            .AugmentAsync();

        var d1 = CommitOf(repo, "d1");
        Assert.IsTrue(d1.IsAmbiguous);
        Assert.AreNotEqual("origin/feature", d1.Branch?.Name);
        Assert.IsTrue(WellKnownBranches.IsIntegrationName(d1.Branch!.NiceName), d1.Branch.Name);
    }

    // A deleted branch merged twice is recovered twice, once from each merge subject, both named dev.
    // Where the two meet there is nothing to choose between, so the commit is not left ambiguous,
    // which would ask the user whether it is on 'dev' or on 'dev'.
    //
    //   c3      main, merges dev
    //   c2 |    main, merges dev (the other)
    //   | d2    dev
    //   e1 |    dev
    //    \ |
    //     d1    dev or dev?
    [TestMethod]
    public async Task TestBranchPointOfBranchesOfOneNameIsNotAmbiguous()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d2")
            .Commit("c2", "Merge branch 'dev' into main", "c1", "e1")
            .Commit("d2", "Dev 2", "d1")
            .Commit("e1", "Dev other", "d1")
            .Commit("d1", "Dev 1", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .AugmentAsync();

        Assert.AreEqual($"dev:{RepoBuilder.Sid("d2")}", BranchOf(repo, "d2"));
        Assert.AreEqual($"dev:{RepoBuilder.Sid("e1")}", BranchOf(repo, "e1"));
        Assert.AreEqual($"dev:{RepoBuilder.Sid("d2")}", BranchOf(repo, "d1"), "The first of the two");
        Assert.IsFalse(repo.Commits.Any(c => c.IsAmbiguous));
    }

    // A foxtrot merge: main merged into feature to bring it up to date, and then main fast-forwarded
    // to that merge, e.g. by 'git merge feature' on main. Git's first parent of the merge is feature's
    // commit, so main's line ran through feature, and main's own commit was drawn as a side branch
    // named main, merged in. The subject says what happened, and on main's line it means main was
    // merged into, so the parents are swapped, as a pull merge's are: main keeps its own commits on its
    // line, and feature is drawn as merged into it.
    //
    //   ff      main, "Merge branch 'main' into feature"
    //   |\
    //   | c2    main
    //   f1 |    feature
    //   |/
    //   c1
    [TestMethod]
    public async Task TestFoxtrotMergeOnMainKeepsMainsCommitsOnItsLine()
    {
        var repo = await new RepoBuilder()
            .Commit("ff", "Merge branch 'main' into feature", "f1", "c2")
            .Commit("f1", "Feature work", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "ff", isCurrent: true)
            .AugmentAsync();

        var merge = CommitOf(repo, "ff");
        Assert.IsTrue(merge.IsParentsSwapped);
        Assert.AreEqual(RepoBuilder.Sha("c2"), merge.FirstParent?.Id);
        Assert.AreEqual("origin/main", BranchOf(repo, "ff"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c2"));
        Assert.AreEqual($"feature:{RepoBuilder.Sid("f1")}", BranchOf(repo, "f1"), "Named by the subject");
        Assert.AreEqual("origin/main", BranchOf(repo, "c1"));
        Assert.AreEqual("origin/main", repo.Branches[BranchOf(repo, "f1")].ParentBranch?.Name);
    }

    // gmd reads a repo again on every change, through the one augmenter and so the one
    // BranchNameService, whose parsed names the stages of a read share. Each read starts from git's
    // parent order, so the swap has to be made again: a swap kept in the names from the read before
    // made the subject no longer read as main merged into feature, nothing was swapped, and main's
    // own commit was drawn as a deleted branch again.
    [TestMethod]
    public async Task TestFoxtrotMergeIsSwappedOnEveryRead()
    {
        var builder = new RepoBuilder()
            .Commit("ff", "Merge branch 'main' into feature", "f1", "c2")
            .Commit("f1", "Feature work", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "ff", isCurrent: true);
        var augmenter = RepoBuilder.NewAugmenter();

        await augmenter.GetAugRepoAsync(builder.ToGitRepo());
        var repo = await augmenter.GetAugRepoAsync(builder.ToGitRepo());

        Assert.IsTrue(CommitOf(repo, "ff").IsParentsSwapped);
        Assert.AreEqual("origin/main", BranchOf(repo, "c2"));
        Assert.AreEqual($"feature:{RepoBuilder.Sid("f1")}", BranchOf(repo, "f1"));
    }

    // The same merge on feature's own line, where it belongs, is what it says it is and left alone
    [TestMethod]
    public async Task TestMainMergedIntoAFeatureIsNotAFoxtrot()
    {
        var repo = await new RepoBuilder()
            .Commit("ff", "Merge branch 'main' into feature", "f1", "c2")
            .Commit("f1", "Feature work", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("feature", "ff")
            .AugmentAsync();

        Assert.IsFalse(CommitOf(repo, "ff").IsParentsSwapped);
        Assert.AreEqual("origin/feature", BranchOf(repo, "ff"));
        Assert.AreEqual("origin/feature", BranchOf(repo, "f1"));
        Assert.AreEqual("origin/main", BranchOf(repo, "c2"));
    }

    // Stacked branches: feature2 started at feature1's tip with 'git checkout -b', and both pushed.
    // The graph cannot tell which of the two the shared commit is on, and names and merges say
    // nothing either. The reflog does: feature2 was created from HEAD, and HEAD was on feature1.
    //
    //   e2      feature2
    //   e1      feature1, and where feature2 started
    //   | c2    main
    //   |/
    //   c1
    [TestMethod]
    public async Task TestReflogSaysWhichBranchABranchWasStartedFrom()
    {
        var repo = await new RepoBuilder()
            .Commit("e2", "Feature 2 work", "e1")
            .Commit("e1", "Feature 1 work", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("feature1", "e1")
            .BranchWithRemote("feature2", "e2")
            .Reflog("refs/heads/feature2", "e2", "commit: Feature 2 work")
            .Reflog("refs/heads/feature2", "e1", "branch: Created from HEAD")
            .Reflog("HEAD", "e2", "commit: Feature 2 work")
            .Reflog("HEAD", "e1", "checkout: moving from feature1 to feature2")
            .AugmentAsync();

        var e1 = CommitOf(repo, "e1");
        Assert.AreEqual("origin/feature1", e1.Branch?.Name);
        Assert.AreEqual("IsWitnessed", e1.DecidedBy);
        Assert.IsFalse(e1.IsAmbiguous);
        Assert.IsFalse(e1.IsLikely, "Where a commit was made says nothing about its parent");
        Assert.AreEqual("origin/feature1", repo.Branches["origin/feature2"].ParentBranch?.Name);
    }

    // What the reflog decided between branches is to be kept in the metadata, since the reflog expires
    [TestMethod]
    public async Task TestReflogDecisionIsToBeKept()
    {
        var repo = await StackedBranches()
            .Reflog("refs/heads/feature2", "e2", "commit: Feature 2 work")
            .Reflog("refs/heads/feature2", "e1", "branch: Created from HEAD")
            .Reflog("HEAD", "e2", "commit: Feature 2 work")
            .Reflog("HEAD", "e1", "checkout: moving from feature1 to feature2")
            .AugmentAsync();

        CollectionAssert.AreEqual(
            new[] { new WitnessedBranch(RepoBuilder.Sha("e1"), "feature1") },
            repo.WitnessedToKeep.ToArray()
        );
    }

    // And once kept it decides the same way after the reflog is gone, e.g. in a fresh clone
    [TestMethod]
    public async Task TestKeptReflogFactDecidesOnceTheReflogIsGone()
    {
        var repo = await StackedBranches().Witnessed("e1", "feature1").AugmentAsync();

        Assert.AreEqual("origin/feature1", BranchOf(repo, "e1"));
        Assert.AreEqual("IsWitnessed", CommitOf(repo, "e1").DecidedBy);
        Assert.AreEqual(0, repo.WitnessedToKeep.Count, "Kept already");
    }

    // A kept fact is still only taken among the branches the commit can be on, and makes up none
    [TestMethod]
    public async Task TestKeptReflogFactOfABranchTheCommitIsNotOnDecidesNothing()
    {
        var repo = await StackedBranches().Witnessed("e1", "gone").AugmentAsync();

        Assert.IsTrue(CommitOf(repo, "e1").IsAmbiguous);
        Assert.IsFalse(repo.Branches.Keys.Any(n => n.StartsWith("gone")));
    }

    static RepoBuilder StackedBranches() =>
        new RepoBuilder()
            .Commit("e2", "Feature 2 work", "e1")
            .Commit("e1", "Feature 1 work", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("feature1", "e1")
            .BranchWithRemote("feature2", "e2");

    // A branch point decided by where it was made, where nothing else could: two ordinary branches,
    // one merged into the other and kept, which merge subjects alone read the wrong way round
    [TestMethod]
    public async Task TestReflogSaysWhichBranchABranchPointWasMadeOn()
    {
        var repo = await new RepoBuilder()
            .Commit("ee", "Merge branch 'topic' into base", "e3", "f1")
            .Commit("e3", "Base direct", "e2")
            .Commit("f1", "Topic work", "e2")
            .Commit("e2", "Base 2", "c1")
            .Commit("c2", "Main 2", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .BranchWithRemote("base", "ee")
            .BranchWithRemote("topic", "f1")
            .Reflog("refs/heads/base", "e2", "commit: Base 2")
            .AugmentAsync();

        Assert.AreEqual("origin/base", BranchOf(repo, "e2"));
        Assert.AreEqual("origin/topic", BranchOf(repo, "f1"));
        Assert.AreEqual("origin/base", repo.Branches["origin/topic"].ParentBranch?.Name);
    }

    // A commit made on dev and then reset away from it is not on dev any more, so the fact decides
    // nothing, and no branch is made up for it
    [TestMethod]
    public async Task TestReflogFactOfABranchTheCommitIsNotOnDecidesNothing()
    {
        var repo = await new RepoBuilder()
            .Commit("a1", "Work a", "d1")
            .Commit("b1", "Work b", "d1")
            .Commit("d1", "Shared work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c1", isCurrent: true)
            .LocalBranch("feat-a", "a1")
            .LocalBranch("feat-b", "b1")
            .Reflog("refs/heads/dev", "d1", "commit: Shared work")
            .AugmentAsync();

        Assert.IsTrue(CommitOf(repo, "d1").IsAmbiguous);
        Assert.IsFalse(repo.Branches.Keys.Any(n => n.StartsWith("dev")));
    }

    // Main's commits stay main's: a feature fast-forwarded into main would otherwise take main's tip
    [TestMethod]
    public async Task TestReflogFactDoesNotTakeACommitFromMain()
    {
        var repo = await new RepoBuilder()
            .Commit("f1", "Feature work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "f1", isCurrent: true)
            .LocalBranch("feature", "f1")
            .Reflog("refs/heads/feature", "f1", "commit: Feature work")
            .AugmentAsync();

        Assert.AreEqual("origin/main", BranchOf(repo, "f1"));
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
