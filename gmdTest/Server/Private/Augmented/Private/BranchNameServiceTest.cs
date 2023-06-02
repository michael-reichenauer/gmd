using gmd.Server.Private.Augmented.Private;

[TestClass]
public class BranchNameServiceTest
{
    [TestMethod]
    public void TestNames()
    {
        var bs = new BranchNameService();
        // var c = new WorkCommit("1", "Merge branch 'develop' into main", "Merge branch 'develop' into main",
        //      "author", DateTime.Now, new[] { "2", "3" });

        var fd = bs.ParseSubject("Merge branch 'develop' into main");
        Assert.AreEqual("develop", fd.From);
        Assert.AreEqual("main", fd.Into);
        Assert.AreEqual(false, fd.IsPullMerge);
        Assert.AreEqual(false, fd.IsPullRequest);

        fd = bs.ParseSubject("Merge branch 'dev' of https://github.com/michael-reichenauer/gmd into dev");
        Assert.AreEqual("dev", fd.From);
        Assert.AreEqual("dev", fd.Into);
        Assert.AreEqual(true, fd.IsPullMerge);
        Assert.AreEqual(false, fd.IsPullRequest);
    }
}