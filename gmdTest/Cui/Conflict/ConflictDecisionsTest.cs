using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// The decisions not saved of the files the resolver left, kept for going back to them, and asked
// about as it closes
[TestClass]
public class ConflictDecisionsTest
{
    static ConflictFile FileOf(string path, string theirs = "t") =>
        new(
            path,
            ConflictKind.BothModified,
            false,
            [new([], new ConflictHunk(0, "HEAD", "", "topic", [new("o")], [], [new(theirs)]))],
            true,
            true
        );

    static ConflictResolution Decided(ConflictFile file)
    {
        var resolution = new ConflictResolution(file);
        resolution.Set(0, HunkChoice.Theirs);
        return resolution;
    }

    // Left with decisions, a file is found with them again; left with none, it is not kept
    [TestMethod]
    public void TestAFileLeftIsFoundWithItsDecisions()
    {
        var decisions = new ConflictDecisions();
        decisions.Keep(Decided(FileOf("a.txt")));
        decisions.Keep(new ConflictResolution(FileOf("b.txt")));

        Assert.AreEqual(1, decisions.Kept.Count, "b.txt had nothing decided");
        var again = decisions.Take(FileOf("a.txt"), out var isLost);
        Assert.AreEqual(HunkChoice.Theirs, again.ChoiceOf(0));
        Assert.IsFalse(isLost);
        Assert.AreEqual(0, decisions.Kept.Count, "Open again, it is no longer kept");
        Assert.IsFalse(decisions.Take(FileOf("b.txt"), out _).IsChanged, "None for a file left with none");
    }

    // Changed on disk since it was left, the decisions are gone, and isLost says so
    [TestMethod]
    public void TestTheDecisionsOfAFileChangedOnDiskAreLost()
    {
        var decisions = new ConflictDecisions();
        decisions.Keep(Decided(FileOf("a.txt")));

        var again = decisions.Take(FileOf("a.txt", theirs: "changed"), out var isLost);

        Assert.IsTrue(isLost);
        Assert.IsFalse(again.IsChanged);
    }

    // Closing asks about every file with decisions not saved, the open one last, where it has any
    [TestMethod]
    public void TestTheUnsavedAreThoseLeftAndTheOpenOne()
    {
        var decisions = new ConflictDecisions();
        decisions.Keep(Decided(FileOf("a.txt")));
        decisions.Keep(Decided(FileOf("b.txt")));
        decisions.Forget("a.txt"); // Saved

        var open = Decided(FileOf("c.txt"));
        CollectionAssert.AreEqual(
            new[] { "b.txt", "c.txt" },
            decisions.Unsaved(open).Select(r => r.File.Path).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "b.txt" },
            decisions.Unsaved(new ConflictResolution(FileOf("c.txt"))).Select(r => r.File.Path).ToArray(),
            "Nothing decided in the open one"
        );
    }
}
