using gmd.Common;
using gmd.Cui;

namespace gmdTest.Cui;

// What the question before an update says, and where its change log is. The dialog itself is out
// of reach of the end-to-end tests, which run a dev instance, and a dev instance never offers one.
[TestClass]
public class UpdateDlgTest
{
    static readonly DateTime Running = new(2026, 10, 13, 10, 22, 0);
    static readonly DateTime Latest = new(2026, 10, 19, 18, 40, 0);

    // Both versions lined up, and that gmd quits once updated, which is better known before than after
    [TestMethod]
    public void TestTheMessageSaysBothVersionsAndThatGmdQuits()
    {
        Assert.AreEqual(
            $$"""
            Gmd 1.6 (1444.518) is available.

            This version:  1.5 (1444.48)    built 2026-10-13{{" "}}
            New version:   1.6 (1444.518)   built 2026-10-19{{" "}}

            Gmd quits once it is updated, and the new
            version runs when gmd is started again.
            """,
            UpdateDlg.Message(new Version(1, 5, 1444, 48), Running, new Version(1, 6, 1444, 518), Latest, false)
        );
    }

    [TestMethod]
    public void TestAPreviewIsSaidToBeOne()
    {
        var message = UpdateDlg.Message(
            new Version(1, 5, 1444, 48),
            Running,
            new Version(1, 6, 1444, 518),
            Latest,
            true
        );

        StringAssert.StartsWith(message, "Gmd 1.6 (1444.518) is available, a preview.\n");
    }

    // A release's changes are in the change log, with those of every release skipped since this one,
    // and a preview's in its release notes, since the change log only has them once released
    [TestMethod]
    public void TestTheChangeLogOfAReleaseAndOfAPreview()
    {
        Assert.AreEqual(
            "https://github.com/michael-reichenauer/gmd/blob/main/CHANGELOG.md",
            UpdateDlg.ChangeLogUrl(new Releases { LatestVersion = "1.6.1444.518", IsPreview = false })
        );
        Assert.AreEqual(
            "https://github.com/michael-reichenauer/gmd/releases/tag/v1.6.1444.518",
            UpdateDlg.ChangeLogUrl(new Releases { LatestVersion = "1.6.1444.518", IsPreview = true })
        );
    }
}
