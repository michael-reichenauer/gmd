using System.Runtime.InteropServices;
using gmd.Common;
using gmd.Installation;

namespace gmdTest.Installation;

// Which release gmd updates to, and which of its assets is the binary that runs here
[TestClass]
public class ReleaseChoiceTest
{
    static readonly Release Stable = NewRelease("1.6.1444.518", false, "gmd_linux_x64");
    static readonly Release NewerPreview = NewRelease("1.6.1444.870", true, "gmd_linux_x64");
    static readonly Release OlderPreview = NewRelease("1.6.1444.400", true, "gmd_linux_x64");

    [TestMethod]
    public void TestTheStableReleaseUnlessPreviewsAreAllowed()
    {
        var releases = new Releases { StableRelease = Stable, PreRelease = NewerPreview };

        Assert.AreSame(Stable, ReleaseChoice.Select(releases, allowPreview: false));
        Assert.AreSame(NewerPreview, ReleaseChoice.Select(releases, allowPreview: true));
    }

    // A preview older than the stable release is never chosen, so allowing previews never goes back
    [TestMethod]
    public void TestAPreviewOnlyWhenItIsNewer()
    {
        var releases = new Releases { StableRelease = Stable, PreRelease = OlderPreview };

        Assert.AreSame(Stable, ReleaseChoice.Select(releases, allowPreview: true));
    }

    [TestMethod]
    public void TestAPreviewWithNoBinariesIsNotChosen()
    {
        var releases = new Releases { StableRelease = Stable, PreRelease = NewRelease("1.6.1444.870", true) };

        Assert.AreSame(Stable, ReleaseChoice.Select(releases, allowPreview: true));
    }

    [TestMethod]
    public void TestTheBinaryNamesOfEachPlatform()
    {
        Assert.AreEqual("gmd_linux_x64 gmd_linux", Names(OSPlatform.Linux, Architecture.X64));
        Assert.AreEqual("gmd_linux_arm64 gmd_linux", Names(OSPlatform.Linux, Architecture.Arm64));
        Assert.AreEqual("gmd_linux", Names(OSPlatform.Linux, Architecture.X86));
        Assert.AreEqual("gmd_osx_arm64", Names(OSPlatform.OSX, Architecture.Arm64));
        Assert.AreEqual("gmd_windows", Names(OSPlatform.Windows, Architecture.X64));
        Assert.AreEqual("", Names(OSPlatform.FreeBSD, Architecture.X64));
    }

    // The first name a release has, so a release from before the arm64 build, which has only
    // 'gmd_linux', is still installed on Linux
    [TestMethod]
    public void TestTheBinaryIsTheFirstNameTheReleaseHas()
    {
        var names = ReleaseChoice.BinaryNames(OSPlatform.Linux, Architecture.Arm64);

        var release = NewRelease("1.6.1444.518", false, "gmd_linux", "gmd_linux_arm64", "gmd_linux_x64");
        Assert.AreEqual("gmd_linux_arm64", ReleaseChoice.Binary(release, names)?.Name);

        var old = NewRelease("0.80.1300.10", false, "gmd_linux", "gmd_windows");
        Assert.AreEqual("gmd_linux", ReleaseChoice.Binary(old, names)?.Name);

        var windowsOnly = NewRelease("1.6.1444.518", false, "gmd.exe", "gmd_windows");
        Assert.IsNull(ReleaseChoice.Binary(windowsOnly, names));
    }

    static string Names(OSPlatform os, Architecture arch) => string.Join(" ", ReleaseChoice.BinaryNames(os, arch));

    internal static Release NewRelease(string version, bool isPreview, params string[] assetNames) =>
        new()
        {
            Version = version,
            IsPreview = isPreview,
            Assets = assetNames
                .Select(n => new Asset { Name = n, Url = $"https://example.com/v{version}/{n}" })
                .ToArray(),
        };
}
