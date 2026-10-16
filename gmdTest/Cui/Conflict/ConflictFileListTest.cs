using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// The list of conflicted files beside the resolver: which to go to next, how wide it is, and its rows
[TestClass]
public class ConflictFileListTest
{
    static HashSet<string> Passed(params string[] paths) => [.. paths];

    static ConflictFileList ListOf(string open, string[] resolved, params string[] paths) =>
        new(paths.Select(p => new ConflictedFile(p, ConflictKind.BothModified)).ToList(), resolved.ToHashSet(), open);

    // The next file still conflicted, round the end, past the ones resolved
    [TestMethod]
    public void TestTheNextFileIsTheNextStillConflicted()
    {
        var list = ListOf("b", ["c"], "a", "b", "c", "d");

        Assert.AreEqual("d", list.NextOpen(1), "Past c, resolved");
        Assert.AreEqual("a", list.NextOpen(-1));
        Assert.AreEqual("a", ListOf("d", [], "a", "b", "c", "d").NextOpen(1), "Round the end");
        Assert.AreEqual("", ListOf("a", ["b"], "a", "b").NextOpen(1), "None left but the open one");
    }

    // Once a file ended: closed is the end, a file gone to is opened, one done is opened again while it
    // is conflicted still, and one resolved or passed over goes on to the next one still conflicted
    [TestMethod]
    public void TestTheFileOpenedAfterOneEnded()
    {
        HashSet<string> none = [];

        Assert.AreEqual("", ListOf("a", [], "a", "b").After(new(ConflictEnd.Closed), none));
        Assert.AreEqual("a", ListOf("b", [], "a", "b").After(new(ConflictEnd.GoTo, "a"), none));
        Assert.AreEqual("a", ListOf("a", [], "a", "b").After(new(ConflictEnd.Done), none), "Conflicted still");
        Assert.AreEqual("b", ListOf("a", ["a"], "a", "b").After(new(ConflictEnd.Done), none), "Resolved");
        Assert.AreEqual("", ListOf("b", ["a", "b"], "a", "b").After(new(ConflictEnd.Done), none), "None left");
    }

    // A file whose question was cancelled is passed over, and so is every file passed over before: with
    // each of them cancelled, the workspace closes rather than asking about them round and round
    [TestMethod]
    public void TestFilesPassedOverAreNotAskedAboutAgain()
    {
        var list = ListOf("a", [], "a", "b", "c");

        Assert.AreEqual("b", list.After(new(ConflictEnd.Skipped), Passed("a")));
        Assert.AreEqual("c", (list with { Open = "b" }).After(new(ConflictEnd.Skipped), Passed("a", "b")));
        Assert.AreEqual("", (list with { Open = "c" }).After(new(ConflictEnd.Skipped), Passed("a", "b", "c")));
        Assert.AreEqual(
            "a",
            (list with { Open = "c" }).After(new(ConflictEnd.GoTo, "a"), Passed("a", "b", "c")),
            "Asked for"
        );
    }

    [TestMethod]
    public void TestTheListIsDrawnOnAWideTerminal()
    {
        var list = ListOf("a", [], "a.txt", "b.txt");

        Assert.AreEqual(0, list.WidthFor(149));
        Assert.AreEqual(24, list.WidthFor(150));
    }

    // The title says how many are left, the open file is marked with how far it is, a resolved file
    // ticked, and a long path cut from the front to keep its name
    [TestMethod]
    public void TestTheRowsMarkTheOpenFileAndTheResolved()
    {
        var list = ListOf(
            "src/Pricing.cs",
            ["README.md"],
            "src/Pricing.cs",
            "README.md",
            "some/long/folder/name/Order.cs"
        );

        Assert.AreEqual(
            """
            Conflicts 2 left              │
            ▌● src/Pricing.cs  1 of 2     │
             ✓ README.md                  │
             ● …long/folder/name/Order.cs │
            """,
            string.Join("\n", list.Rows(31, 0, false, "1 of 2").Select(r => r.ToString()))
        );
    }
}
