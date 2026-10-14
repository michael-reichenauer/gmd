using gmd.Installation;

namespace gmdTest.Installation;

// Which package manager installed gmd, told by where its binary is, the binary itself rather than the
// link to it on the PATH (RunningGmd.BinaryPath)
[TestClass]
public class PackageManagerTest
{
    [TestMethod]
    public void TestHomebrewOnMacOSAndOnLinux()
    {
        Assert.AreEqual(PackageManager.Homebrew, PackageManager.Of("/opt/homebrew/Cellar/gmd/1.6.1445.379/bin/gmd"));
        Assert.AreEqual(
            PackageManager.Homebrew,
            PackageManager.Of("/home/linuxbrew/.linuxbrew/Cellar/gmd/1.6.1445.379/bin/gmd")
        );
        Assert.AreEqual(PackageManager.Homebrew, PackageManager.Of("/usr/local/Cellar/gmd/1.6.1445.379/bin/gmd"));
    }

    [TestMethod]
    public void TestHomebrewSaysTheCommandThatUpdatesGmd()
    {
        Assert.AreEqual("Homebrew", PackageManager.Homebrew.Name);
        Assert.AreEqual("brew update && brew upgrade gmd", PackageManager.Homebrew.UpdateCommand);
    }

    // Where install.sh and the Windows installer put it, and where a user may have, which gmd updates
    [TestMethod]
    public void TestAGmdInstalledOtherwiseHasNone()
    {
        Assert.IsNull(PackageManager.Of("/home/anna/gmd/gmd"));
        Assert.IsNull(PackageManager.Of("/usr/local/bin/gmd"));
        Assert.IsNull(PackageManager.Of(@"C:\ProgramData\gmd\gmd.exe"));
        Assert.IsNull(PackageManager.Of("/home/anna/Cellar/gmd/gmd"));
        Assert.IsNull(PackageManager.Of("/opt/homebrew/Cellar/other/1.0/bin/gmd"));
        Assert.IsNull(PackageManager.Of("gmd"));
        Assert.IsNull(PackageManager.Of(null));
    }
}
