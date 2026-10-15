using gmd.Common;

namespace gmdTest.Common;

// Whether the release the last check found is offered, as ⇓ and the update items, for the gmd that
// runs: its version, whether it is a dev instance, and whether a package manager installed it
[TestClass]
public class ReleasesTest
{
    static readonly Version Running = new("1.8.1445.1141");

    [TestMethod]
    public void TestANewerReleaseIsOffered()
    {
        var releases = new Releases { LatestVersion = "1.8.1445.1200" };

        Assert.IsTrue(releases.IsUpdateAvailable(Running, isDevInstance: false, isPackageManaged: false));
        Assert.IsTrue(releases.IsUpdateAvailable(Running, isDevInstance: false, isPackageManaged: true));
    }

    [TestMethod]
    public void TestTheRunningReleaseOrAnOlderIsNot()
    {
        Assert.IsFalse(new Releases { LatestVersion = "1.8.1445.1141" }.IsUpdateAvailable(Running, false, false));
        Assert.IsFalse(new Releases { LatestVersion = "1.7.1445.1049" }.IsUpdateAvailable(Running, false, false));
    }

    // Its version is no release's
    [TestMethod]
    public void TestADevInstanceIsOfferedNone()
    {
        var releases = new Releases { LatestVersion = "1.8.1445.1200" };

        Assert.IsFalse(releases.IsUpdateAvailable(Running, isDevInstance: true, isPackageManaged: false));
    }

    // The package manager has no previews, and the config is shared by every gmd on the machine, so
    // another one, which updates itself and allows previews, can have found one
    [TestMethod]
    public void TestAGmdThatAPackageManagerInstalledIsOfferedNoPreview()
    {
        var releases = new Releases { LatestVersion = "1.8.1445.1200", IsPreview = true };

        Assert.IsFalse(releases.IsUpdateAvailable(Running, isDevInstance: false, isPackageManaged: true));
        Assert.IsTrue(releases.IsUpdateAvailable(Running, isDevInstance: false, isPackageManaged: false));
    }
}
