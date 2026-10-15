using gmd.Git;
using gmd.Git.Private;

namespace gmdTest.Git;

// The patch of some of a file's changes: to apply to the old version, which then has the chosen
// changes and none of the others, and to apply in reverse to the new version, which then has the
// chosen changes taken back and keeps the others. PatchIntegrationTest has git apply them.
[TestClass]
public class FilePatchTest
{
    const string TwoHunks = """
        diff --git a/a.txt b/a.txt
        index 1111111..2222222 100644
        --- a/a.txt
        +++ b/a.txt
        @@ -1,4 +1,5 @@
         one
        -two
        +TWO
        +two and a half
         three
         four
        @@ -10,3 +11,3 @@
         ten
        -eleven
        +ELEVEN
         twelve

        """;

    static FilePatch Parse(string text) => AssertOk(PatchParser.Parse(text));

    static Func<PatchLine, bool> Chosen(params string[] lines) => l => lines.Contains(l.ToString());

    // A removed line not chosen stays, unchanged, an added one not chosen is left out, and a hunk with
    // no change chosen goes. The 'index' line goes too, naming as it does the whole change.
    [TestMethod]
    public void TestAPatchToApplyHasTheChosenChangesAlone()
    {
        var patch = AssertOk(Parse(TwoHunks).ForApply(Chosen("+two and a half")));

        Assert.AreEqual(
            """
            diff --git a/a.txt b/a.txt
            --- a/a.txt
            +++ b/a.txt
            @@ -1,4 +1,5 @@
             one
             two
            +two and a half
             three
             four

            """,
            patch.ToText()
        );
    }

    // Where a hunk starts in the new version follows from what the chosen changes before it add or
    // take away: here none, where the whole change added a line
    [TestMethod]
    public void TestAHunkStartsWhereTheChangesChosenBeforeItPutIt()
    {
        var patch = AssertOk(Parse(TwoHunks).ForApply(Chosen("-eleven", "+ELEVEN")));

        Assert.AreEqual("@@ -10,3 +10,3 @@", patch.Hunks.Single().ToString());
        Assert.AreEqual(" ten 10 10, -eleven 11 0, +ELEVEN 0 11,  twelve 12 12", Numbered(patch));
    }

    // To apply in reverse, an added line not chosen stays, unchanged, and a removed one not chosen is
    // left out; the new version, which it is applied to, keeps its line numbers
    [TestMethod]
    public void TestAPatchToApplyInReverseHasTheChosenChangesAlone()
    {
        var first = AssertOk(Parse(TwoHunks).ForReverseApply(Chosen("-two", "+TWO")));
        var second = AssertOk(Parse(TwoHunks).ForReverseApply(Chosen("-eleven", "+ELEVEN")));

        Assert.AreEqual(
            """
            diff --git a/a.txt b/a.txt
            --- a/a.txt
            +++ b/a.txt
            @@ -1,5 +1,5 @@
             one
            -two
            +TWO
             two and a half
             three
             four

            """,
            first.ToText()
        );
        Assert.AreEqual("@@ -11,3 +11,3 @@", second.Hunks.Single().ToString());
    }

    // A change's removed and added lines are paired, as the diff view shows them side by side, so a
    // removed line that stays, stays where it is among the added ones: with two made TWO alone, TWO is
    // where two was, before three, not after it, as taking the lines in git's order would have it
    [TestMethod]
    public void TestAChangesLinesArePairedAsTheyAreShown()
    {
        var patch = Parse(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,3 +1,3 @@\n"
                + " one\n-two\n-three\n+TWO\n+THREE\n"
        );

        Assert.AreEqual(" one, -two, +TWO,  three", Lines(AssertOk(patch.ForApply(Chosen("-two", "+TWO")))));
        Assert.AreEqual(" one,  two, -three, +THREE", Lines(AssertOk(patch.ForApply(Chosen("-three", "+THREE")))));
        Assert.AreEqual(
            " one,  TWO, -three, +THREE",
            Lines(AssertOk(patch.ForReverseApply(Chosen("-three", "+THREE"))))
        );

        static string Lines(FilePatch p) => string.Join(", ", p.Hunks.SelectMany(h => h.Lines));
    }

