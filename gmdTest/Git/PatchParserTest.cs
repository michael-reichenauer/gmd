using gmd.Git;
using gmd.Git.Private;

namespace gmdTest.Git;

// The patch of one file, read and written back. What matters most is that ToText(Parse(x)) is x,
// byte for byte, for every shape git writes a patch in: a patch of some of the lines is one changed
// and written by ToText, and git is to be given what it wrote, less the lines left out.
[TestClass]
public class PatchParserTest
{
    const string Modified = """
        diff --git a/a.txt b/a.txt
        index 3b18e51..4c53a7e 100644
        --- a/a.txt
        +++ b/a.txt
        @@ -1,4 +1,4 @@ section
         one
        -two
        +TWO
         three
         four
        @@ -10 +10,2 @@
        -ten
        +TEN
        +eleven

        """;

    static string RoundTrip(string text) => AssertOk(PatchParser.Parse(text)).ToText();

    [TestMethod]
    public void TestAModifiedFileRoundTrips()
    {
        Assert.AreEqual(Modified, RoundTrip(Modified));
    }

    // The lines of each hunk with what they are and where, numbered as the diff view numbers its rows,
    // and a count git leaves out read as the 1 it is
    [TestMethod]
    public void TestTheLinesAreNumberedInBothVersions()
    {
        var patch = AssertOk(PatchParser.Parse(Modified));

        Assert.AreEqual(4, patch.Header.Count);
        Assert.AreEqual(" section", patch.Hunks[0].Section);
        Assert.AreEqual(
            " one 1 1, -two 2 0, +TWO 0 2,  three 3 3,  four 4 4 | -ten 10 0, +TEN 0 10, +eleven 0 11",
            string.Join(" | ", patch.Hunks.Select(h => string.Join(", ", h.Lines.Select(Described))))
        );
        Assert.AreEqual(
            (10, 1, 10, 2),
            (patch.Hunks[1].OldStart, patch.Hunks[1].OldCount, patch.Hunks[1].NewStart, patch.Hunks[1].NewCount)
        );

        static string Described(PatchLine l) => $"{l} {l.OldLineNbr} {l.NewLineNbr}";
    }

    // A new file, a deleted one, and the '-0,0' and '+0,0' of the side they have no lines on
    [TestMethod]
    [DataRow(
        """
            diff --git a/new.txt b/new.txt
            new file mode 100644
            index 0000000..d68dd40
            --- /dev/null
            +++ b/new.txt
            @@ -0,0 +1,2 @@
            +a
            +b

            """
    )]
    [DataRow(
        """
            diff --git a/old.txt b/old.txt
            deleted file mode 100755
            index d68dd40..0000000
            --- a/old.txt
            +++ /dev/null
            @@ -1 +0,0 @@
            -a

            """
    )]
    public void TestANewAndADeletedFileRoundTrip(string patch)
    {
        Assert.AreEqual(patch, RoundTrip(patch));
    }

    // A '\r' of a file's '\r\n' line ends is part of the line, which a patch ends with '\n' alone, and
    // where the file has no newline at its end git says so after the line, which stays with it
    [TestMethod]
    public void TestLineEndsAndNoNewlineAtTheEndRoundTrip()
    {
        var patch =
            "diff --git a/a.txt b/a.txt\n"
            + "--- a/a.txt\n"
            + "+++ b/a.txt\n"
            + "@@ -1,2 +1,2 @@\n"
            + " one\r\n"
            + "-two\r\n"
            + "\\ No newline at end of file\n"
            + "+two\n"
            + "\\ No newline at end of file\n";

        var parsed = AssertOk(PatchParser.Parse(patch));

        Assert.AreEqual(patch, parsed.ToText());
        Assert.AreEqual("one\r", parsed.Hunks[0].Lines[0].Text);
        Assert.AreEqual("\\ No newline at end of file", parsed.Hunks[0].Lines[1].NoNewline);
        Assert.AreEqual(3, parsed.Hunks[0].Lines.Count, "The markers are not lines");
    }

    // A hunk is read by its counts, so a line of the file that looks like the start of a hunk, an
    // empty line, a byte order mark and spaces at the end of the last line are all just lines
    [TestMethod]
    public void TestLinesThatLookLikeOthersAreJustLines()
    {
        var patch =
            "diff --git a/a.txt b/a.txt\n"
            + "--- a/a.txt\n"
            + "+++ b/a.txt\n"
            + "@@ -1,3 +1,3 @@\n"
            + "-﻿first\n"
            + "+@@ -1 +1 @@\n"
            + " \n"
            + "-diff --git a/b b/b\n"
            + "+last  \n";

        var parsed = AssertOk(PatchParser.Parse(patch));

        Assert.AreEqual(patch, parsed.ToText());
        Assert.AreEqual(5, parsed.Hunks.Single().Lines.Count);
    }

    // Anything else is not read as a patch rather than read wrongly: a count the lines do not make,
    // a second file, and text that is no patch at all
    [TestMethod]
    [DataRow("diff --git a/a b/a\n--- a/a\n+++ b/a\n@@ -1,2 +1,2 @@\n-x\n+y\n", "cannot be read")]
    [DataRow("diff --git a/a b/a\n--- a/a\n+++ b/a\n@@ -1 +1 @@\n-x\n+y\n+z\n", "cannot be read")]
    [DataRow("diff --git a/a b/a\n--- a/a\n+++ b/a\ndiff --git a/b b/b\n", "more than one file")]
    [DataRow("not a patch\n", "does not start")]
    [DataRow("diff --git a/a b/a", "does not end")]
    [DataRow("", "empty")]
    public void TestWhatIsNotAPatchIsNotRead(string text, string why)
    {
        StringAssert.Contains(AssertError(PatchParser.Parse(text)).Message, why);
    }
}
