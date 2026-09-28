using gmd.Cui;

namespace gmdTest.Cui;

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
}
