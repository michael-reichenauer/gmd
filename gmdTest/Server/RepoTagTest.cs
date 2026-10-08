using gmd.Server;
using GitTag = gmd.Git.Tag;

namespace gmdTest.Server;

// The tags the tag list shows: each once, on its commit, and whether origin has it
[TestClass]
public class RepoTagTest
{
    // 'show-ref --dereference' lists an annotated tag twice, the tag object and then its commit
    [TestMethod]
    public void TestATagIsListedOnceOnItsCommit()
    {
        var tags = RepoTag.Of(
            [new GitTag("v1.0", "tagobject"), new GitTag("v1.0", "commit1"), new GitTag("v0.9", "commit0")],
            new Dictionary<string, string> { ["v0.9"] = "commit0" }
        );

        CollectionAssert.AreEqual(
            new[] { new RepoTag("v1.0", "commit1", false), new RepoTag("v0.9", "commit0", true) },
            tags.ToArray()
        );
    }
}
