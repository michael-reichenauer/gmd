using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// The uncommitted commit is the virtual commit gmd shows for the changes in the working folder.
// Adding it is covered end to end elsewhere (it is what RepoBuilder.AugmentedRepoAsync goes
// through), so these are about the three things Uncommitted.Adjust decides on its own: when to add
// it, when to update it, and when to remove it again. Removing is the path UpdateRepoStatusAsync
// takes when the changes are committed while the log is open.
[TestClass]
public class UncommittedTest
{
    [TestMethod]
    public async Task TestNoChangesGivesNoUncommittedCommit()
    {
        var repo = await Clean();

        Assert.IsFalse(repo.CommitById.ContainsKey(Repo.UncommittedId));
        Assert.AreSame(repo, Uncommitted.Adjust(repo));
    }

    // The uncommitted commit becomes the tip of the current branch, with the previous tip as its
    // parent, so the working folder is drawn as a row above the last commit
    [TestMethod]
    public async Task TestChangesAddAnUncommittedCommitAsTheCurrentBranchTip()
    {
        var repo = Uncommitted.Adjust(await WithChanges(modified: 2, added: 1));

        var uncommitted = repo.CommitById[Repo.UncommittedId];
        Assert.AreEqual("3 uncommitted changes", uncommitted.Subject);
        Assert.AreEqual("main", uncommitted.BranchName);
        Assert.IsTrue(uncommitted.IsUncommitted);
        CollectionAssert.AreEqual(new[] { RepoBuilder.Sha("c2") }, uncommitted.ParentIds.ToArray());
        Assert.AreEqual(Repo.UncommittedId, repo.BranchByName["main"].TipId);
    }

    [TestMethod]
    public async Task TestMoreChangesUpdateTheExistingUncommittedCommit()
    {
        var repo = Uncommitted.Adjust(await WithChanges(modified: 2));
        Assert.AreEqual("2 uncommitted changes", repo.CommitById[Repo.UncommittedId].Subject);

        var updated = Uncommitted.Adjust(repo with { Status = repo.Status with { Modified = 5 } });

        Assert.AreEqual("5 uncommitted changes", updated.CommitById[Repo.UncommittedId].Subject);
        Assert.AreEqual(repo.AllCommits.Count, updated.AllCommits.Count);
    }

    // Committing while the log is open leaves a repo that still has the uncommitted commit but an
    // ok status, and the commit has to go again, taking the branch tip back to its parent
    [TestMethod]
    public async Task TestOkStatusRemovesTheUncommittedCommitAgain()
    {
        var withChanges = Uncommitted.Adjust(await WithChanges(modified: 2));

        var repo = Uncommitted.Adjust(withChanges with { Status = withChanges.Status with { Modified = 0 } });

        Assert.IsFalse(repo.CommitById.ContainsKey(Repo.UncommittedId));
        Assert.AreEqual(RepoBuilder.Sha("c2"), repo.BranchByName["main"].TipId);
        Assert.AreEqual(2, repo.AllCommits.Count);
    }

    // Pinned as current behavior: the two directions do not match. Adding the uncommitted commit
    // gives it its parent but does not add it to that parent's children, while removing it filters
    // the child lists of the parent all the same. Invisible today, since the graph draws the row
    // from the commit's own parent ids, but worth knowing before relying on a commit's children.
    [TestMethod]
    public async Task TestTheUncommittedCommitIsNeverAddedToItsParentsChildren()
    {
        var withChanges = Uncommitted.Adjust(await WithChanges(modified: 2));

        var parent = withChanges.CommitById[RepoBuilder.Sha("c2")];
        Assert.IsFalse(parent.AllChildIds.Contains(Repo.UncommittedId));
        Assert.IsFalse(parent.FirstChildIds.Contains(Repo.UncommittedId));
        Assert.IsFalse(parent.MergeChildIds.Contains(Repo.UncommittedId));
    }

    // A merge in progress and its conflicts are written into the subject, since the uncommitted
    // row is the only place the user sees them
    [TestMethod]
    public async Task TestMergeAndConflictsAreWrittenIntoTheSubject()
    {
        var clean = await Clean();

        var repo = Uncommitted.Adjust(
            clean with
            {
                Status = clean.Status with
                {
                    Modified = 2,
                    Conflicted = 1,
                    IsMerging = true,
                    MergeMessage = "Merge branch 'dev'",
                },
            }
        );

        var uncommitted = repo.CommitById[Repo.UncommittedId];
        Assert.AreEqual("CONFLICTS: 1, Merge branch 'dev', 3 uncommitted changes", uncommitted.Subject);
        Assert.IsTrue(uncommitted.IsConflicted);
    }

    static Task<Repo> Clean() => Builder().AugmentedRepoAsync();

    static Task<Repo> WithChanges(int modified = 0, int added = 0) =>
        Builder().WithStatus(modified: modified, added: added).AugmentedRepoAsync();

    static RepoBuilder Builder() =>
        new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true);
}
