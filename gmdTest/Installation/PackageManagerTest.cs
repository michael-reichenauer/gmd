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

    // In the user's AppData, or in Program Files when installed for all users, in any case of letters,
    // as Windows names folders
    [TestMethod]
    public void TestWinget()
    {
        Assert.AreEqual(
            PackageManager.Winget,
            PackageManager.Of(
                @"C:\Users\anna\AppData\Local\Microsoft\WinGet\Packages\MichaelReichenauer.gmd_Microsoft.Winget.Source_8wekyb3d8bbwe\gmd.exe"
            )
        );
        Assert.AreEqual(
            PackageManager.Winget,
            PackageManager.Of(
                @"C:\Program Files\WinGet\Packages\MichaelReichenauer.gmd_Microsoft.Winget.Source_8wekyb3d8bbwe\gmd.exe"
            )
        );
        Assert.AreEqual(
            PackageManager.Winget,
            PackageManager.Of(@"c:\users\anna\appdata\local\microsoft\winget\packages\michaelreichenauer.GMD_x\GMD.EXE")
        );
    }

    // Updated once gmd is closed, since Windows cannot replace a program that runs
    [TestMethod]
    public void TestWingetSaysTheCommandThatUpdatesGmd()
    {
        Assert.AreEqual("winget", PackageManager.Winget.Name);
        Assert.AreEqual("winget upgrade --id MichaelReichenauer.gmd", PackageManager.Winget.UpdateCommand);
        Assert.IsTrue(PackageManager.Winget.IsClosedFirst);
        Assert.IsFalse(PackageManager.Homebrew.IsClosedFirst);
    }

    // Where install.sh and the Windows installer put it, and where a user may have, which gmd updates
    [TestMethod]
    public void TestAGmdInstalledOtherwiseHasNone()
    {
        Assert.IsNull(PackageManager.Of("/home/anna/gmd/gmd"));
        Assert.IsNull(PackageManager.Of("/usr/local/bin/gmd"));
        Assert.IsNull(PackageManager.Of(@"C:\ProgramData\gmd\gmd.exe"));
        Assert.IsNull(PackageManager.Of(@"C:\Users\anna\AppData\Local\Programs\gmd\gmd.exe"));
        Assert.IsNull(PackageManager.Of(@"C:\Users\anna\AppData\Local\Microsoft\WinGet\Packages\Other.App_x\gmd.exe"));
        Assert.IsNull(PackageManager.Of("/home/anna/Cellar/gmd/gmd"));
        Assert.IsNull(PackageManager.Of("/opt/homebrew/Cellar/other/1.0/bin/gmd"));
        Assert.IsNull(PackageManager.Of("gmd"));
        Assert.IsNull(PackageManager.Of(null));
    }
}
