using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// The list of conflicted files beside the resolver: which to go to next, how wide it is, and its rows
[TestClass]
public class ConflictFileListTest
{
    static HashSet<string> Passed(params string[] paths) => [.. paths];

    static Dictionary<string, string> Progress(params (string Path, string How)[] files) =>
        files.ToDictionary(f => f.Path, f => f.How);

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

    // Drawn at the width it asks for, less the view's scrollbar column, which the rows are given, the
    // widest path is whole, the open one's too with ten conflicts or more to say how far it is
    [TestMethod]
    public void TestTheWidestPathIsWholeAtTheWidthOfTheList()
    {
        var list = ListOf("src/Pricing.cs", [], "src/Pricing.cs", "a.txt");

        var rows = list.Rows(list.WidthFor(200) - 1, 0, false, Progress(("src/Pricing.cs", "10 of 12")))
            .Select(r => r.ToString())
            .ToList();

        StringAssert.StartsWith(rows[1], "▌● src/Pricing.cs  10 of 12");
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
            string.Join("\n", list.Rows(31, 0, false, Progress(("src/Pricing.cs", "1 of 2"))).Select(r => r.ToString()))
        );
    }

    // A file left with decisions not saved says how far it is, as the open one does: they are kept
    // for going back to it, and asked about as the resolver closes
    [TestMethod]
    public void TestAFileLeftWithDecisionsSaysHowFarItIs()
    {
        var list = ListOf("a.txt", [], "a.txt", "b.txt", "c.txt");

        Assert.AreEqual(
            """
            Conflicts 3 left         │
            ▌● a.txt  0 of 1         │
             ● b.txt  1 of 2         │
             ● c.txt                 │
            """,
            string.Join(
                "\n",
                list.Rows(26, 0, false, Progress(("a.txt", "0 of 1"), ("b.txt", "1 of 2"))).Select(r => r.ToString())
            )
        );
    }
}
