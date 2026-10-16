using gmd.Cui.Common;
using gmd.Server;

namespace gmdTest.Cui.Common;

// The lines left out of the next commit, by file, as x in the diff keeps them
[TestClass]
public class LeftOutLinesTest
{
    static readonly ChosenLine Removed = new(false, 11, "eleven");
    static readonly ChosenLine Added = new(true, 11, "ELEVEN");

    [TestMethod]
    public void TestLinesAreLeftOutAndPutBack()
    {
        var leftOut = new LeftOutLines();

        leftOut.LeaveOut("a.txt", [Removed, Added]);
        Assert.IsTrue(leftOut.IsLeftOut("a.txt", Added));
        Assert.AreEqual(2, leftOut.CountOf("a.txt"));
        Assert.IsFalse(leftOut.IsLeftOut("b.txt", Added), "By file");

        leftOut.Remove("a.txt", [Added]);
        Assert.AreEqual(1, leftOut.CountOf("a.txt"));
        leftOut.PutBack(["a.txt"]);
        Assert.IsFalse(leftOut.IsAny);
    }

    // They are of the repository shown, and those of another one are forgotten
    [TestMethod]
    public void TestAnotherRepositoryHasNone()
    {
        var leftOut = new LeftOutLines();
        leftOut.ForRepo("/one");
        leftOut.LeaveOut("a.txt", [Added]);

        leftOut.ForRepo("/one");
        Assert.IsTrue(leftOut.IsAny, "The same one keeps them");
        leftOut.ForRepo("/two");
        Assert.IsFalse(leftOut.IsAny);
    }

    // Read again, a line whose number moved, the lines above it changed, is found by its text where one
    // line alone of its version has it; one with no such line, or two, is not found, and stays, to stop a
    // commit rather than go in unseen
    [TestMethod]
    public void TestALineThatMovedIsFoundByItsText()
    {
        var leftOut = new LeftOutLines();
        leftOut.LeaveOut("a.txt", [Removed, Added, new(true, 20, "twice")]);

        var notFound = leftOut.Refind(
            "a.txt",
            [new(false, 11, "eleven"), new(true, 12, "ELEVEN"), new(true, 30, "twice"), new(true, 31, "twice")]
        );

        Assert.AreEqual(1, notFound);
        Assert.IsTrue(leftOut.IsLeftOut("a.txt", new(true, 12, "ELEVEN")), "Moved down a line");
        Assert.IsTrue(leftOut.IsLeftOut("a.txt", Removed), "Where it was");
        Assert.IsTrue(leftOut.IsLeftOut("a.txt", new(true, 20, "twice")), "Not found, two lines have its text");
    }
}
