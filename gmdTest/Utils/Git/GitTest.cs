using gmd.Utils.Git;
using gmd.Utils.Git.Private;

namespace gmdTest.Utils.Git;

[TestClass]
public class GitTest
{
    [TestMethod]
    public void TestDo()
    {
        IGitService git = new GitService();

        var result = git.Do("name");
        Assert.AreEqual("namename", result);
    }
}