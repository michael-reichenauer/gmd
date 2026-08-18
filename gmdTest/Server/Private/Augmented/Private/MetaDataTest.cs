using gmd.Server.Private.Augmented.Private;

namespace gmdTest.Augmented;

// The user's branch choices are stored as git key/value data so they can be pushed and pulled
// between users. That sharing is why a removed choice is stored as an empty value instead of
// being deleted: a deleted key would just come back on the next merge with another user's data.
[TestClass]
public class MetaDataTest
{
    [TestMethod]
    public void TestSetCommitBranchIsAUserChoice()
    {
        var metaData = new MetaData();
        metaData.SetCommitBranch("abc123", "dev");

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out var isSetByUser));
        Assert.AreEqual("dev", name);
        Assert.IsTrue(isSetByUser);
    }

    // Where a branch was created from is recorded the same way, but is not a user choice, so the
    // UI does not offer to undo it
    [TestMethod]
    public void TestSetBranchedIsNotAUserChoice()
    {
        var metaData = new MetaData();
        metaData.SetBranched("abc123", "dev");

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out var isSetByUser));
        Assert.AreEqual("dev", name);
        Assert.IsFalse(isSetByUser);
    }

    [TestMethod]
    public void TestRemoveKeepsTheKeyButEmptiesIt()
    {
        var metaData = new MetaData();
        metaData.SetCommitBranch("abc123", "dev");
        metaData.RemoveCommitBranch("abc123");

        Assert.IsFalse(metaData.TryGetCommitBranch("abc123", out var name, out _));
        Assert.AreEqual("", name);
        Assert.IsTrue(metaData.CommitBranchBySid.ContainsKey("abc123"), "The removal itself has to be shareable");
    }

    [TestMethod]
    public void TestUnknownCommitHasNoBranch()
    {
        Assert.IsFalse(new MetaData().TryGetCommitBranch("abc123", out var name, out var isSetByUser));
        Assert.AreEqual("", name);
        Assert.IsFalse(isSetByUser);
    }

    // A renamed branch has to be renamed in the choices too, otherwise the old name would no longer
    // match any branch and would be resurrected as a deleted branch of its own
    [TestMethod]
    public void TestRenameBranchRenamesTheChoicesOfThatBranch()
    {
        var metaData = new MetaData();
        metaData.SetBranched("branched", "dev");
        metaData.SetCommitBranch("byUser", "dev");
        metaData.SetBranched("other", "main");

        metaData.RenameBranch("dev", "dev2");

        Assert.IsTrue(metaData.TryGetCommitBranch("branched", out var name, out var isSetByUser));
        Assert.AreEqual("dev2", name);
        Assert.IsFalse(isSetByUser, "A branched choice stays a branched choice");

        Assert.IsTrue(metaData.TryGetCommitBranch("byUser", out name, out isSetByUser));
        Assert.AreEqual("dev2", name);
        Assert.IsTrue(isSetByUser, "A choice set by the user stays set by the user");

        Assert.IsTrue(metaData.TryGetCommitBranch("other", out name, out _));
        Assert.AreEqual("main", name, "Other branches are untouched");
    }

    // Renaming to a name that is a prefix or suffix of another must not touch the other
    [TestMethod]
    public void TestRenameBranchOnlyRenamesExactNames()
    {
        var metaData = new MetaData();
        metaData.SetBranched("similar", "dev-2");
        metaData.SetBranched("prefixed", "my/dev");

        metaData.RenameBranch("dev", "renamed");

        Assert.IsTrue(metaData.TryGetCommitBranch("similar", out var name, out _));
        Assert.AreEqual("dev-2", name);
        Assert.IsTrue(metaData.TryGetCommitBranch("prefixed", out name, out _));
        Assert.AreEqual("my/dev", name);
    }

    // A later choice replaces an earlier one rather than adding to it
    [TestMethod]
    public void TestSettingTheBranchAgainReplacesTheChoice()
    {
        var metaData = new MetaData();
        metaData.SetBranched("abc123", "dev");
        metaData.SetCommitBranch("abc123", "main");

        Assert.IsTrue(metaData.TryGetCommitBranch("abc123", out var name, out var isSetByUser));
        Assert.AreEqual("main", name);
        Assert.IsTrue(isSetByUser);
        Assert.AreEqual(1, metaData.CommitBranchBySid.Count);
    }
}
