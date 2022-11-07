using gmd.Utils.Git;

namespace gmdTest.Utils.Git;

[TestClass]
public class GitRepoTest
{
    [TestMethod]
    public async void TestLog()
    {
        IGit git = new gmd.Utils.Git.Private.Git("");

        var log = await git.GetLogAsync();
        Assert.IsFalse(log.IsError);

        Log.Info($"Count: {log.Value.Count}");
        foreach (var c in log.Value)
        {
            Log.Info($"C: {c}");
        }
    }

    [TestMethod]
    public async void TestGetBranches()
    {
        IGit git = new gmd.Utils.Git.Private.Git("");

        var branches = await git.GetBranchesAsync();
        Assert.IsFalse(branches.IsError);

        Log.Info($"Count: {branches.Value.Count}");
        foreach (var b in branches.Value)
        {
            Log.Info($"C: {b}");
        }
    }
}
