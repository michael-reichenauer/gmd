using gmd;

namespace gmdTest;

[TestClass]
public class ProgramTest
{
    // What a crash says once the terminal is given back, where gmd used to end without a word: what
    // failed, where the log is and that the next start begins it anew, and where to report it
    [TestMethod]
    public void TestACrashSaysWhereTheLogIsAndWhereToReportIt()
    {
        var message = Program.CrashMessage(
            "NullReferenceException: Object reference not set to an instance of an object.",
            "/home/anna/gmd.log"
        );

        Assert.AreEqual(
            """
            gmd stopped on an unexpected error, sorry: NullReferenceException: Object reference not set to an instance of an object.

            What happened is in the log, which the next start of gmd begins anew, so copy it first:
              /home/anna/gmd.log
            Please report the problem, with the log and the version (gmd --version), at:
              https://github.com/michael-reichenauer/gmd/issues
            """,
            message
        );
    }
}
