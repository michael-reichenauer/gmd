using gmd.Git;

namespace gmdTest.Git;

// A branch or tag name typed into a dialog ends up on a git command line that is not quoted, so what
// git itself would refuse ('git check-ref-format') is refused before anything is touched, and so is
// what would split the argument. Only the forms a user is likely to type are covered, git is still
// the final word.
[TestClass]
public class RefNameTest
{
    [TestMethod]
    public void TestOrdinaryBranchNamesAreValid()
    {
        Assert.IsTrue(RefName.IsValid("dev"));
        Assert.IsTrue(RefName.IsValid("feature/some-work"));
        Assert.IsTrue(RefName.IsValid("mr/fix_1.2"));
        Assert.IsTrue(RefName.IsValid("release-1.0"));
    }

    // A space reached git as two arguments: 'checkout -b fix dev' made 'fix' at dev's tip. Git
    // refuses white space anyway, but allows a '"', which would split the argument too.
    [TestMethod]
    public void TestNamesThatWouldBeTwoArgumentsAreInvalid()
    {
        Assert.IsFalse(RefName.IsValid("my branch"));
        Assert.IsFalse(RefName.IsValid("my\tbranch"));
        Assert.IsFalse(RefName.IsValid("my\"branch"));
    }

    [TestMethod]
    public void TestNamesGitWouldRefuseAreInvalid()
    {
        Assert.IsFalse(RefName.IsValid(""));
        Assert.IsFalse(RefName.IsValid("a~b"), "Tilde");
        Assert.IsFalse(RefName.IsValid("a^b"), "Caret");
        Assert.IsFalse(RefName.IsValid("a:b"), "Colon");
        Assert.IsFalse(RefName.IsValid("a?b"), "Question mark");
        Assert.IsFalse(RefName.IsValid("a*b"), "Asterisk");
        Assert.IsFalse(RefName.IsValid("a[b"), "Open bracket");
        Assert.IsFalse(RefName.IsValid("a\\b"), "Backslash");
        Assert.IsFalse(RefName.IsValid("a..b"), "Two dots");
        Assert.IsFalse(RefName.IsValid("a@{b"), "At brace");
        Assert.IsFalse(RefName.IsValid("a//b"), "Two slashes");
        Assert.IsFalse(RefName.IsValid("/dev"), "Leading slash");
        Assert.IsFalse(RefName.IsValid("dev/"), "Trailing slash");
        Assert.IsFalse(RefName.IsValid("-dev"), "Leading dash, would be read as an option");
        Assert.IsFalse(RefName.IsValid(".dev"), "Leading dot");
        Assert.IsFalse(RefName.IsValid("dev."), "Trailing dot");
        Assert.IsFalse(RefName.IsValid("dev.lock"), "Reserved suffix");
        Assert.IsFalse(RefName.IsValid("dev/.x"), "A part starting with a dot");
        Assert.IsFalse(RefName.IsValid("@"), "HEAD's other name");
        Assert.IsFalse(RefName.IsValid("a\u007fb"), "A control character");
    }
}
