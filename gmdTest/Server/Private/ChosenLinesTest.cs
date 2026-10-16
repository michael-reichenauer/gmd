using gmd.Git;
using gmd.Git.Private;
using gmd.Server;
using gmd.Server.Private;

namespace gmdTest.Server.Private;

// The lines chosen in the diff, found in the file's patch as it is read again when they are acted on
[TestClass]
public class ChosenLinesTest
{
    static readonly FilePatch Patch = AssertOk(
        PatchParser.Parse(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,3 +1,3 @@\n" + " one\n-two\n+\tTWO\n three\n"
        )
    );

    // By version, number and text; a line's tab is three spaces in the diff the view shows
    [TestMethod]
    public void TestTheChosenLinesAreFoundInThePatch()
    {
        var found = AssertOk(ChosenLines.Find(Patch, [new(false, 2, "two"), new(true, 2, "   TWO")], "a.txt"));

        Assert.AreEqual("-two, +\tTWO", string.Join(", ", found));
    }

    // A line not found as it was shown, its text or its number changed, or no longer a change, means
    // the file has changed since the diff was read, and nothing is done rather than something else
    [TestMethod]
    [DataRow(false, 2, "TWO")]
    [DataRow(true, 2, "two")]
    [DataRow(false, 1, "one")]
    [DataRow(true, 9, "nine")]
    public void TestALineNotAsShownIsNotFound(bool isAdded, int lineNbr, string text)
    {
        var error = AssertError(ChosenLines.Find(Patch, [new(false, 2, "two"), new(isAdded, lineNbr, text)], "a.txt"));

        Assert.AreEqual("a.txt has changed since the diff was read: r reads it again", error.Message);
    }

    [TestMethod]
    public void TestNoLinesIsNoChoice()
    {
        AssertError(ChosenLines.Find(Patch, [], "a.txt"));
    }
}
