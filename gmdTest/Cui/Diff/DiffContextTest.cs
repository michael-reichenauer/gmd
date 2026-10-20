using gmd.Cui.Diff;
using gmd.Server;

namespace gmdTest.Cui.Diff;

[TestClass]
public class DiffContextTest
{
    // Stepping the context, 15 lines at a time from the default

    [TestMethod]
    public void TestContextStepsUpAndBackDownByFifteenLines()
    {
        Assert.AreEqual(21, DiffContext.Step(DiffContext.Default, 1));
        Assert.AreEqual(36, DiffContext.Step(21, 1));
        Assert.AreEqual(21, DiffContext.Step(36, -1));
        Assert.AreEqual(DiffContext.Default, DiffContext.Step(21, -1));
    }

    // Never less than the default, and a context between, which nothing produces today, steps to it
    [TestMethod]
    public void TestContextStaysAtTheDefaultDown()
    {
        Assert.AreEqual(DiffContext.Default, DiffContext.Step(DiffContext.Default, -1));
        Assert.AreEqual(DiffContext.Default, DiffContext.Step(10, -1));
    }

    // The whole file is shown from the menu: up from it is where it is, and down from it is back to
    // the context it was shown at before, the default when it was not known
    [TestMethod]
    public void TestDownFromTheWholeFileIsBackToTheContextBefore()
    {
        Assert.AreEqual(DiffContext.WholeFile, DiffContext.Step(DiffContext.WholeFile, 1, 21));
        Assert.AreEqual(21, DiffContext.Step(DiffContext.WholeFile, -1, 21));
        Assert.AreEqual(DiffContext.Default, DiffContext.Step(DiffContext.WholeFile, -1));
    }

    // The header says what the file is showing, the menu item says what picking it would show, and
    // the whole file is worded the same either way
    [TestMethod]
    public void TestAContextIsNamedForTheHeaderAndForAMenuItem()
    {
        Assert.AreEqual("(context 21)", DiffContext.ToText(21));
        Assert.AreEqual("21 lines", DiffContext.ToItemText(21));

        Assert.AreEqual("(whole file)", DiffContext.ToText(DiffContext.WholeFile));
        Assert.AreEqual("whole file", DiffContext.ToItemText(DiffContext.WholeFile));
    }

    // Whether more context would show more of a file, which git does not say, since it says nothing
    // of how long a file is: the hunks do

    // One hunk from the first line, which ends in fewer unchanged lines than the context: git would
    // have shown more after the change had the file gone on
    [TestMethod]
    public void TestOneHunkFromTheTopEndingShortOfTheContextIsTheWholeFile()
    {
        var file = FileOf(Hunk(1, 1, unchangedBefore: 2, unchangedAfter: 4));

        Assert.IsTrue(DiffContext.IsWholeFileShown(file, 6));
        Assert.IsFalse(DiffContext.IsWholeFileShown(file, 4), "Four after the change, as many as asked for");
    }

    // The hunk starts below the first line on either side, so there are lines above it to show
    [TestMethod]
    public void TestAHunkBelowTheFirstLineIsNotTheWholeFile()
    {
        Assert.IsFalse(DiffContext.IsWholeFileShown(FileOf(Hunk(5, 5, 6, 2)), 6));
        Assert.IsFalse(DiffContext.IsWholeFileShown(FileOf(Hunk(1, 3, 0, 2)), 6), "Two lines were added above");
    }

    [TestMethod]
    public void TestTwoHunksAreNotTheWholeFile()
    {
        Assert.IsFalse(DiffContext.IsWholeFileShown(FileOf(Hunk(1, 1, 0, 6), Hunk(20, 20, 6, 0)), 6));
    }

    // A file added or removed whole is one hunk of every line, from the first one, a side with no
    // line at 0, and no unchanged line after the last
    [TestMethod]
    public void TestAFileAddedWholeIsTheWholeFile()
    {
        var added = new SectionDiff(
            "0,0 +1,2",
            0,
            0,
            1,
            2,
            [new LineDiff(DiffMode.DiffAdded, "one"), new LineDiff(DiffMode.DiffAdded, "two")]
        );

        Assert.IsTrue(DiffContext.IsWholeFileShown(FileOf(added), DiffContext.Default));
    }

    // A binary file or a rename alone, which git shows no lines of, has no more lines to show
    [TestMethod]
    public void TestAFileWithNoHunkHasNoMoreToShow()
    {
        Assert.IsTrue(DiffContext.IsWholeFileShown(FileOf(), DiffContext.Default));
    }

    [TestMethod]
    public void TestTheWholeFileContextIsTheWholeFile()
    {
        Assert.IsTrue(DiffContext.IsWholeFileShown(FileOf(Hunk(30, 30, 6, 6)), DiffContext.WholeFile));
    }

    static FileDiff FileOf(params SectionDiff[] sections) =>
        new("a.txt", "a.txt", false, false, DiffMode.DiffModified, sections);

    // A hunk from a line on each side, of unchanged lines around one line changed
    static SectionDiff Hunk(int leftLine, int rightLine, int unchangedBefore, int unchangedAfter)
    {
        List<LineDiff> lines = [];
        lines.AddRange(
            Enumerable.Range(0, unchangedBefore).Select(i => new LineDiff(DiffMode.DiffSame, $"before {i}"))
        );
        lines.Add(new LineDiff(DiffMode.DiffRemoved, "old"));
        lines.Add(new LineDiff(DiffMode.DiffAdded, "new"));
        lines.AddRange(Enumerable.Range(0, unchangedAfter).Select(i => new LineDiff(DiffMode.DiffSame, $"after {i}")));

        var count = unchangedBefore + 1 + unchangedAfter;
        return new SectionDiff($"{leftLine},{count} +{rightLine},{count}", leftLine, count, rightLine, count, lines);
    }
}
