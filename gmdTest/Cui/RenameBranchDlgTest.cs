using gmd.Cui;

namespace gmdTest.Cui;

// The name typed into the rename dialog ends up on a git command line that Cmd does not quote, so
// what git itself would refuse ('git check-ref-format') is refused here instead, before the branch
// is touched. Only the forms a user is likely to type are covered, git is still the final word.
[TestClass]
public class RenameBranchDlgTest
{
    [TestMethod]
    public void TestOrdinaryBranchNamesAreValid()
    {
        Assert.IsTrue(RenameBranchDlg.IsValidName("dev"));
        Assert.IsTrue(RenameBranchDlg.IsValidName("feature/some-work"));
        Assert.IsTrue(RenameBranchDlg.IsValidName("mr/fix_1.2"));
        Assert.IsTrue(RenameBranchDlg.IsValidName("release-1.0"));
    }

    // White space is the one git would accept in some shells but Cmd cannot pass on as one argument
    [TestMethod]
    public void TestNamesWithWhiteSpaceAreInvalid()
    {
        Assert.IsFalse(RenameBranchDlg.IsValidName("my branch"));
        Assert.IsFalse(RenameBranchDlg.IsValidName("my\tbranch"));
    }

    [TestMethod]
    public void TestNamesGitWouldRefuseAreInvalid()
    {
        Assert.IsFalse(RenameBranchDlg.IsValidName(""));
        Assert.IsFalse(RenameBranchDlg.IsValidName("a~b"), "Tilde");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a^b"), "Caret");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a:b"), "Colon");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a?b"), "Question mark");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a*b"), "Asterisk");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a[b"), "Open bracket");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a\\b"), "Backslash");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a..b"), "Two dots");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a@{b"), "At brace");
        Assert.IsFalse(RenameBranchDlg.IsValidName("a//b"), "Two slashes");
        Assert.IsFalse(RenameBranchDlg.IsValidName("/dev"), "Leading slash");
        Assert.IsFalse(RenameBranchDlg.IsValidName("dev/"), "Trailing slash");
        Assert.IsFalse(RenameBranchDlg.IsValidName("-dev"), "Leading dash, would be read as an option");
        Assert.IsFalse(RenameBranchDlg.IsValidName(".dev"), "Leading dot");
        Assert.IsFalse(RenameBranchDlg.IsValidName("dev."), "Trailing dot");
        Assert.IsFalse(RenameBranchDlg.IsValidName("dev.lock"), "Reserved suffix");
    }
}
