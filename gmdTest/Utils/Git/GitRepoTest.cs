using gmd.Utils;
using gmd.Utils.Git;
using gmd.Utils.Git.Private;

namespace gmdTest.Utils.Git;

[TestClass]
public class GitRepoTest
{
    [TestMethod]
    public void TestLog()
    {
        IGitRepo git = new GitRepo("");

        var result = git.Log();
        Assert.AreEqual(0, result.Length);
    }
}