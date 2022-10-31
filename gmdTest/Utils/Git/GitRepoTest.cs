using gmd.Utils;
using gmd.Utils.Git;
using gmd.Utils.Git.Private;

namespace gmdTest.Utils.Git;

[TestClass]
public class GitRepoTest
{
    [TestMethod]
    public async Task TestLog()
    {
        IGitRepo git = new GitRepo("");

        var result = await git.GetLog();
        Assert.IsTrue(result.IsOk);
        Log.Info($"Count: {result.Value.Count}");
        foreach (var c in result.Value)
        {
            Log.Info($"C: {c.ToString()}");
        }
    }
}