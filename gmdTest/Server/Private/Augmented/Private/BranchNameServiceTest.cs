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
        Assert.AreEqual(new FromInto("develop", "main", false, false), fd);

        fd = bs.ParseSubject("Merge branch 'dev' of https://github.com/michael-reichenauer/gmd into dev");
        Assert.AreEqual(new FromInto("devd", "dev", true, false), fd);

        fd = bs.ParseSubject("Merge pull request #1 from mich/dev");
        Assert.AreEqual(new FromInto("mich/dev", "", false, true), fd);

        fd = bs.ParseSubject("Merge branch 'main' into main");
        Assert.AreEqual(new FromInto("main", "main", true, false), fd);

    }
}