namespace gmdTest.Utils;

[TestClass]
public class AsserterTest
{
    // Fail returns the exception to throw, saying where it failed
    [TestMethod]
    public void TestFailSaysWhere()
    {
        var e = Asserter.Fail("Unknown kind");

        StringAssert.StartsWith(e.Message, "Unknown kind, at ");
        StringAssert.Contains(e.Message, $"{nameof(AsserterTest)}.cs(");
        StringAssert.EndsWith(e.Message, $") {nameof(TestFailSaysWhere)}");
    }

    // A broken invariant is reported before it is thrown, so that it stops gmd even where a catch
    // takes the exception for an ordinary failure, and it carries a stack trace already, for the log
    // that reports it
    [TestMethod]
    public void TestFailIsReportedBeforeItIsThrown()
    {
        List<AssertException> reported = [];
        void OnFailed(AssertException e) => reported.Add(e);
        Asserter.Failed += OnFailed;
        try
        {
            var e = Asserter.Fail("Broken");

            Assert.AreSame(e, reported.Single());
            StringAssert.Contains(e.StackTrace, nameof(TestFailIsReportedBeforeItIsThrown));
            Assert.IsFalse(e.StackTrace!.Contains("Asserter.Fail"), "From where it failed, not inside the asserter");
        }
        finally
        {
            Asserter.Failed -= OnFailed;
        }
    }

    // Requires throws, with what it required, where it used to report and carry on
    [TestMethod]
    public void TestRequiresThrowsWithTheExpression()
    {
        var index = -1;

        Asserter.Requires(index < 0);
        var e = Assert.ThrowsExactly<AssertException>(() => Asserter.Requires(index >= 0));

        StringAssert.StartsWith(e.Message, "Requires index >= 0, at ");
    }

    // Once thrown, the stack trace is the throw's, as for any exception
    [TestMethod]
    public void TestAThrownAssertHasItsOwnStackTrace()
    {
        var e = Assert.ThrowsExactly<AssertException>(() => ThrowAssert());

        StringAssert.Contains(e.StackTrace, nameof(ThrowAssert));
    }

    static void ThrowAssert() => throw Asserter.Fail("Thrown");
}
