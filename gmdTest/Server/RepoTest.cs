using gmdTest.Fixtures;

namespace gmdTest.Server;

// What a commit of the repo the UI renders tells about its parents: the order gmd draws them in,
// and the order git has them in, which differ for a merge whose parents gmd swapped
[TestClass]
public class RepoTest
{
    // gmd swaps a pull merge's parents, so the remote's commits are drawn on the branch's line and the
    // local ones as merged in. Git's order is still what the user is shown, and 'git revert -m'
    // counts in it, so reverting the merge undoes the side drawn as merged in: git's first parent.
    [TestMethod]
    public async Task TestAPullMergeKeepsGitsParentOrder()
    {
        var repo = await new RepoBuilder()
            .Commit("c4", "Merge branch 'main' of https://github.com/x/y into main", "c2", "c3")
            .Commit("c3", "Remote work", "c1")
            .Commit("c2", "Local work", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c4", isCurrent: true)
            .AugmentedRepoAsync();

        var merge = repo.CommitById[RepoBuilder.Sha("c4")];
        Assert.IsTrue(merge.IsParentsSwapped);
        CollectionAssert.AreEqual(new[] { RepoBuilder.Sha("c3"), RepoBuilder.Sha("c2") }, merge.ParentIds.ToArray());
        CollectionAssert.AreEqual(new[] { RepoBuilder.Sha("c2"), RepoBuilder.Sha("c3") }, merge.GitParentIds.ToArray());
        Assert.AreEqual(2, merge.MainlineParentNumber);
    }

    // Any other merge is drawn as git has it, and reverted against its first parent
    [TestMethod]
    public async Task TestAMergeIsDrawnInGitsParentOrder()
    {
        var repo = await new RepoBuilder()
            .Commit("c3", "Merge branch 'dev' into main", "c2", "d1")
            .Commit("d1", "Dev work", "c1")
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c3", isCurrent: true)
            .AugmentedRepoAsync();

        var merge = repo.CommitById[RepoBuilder.Sha("c3")];
        Assert.IsFalse(merge.IsParentsSwapped);
        CollectionAssert.AreEqual(merge.ParentIds.ToArray(), merge.GitParentIds.ToArray());
        Assert.AreEqual(1, merge.MainlineParentNumber);
        Assert.AreEqual(0, repo.CommitById[RepoBuilder.Sha("c2")].MainlineParentNumber, "Not a merge");
    }
}
