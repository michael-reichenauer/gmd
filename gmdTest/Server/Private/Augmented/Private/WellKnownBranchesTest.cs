using gmd.Server.Private.Augmented.Private;

namespace gmdTest.Server.Private.Augmented.Private;

// The names the inference treats as the trunk's or an integration branch's, which decide both which
// branch goes on at a branch point and which merges count towards it (see TryDecideBranchPoint)
[TestClass]
public class WellKnownBranchesTest
{
    [TestMethod]
    public void TestIntegrationNames()
    {
        Assert.IsTrue(WellKnownBranches.IsIntegrationName("develop"));
        Assert.IsTrue(WellKnownBranches.IsIntegrationName("dev"));
        Assert.IsTrue(WellKnownBranches.IsIntegrationName("development"));
        Assert.IsTrue(WellKnownBranches.IsIntegrationName("owner/develop"), "Also as the last part");
        Assert.IsTrue(WellKnownBranches.IsIntegrationName("v2_develop"), "One per major version");
        Assert.IsTrue(WellKnownBranches.IsIntegrationName("v1-develop"));

        Assert.IsFalse(WellKnownBranches.IsIntegrationName("main"), "The trunk is not an integration branch");
        Assert.IsFalse(WellKnownBranches.IsIntegrationName("feature/develop-login"));
        Assert.IsFalse(WellKnownBranches.IsIntegrationName("hotfix-dev"), "Only 'develop' is taken as an ending");
        Assert.IsFalse(WellKnownBranches.IsIntegrationName("devices"));
    }

    [TestMethod]
    public void TestReleaseNames()
    {
        Assert.IsTrue(WellKnownBranches.IsReleaseName("release/1.0"));
        Assert.IsTrue(WellKnownBranches.IsReleaseName("hotfix/login"));
        Assert.IsTrue(WellKnownBranches.IsReleaseName("support/2.x"));
        Assert.IsTrue(WellKnownBranches.IsReleaseName("owner/release/1.0"));

        Assert.IsFalse(WellKnownBranches.IsReleaseName("feature/release-notes"));
        Assert.IsFalse(WellKnownBranches.IsReleaseName("prepare-release"), "A feature");
        Assert.IsFalse(WellKnownBranches.IsReleaseName("v2_release"), "A trunk of its own, one per major version");
        Assert.IsFalse(WellKnownBranches.IsReleaseName("release"));
        Assert.IsFalse(WellKnownBranches.IsReleaseName("releases"));
        Assert.IsFalse(WellKnownBranches.IsReleaseName("dev"));
    }

    // The names a branch brought up to date is merged from: the trunk, an integration branch, either
    // of them from another remote, or a remote alone ('git merge upstream')
    [TestMethod]
    public void TestTrunkOrIntegrationNames()
    {
        Assert.IsTrue(WellKnownBranches.IsTrunkOrIntegrationName("main"));
        Assert.IsTrue(WellKnownBranches.IsTrunkOrIntegrationName("master"));
        Assert.IsTrue(WellKnownBranches.IsTrunkOrIntegrationName("trunk"));
        Assert.IsTrue(WellKnownBranches.IsTrunkOrIntegrationName("upstream/main"));
        Assert.IsTrue(WellKnownBranches.IsTrunkOrIntegrationName("v2_develop"));
        Assert.IsTrue(WellKnownBranches.IsTrunkOrIntegrationName("origin"));
        Assert.IsTrue(WellKnownBranches.IsTrunkOrIntegrationName("upstream"));

        Assert.IsFalse(WellKnownBranches.IsTrunkOrIntegrationName("feature/x"));
        Assert.IsFalse(WellKnownBranches.IsTrunkOrIntegrationName("maintenance"));
    }
}
