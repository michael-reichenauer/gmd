using gmd.Cui.App;
using gmd.Installation;

namespace gmdTest.Cui.App;

[TestClass]
public class ConfigDlgTest
{
    // The integration branches are typed as one line, separated by commas or spaces
    [TestMethod]
    public void TestIntegrationBranchNamesAreParsed()
    {
        CollectionAssert.AreEqual(
            new[] { "staging", "next", "qa" },
            ConfigDlg.ParseNames(" staging, next  qa,,staging ").ToArray()
        );
        Assert.AreEqual(0, ConfigDlg.ParseNames("  ").Count);
    }

    // Greyed out when a package manager installed gmd, which updates it with stable releases alone,
    // and they say so, rather than sit there as boxes that do nothing
    [TestMethod]
    public void TestTheUpdateBoxesSayWhenAPackageManagerUpdatesGmd()
    {
        Assert.AreEqual("Auto update to new releases", ConfigDlg.AutoUpdateText(null));
        Assert.AreEqual("Allow preview releases", ConfigDlg.AllowPreviewText(null));
        Assert.AreEqual("Auto update (Homebrew updates gmd)", ConfigDlg.AutoUpdateText(PackageManager.Homebrew));
        Assert.AreEqual(
            "Allow preview releases (not with Homebrew)",
            ConfigDlg.AllowPreviewText(PackageManager.Homebrew)
        );
    }
}