    // No change chosen is nothing to apply
    [TestMethod]
    public void TestNoChangeChosenIsAnEmptyPatch()
    {
        Assert.IsTrue(AssertOk(Parse(TwoHunks).ForApply(_ => false)).IsEmpty);
        Assert.IsTrue(AssertOk(Parse(TwoHunks).ForReverseApply(_ => false)).IsEmpty);
    }

    const string NewFile = """
        diff --git a/n.txt b/n.txt
        new file mode 100644
        index 0000000..3333333
        --- /dev/null
        +++ b/n.txt
        @@ -0,0 +1,3 @@
        +a
        +b
        +c

        """;

    // Some lines of a new file make a new file of those lines. Some taken back from it leave a file
    // that is there in both versions, so the patch is of a change, from the path, not '/dev/null'.
    [TestMethod]
    public void TestSomeLinesOfANewFile()
    {
        var toApply = AssertOk(Parse(NewFile).ForApply(Chosen("+a", "+c")));
        var toReverse = AssertOk(Parse(NewFile).ForReverseApply(Chosen("+b")));

        Assert.AreEqual(
            """
            diff --git a/n.txt b/n.txt
            new file mode 100644
            --- /dev/null
            +++ b/n.txt
            @@ -0,0 +1,2 @@
            +a
            +c

            """,
            toApply.ToText()
        );
        Assert.AreEqual(
            """
            diff --git a/n.txt b/n.txt
            --- a/n.txt
            +++ b/n.txt
            @@ -1,2 +1,3 @@
             a
            +b
             c

            """,
            toReverse.ToText()
        );
    }

    const string DeletedFile = """
        diff --git a/d.txt b/d.txt
        deleted file mode 100644
        index 3333333..0000000
        --- a/d.txt
        +++ /dev/null
        @@ -1,3 +0,0 @@
        -a
        -b
        -c

        """;

    // Some lines of a deleted file taken out leave a file that is there in both versions, a change;
    // some put back by applying in reverse make a file of those lines
    [TestMethod]
    public void TestSomeLinesOfADeletedFile()
    {
        var toApply = AssertOk(Parse(DeletedFile).ForApply(Chosen("-b")));
        var toReverse = AssertOk(Parse(DeletedFile).ForReverseApply(Chosen("-b")));

        Assert.AreEqual(
            """
            diff --git a/d.txt b/d.txt
            --- a/d.txt
            +++ b/d.txt
            @@ -1,3 +1,2 @@
             a
            -b
             c

            """,
            toApply.ToText()
        );
        Assert.AreEqual(
            """
            diff --git a/d.txt b/d.txt
            deleted file mode 100644
            --- a/d.txt
            +++ /dev/null
            @@ -1 +0,0 @@
            -b

            """,
            toReverse.ToText()
        );
    }

    // The last line of a file with no newline after it is the last of its version: a line added after
    // it, without the change that gives it its newline, would come after the end of the file
    [TestMethod]
    public void TestTheEndOfAFileWithNoNewlineIsChosenTogether()
    {
        var patch = Parse(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1,2 @@\n"
                + "-a\n\\ No newline at end of file\n+a\n+b\n"
        );

        StringAssert.Contains(AssertError(patch.ForApply(Chosen("+b"))).Message, "can only be chosen together");
        AssertOk(patch.ForApply(Chosen("-a", "+a")));
        AssertOk(patch.ForApply(_ => true));
    }

    // A change of the header goes with the file whole: a part of its lines would leave it out, or take
    // it along unseen
    [TestMethod]
    [DataRow("Binary files a/a.bin and b/a.bin differ\n", "binary")]
    [DataRow("similarity index 90%\nrename from a.txt\nrename to b.txt\n", "renamed")]
    [DataRow("old mode 100644\nnew mode 100755\n", "mode")]
    [DataRow("index 1111111..2222222 160000\n", "submodule")]
    public void TestAChangeOfTheHeaderIsTakenWhole(string header, string why)
    {
        var hunk = why == "binary" ? "" : "--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-x\n+y\n";
        var patch = Parse($"diff --git a/a.txt b/a.txt\n{header}{hunk}");

        StringAssert.Contains(patch.WhyNotChoose, why);
        StringAssert.Contains(AssertError(patch.ForApply(_ => true)).Message, why);
    }

    static string Numbered(FilePatch patch) =>
        string.Join(", ", patch.Hunks.SelectMany(h => h.Lines).Select(l => $"{l} {l.OldLineNbr} {l.NewLineNbr}"));
}
