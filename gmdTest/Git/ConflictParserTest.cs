using gmd.Git;
using gmd.Git.Private;

namespace gmdTest.Git;

// The parser takes text and returns records, so these need no fake, no repository and no driver —
// just raw string literals and the identity that ToText(Parse(x)) is x.
//
// That identity is the whole point. Everything the resolver writes is built by changing one hunk
// and calling ToText, so as long as it holds, resolving one conflict cannot rewrite the rest of the
// file: no line endings converted, no final newline added, no marker size or label lost.
[TestClass]
public class ConflictParserTest
{
    static ConflictFile Parse(string text) => ConflictParser.Parse("f.txt", ConflictKind.BothModified, text);

    static void AssertRoundTrips(string text)
    {
        var actual = ConflictParser.ToText(Parse(text));

        Assert.AreEqual(text, actual, "Parse then ToText must give back the exact same text");
    }

    const string TwoSided = """
        one
        <<<<<<< HEAD
        ours
        =======
        theirs
        >>>>>>> topic
        two

        """;

    const string WithBase = """
        one
        <<<<<<< HEAD
        ours
        ||||||| merged common ancestors
        base
        =======
        theirs
        >>>>>>> topic
        two

        """;

    // ---- round trips ----

    [TestMethod]
    public void TestRoundTripOfAPlainConflict() => AssertRoundTrips(TwoSided);

    [TestMethod]
    public void TestRoundTripOfADiff3Conflict() => AssertRoundTrips(WithBase);

    [TestMethod]
    public void TestRoundTripOfAFileWithNoConflicts() => AssertRoundTrips("one\ntwo\nthree\n");

    [TestMethod]
    public void TestRoundTripOfAnEmptyFile() => AssertRoundTrips("");

    [TestMethod]
    public void TestRoundTripWithNoTrailingNewline() => AssertRoundTrips("one\ntwo");

    [TestMethod]
    public void TestRoundTripOfCrLf() =>
        AssertRoundTrips("one\r\n<<<<<<< HEAD\r\nours\r\n=======\r\ntheirs\r\n>>>>>>> topic\r\ntwo\r\n");

    // A merge of an LF file and a CRLF file really does produce this, which is why the terminator
    // is kept per line rather than once per file
    [TestMethod]
    public void TestRoundTripOfMixedLineEndings() =>
        AssertRoundTrips("one\r\ntwo\n<<<<<<< HEAD\nours\r\n=======\ntheirs\n>>>>>>> topic\r\nlast");

    [TestMethod]
    public void TestRoundTripOfTwoConflictsInOneFile() =>
        AssertRoundTrips(
            "a\n<<<<<<< HEAD\no1\n=======\nt1\n>>>>>>> topic\nb\n<<<<<<< HEAD\no2\n=======\nt2\n>>>>>>> topic\nc\n"
        );

    [TestMethod]
    public void TestRoundTripOfAConflictAtTheVeryStart() =>
        AssertRoundTrips("<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> topic\ntail\n");

    [TestMethod]
    public void TestRoundTripOfAConflictAtTheVeryEndWithNoNewline() =>
        AssertRoundTrips("head\n<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> topic");

    [TestMethod]
    public void TestRoundTripOfAnEmptySide() =>
        AssertRoundTrips("a\n<<<<<<< HEAD\n=======\ntheirs\n>>>>>>> topic\nb\n");

    [TestMethod]
    public void TestRoundTripOfBothSidesEmpty() => AssertRoundTrips("a\n<<<<<<< HEAD\n=======\n>>>>>>> topic\nb\n");

    [TestMethod]
    public void TestRoundTripOfMarkersWithNoLabels() =>
        AssertRoundTrips("a\n<<<<<<<\nours\n=======\ntheirs\n>>>>>>>\nb\n");

    // '.gitattributes' can set 'conflict-marker-size', so the markers are kept verbatim rather than
    // regenerated at git's default of seven
    [TestMethod]
    public void TestRoundTripOfLongerMarkers() =>
        AssertRoundTrips("a\n<<<<<<<<<<< HEAD\nours\n===========\ntheirs\n>>>>>>>>>>> topic\nb\n");

    // ---- text that only looks like a marker ----

    [TestMethod]
    public void TestUnterminatedConflictIsPlainText()
    {
        var text = "a\n<<<<<<< HEAD\nours\n=======\ntheirs\n";

        var file = Parse(text);

        Assert.AreEqual(0, file.Hunks.Count, "Without '>>>>>>>' there is no conflict, only text");
        AssertRoundTrips(text);
    }

    [TestMethod]
    public void TestSplitWithNoStartIsPlainText()
    {
        var text = "a\n=======\nb\n>>>>>>> topic\nc\n";

        Assert.AreEqual(0, Parse(text).Hunks.Count);
        AssertRoundTrips(text);
    }

    [TestMethod]
    public void TestEndWithNoSplitIsPlainText()
    {
        var text = "a\n<<<<<<< HEAD\nours\n>>>>>>> topic\nb\n";

        Assert.AreEqual(0, Parse(text).Hunks.Count);
        AssertRoundTrips(text);
    }

    // Six is not a marker at any conflict-marker-size, since git's minimum is seven
    [TestMethod]
    public void TestSixCharactersIsNotAMarker()
    {
        var text = "a\n<<<<<< HEAD\nours\n======\ntheirs\n>>>>>> topic\nb\n";

        Assert.AreEqual(0, Parse(text).Hunks.Count);
        AssertRoundTrips(text);
    }

    // A markdown rule or a signature line starts with seven of a character but is followed by
    // something other than a space, so it is text
    [TestMethod]
    public void TestSevenCharactersFollowedByTextIsNotAMarker()
    {
        var text = "a\n=======x\nb\n";

        Assert.AreEqual(0, Parse(text).Hunks.Count);
        AssertRoundTrips(text);
    }

    // ---- what was parsed ----

    [TestMethod]
    public void TestSidesAndLabels()
    {
        var hunk = Parse(TwoSided).Hunks[0];

        Assert.AreEqual("HEAD", hunk.OursLabel);
        Assert.AreEqual("topic", hunk.TheirsLabel);
        Assert.AreEqual("ours", ConflictParser.ToText(hunk.Ours).TrimEnd('\n'));
        Assert.AreEqual("theirs", ConflictParser.ToText(hunk.Theirs).TrimEnd('\n'));
        Assert.IsFalse(hunk.HasBase, "The default 'merge' style records no common ancestor");
        Assert.IsFalse(hunk.IsResolved);
    }

    // During a rebase git names the side by commit rather than by branch, which is what the view
    // shows instead of saying 'theirs' — during a rebase that word means the opposite of expected
    [TestMethod]
    public void TestRebaseStyleLabels()
    {
        var text = "<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> 4a15fb2 (Add gamma)\n";

        var hunk = Parse(text).Hunks[0];

        Assert.AreEqual("HEAD", hunk.OursLabel);
        Assert.AreEqual("4a15fb2 (Add gamma)", hunk.TheirsLabel);
    }

    [TestMethod]
    public void TestBaseIsParsedFromADiff3File()
    {
        var hunk = Parse(WithBase).Hunks[0];

        Assert.IsTrue(hunk.HasBase);
        Assert.AreEqual("merged common ancestors", hunk.BaseLabel);
        Assert.AreEqual("base", ConflictParser.ToText(hunk.Base).TrimEnd('\n'));
    }

    [TestMethod]
    public void TestHunksAreIndexedInOrder()
    {
        var text = "<<<<<<< HEAD\no1\n=======\nt1\n>>>>>>> topic\n<<<<<<< HEAD\no2\n=======\nt2\n>>>>>>> topic\n";

        var hunks = Parse(text).Hunks;

        Assert.AreEqual(2, hunks.Count);
        Assert.AreEqual(0, hunks[0].Index);
        Assert.AreEqual(1, hunks[1].Index);
        Assert.AreEqual(2, Parse(text).UnresolvedCount);
    }

    // ---- choices ----

    [TestMethod]
    [DataRow(HunkChoice.Ours, "one\nours\ntwo\n")]
    [DataRow(HunkChoice.Theirs, "one\ntheirs\ntwo\n")]
    [DataRow(HunkChoice.OursThenTheirs, "one\nours\ntheirs\ntwo\n")]
    [DataRow(HunkChoice.TheirsThenOurs, "one\ntheirs\nours\ntwo\n")]
    [DataRow(HunkChoice.Neither, "one\ntwo\n")]
    public void TestChoiceReplacesTheWholeConflict(HunkChoice choice, string expected)
    {
        var file = ConflictParser.SetChoice(Parse(TwoSided), 0, choice);

        Assert.AreEqual(expected, ConflictParser.ToText(file));
        Assert.AreEqual(0, file.UnresolvedCount);
    }

    // Choosing a side drops the base section along with the markers
    [TestMethod]
    public void TestChoiceOnADiff3ConflictDropsTheBase()
    {
        var file = ConflictParser.SetChoice(Parse(WithBase), 0, HunkChoice.Ours);

        Assert.AreEqual("one\nours\ntwo\n", ConflictParser.ToText(file));
    }

    [TestMethod]
    public void TestManualChoiceUsesTheGivenLines()
    {
        var manual = ConflictParser.ToLines("edited\nby hand\n");

        var file = ConflictParser.SetChoice(Parse(TwoSided), 0, HunkChoice.Manual, manual);

        Assert.AreEqual("one\nedited\nby hand\ntwo\n", ConflictParser.ToText(file));
    }

    [TestMethod]
    public void TestOnlyTheChosenHunkChanges()
    {
        var text =
            "a\n<<<<<<< HEAD\no1\n=======\nt1\n>>>>>>> topic\nb\n<<<<<<< HEAD\no2\n=======\nt2\n>>>>>>> topic\nc\n";

        var file = ConflictParser.SetChoice(Parse(text), 1, HunkChoice.Theirs);

        Assert.AreEqual("a\n<<<<<<< HEAD\no1\n=======\nt1\n>>>>>>> topic\nb\nt2\nc\n", ConflictParser.ToText(file));
        Assert.AreEqual(1, file.UnresolvedCount);
    }

    // The last line of a block has no terminator when the conflict ended the file, so resolving one
    // in the middle would otherwise join two lines together
    [TestMethod]
    public void TestChosenBlockKeepsTheFileLineStructure()
    {
        var file = ConflictParser.SetChoice(Parse(TwoSided), 0, HunkChoice.Ours);

        Assert.AreEqual("one\nours\ntwo\n", ConflictParser.ToText(file));
    }

    [TestMethod]
    public void TestResolvingAConflictThatEndsTheFileKeepsTheBlockAsItWas()
    {
        var text = "head\n<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> topic";

        var file = ConflictParser.SetChoice(Parse(text), 0, HunkChoice.Ours);

        // 'ours' is followed by '=======' in the file, so it has a newline of its own even though
        // the end marker, being the last line, has none
        Assert.AreEqual("head\nours\n", ConflictParser.ToText(file));
    }

    // Hand edited text is the one block that can end without a newline — every parsed block is
    // followed by a marker line, so it always has one. Without the repair the line after the
    // conflict would be joined onto the last edited line.
    [TestMethod]
    public void TestManualTextWithNoTrailingNewlineDoesNotJoinTheNextLine()
    {
        var manual = ConflictParser.ToLines("edited");

        var file = ConflictParser.SetChoice(Parse(TwoSided), 0, HunkChoice.Manual, manual);

        Assert.AreEqual("one\nedited\ntwo\n", ConflictParser.ToText(file));
    }

    [TestMethod]
    public void TestManualTextWithNoTrailingNewlineAtTheEndOfTheFile()
    {
        var text = "head\n<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> topic";

        var file = ConflictParser.SetChoice(Parse(text), 0, HunkChoice.Manual, ConflictParser.ToLines("edited"));

        Assert.AreEqual("head\nedited", ConflictParser.ToText(file), "The file still ends without a newline");
    }

    [TestMethod]
    public void TestManualTextKeepsTheFilesLineEndings()
    {
        var text = "one\r\n<<<<<<< HEAD\r\nours\r\n=======\r\ntheirs\r\n>>>>>>> topic\r\ntwo\r\n";

        var file = ConflictParser.SetChoice(Parse(text), 0, HunkChoice.Manual, ConflictParser.ToLines("edited"));

        Assert.AreEqual("one\r\nedited\r\ntwo\r\n", ConflictParser.ToText(file), "CRLF taken from the end marker");
    }

    [TestMethod]
    public void TestCrLfIsKeptWhenChoosingASide()
    {
        var text = "one\r\n<<<<<<< HEAD\r\nours\r\n=======\r\ntheirs\r\n>>>>>>> topic\r\ntwo\r\n";

        var file = ConflictParser.SetChoice(Parse(text), 0, HunkChoice.Theirs);

        Assert.AreEqual("one\r\ntheirs\r\ntwo\r\n", ConflictParser.ToText(file));
    }

    // ---- bases filled in from a separate diff3 merge ----

    [TestMethod]
    public void TestSetBasesFillsInTheCommonAncestor()
    {
        var file = ConflictParser.SetBases(Parse(TwoSided), [ConflictParser.ToLines("base\n")]);

        Assert.IsTrue(file.Hunks[0].HasBase);
        Assert.AreEqual("base\n", ConflictParser.ToText(file.Hunks[0].Base));
    }

    // ---- mapping an ancestor from a separately computed diff3 merge ----

    // The case that makes mapping by position wrong. 'git merge' writes two nearby changes as one
    // conflict; 'merge-file --diff3' writes the same three versions as two, split at the common line
    // between them. Taking its conflicts in order would put the second ancestor against the first
    // conflict — here, there is only one conflict to put it against at all.
    [TestMethod]
    public void TestBaseIsJoinedBackUpWhenDiff3SplitsAConflict()
    {
        var file = Parse("head\n<<<<<<< HEAD\nO1\nmid\nO2\n=======\nT1\nmid\nT2\n>>>>>>> topic\ntail\n");
        var merged = Parse(
            "head\n"
                + "<<<<<<< ours\nO1\n||||||| base\nB1\n=======\nT1\n>>>>>>> theirs\n"
                + "mid\n"
                + "<<<<<<< ours\nO2\n||||||| base\nB2\n=======\nT2\n>>>>>>> theirs\n"
                + "tail\n"
        );
        Assert.AreEqual(1, file.Hunks.Count);
        Assert.AreEqual(2, merged.Hunks.Count, "diff3 split what the merge wrote as one");

        var withBase = ConflictParser.SetBasesFrom(file, merged);

        // The common line between the two is part of the ancestor of the joined region
        Assert.AreEqual("B1\nmid\nB2\n", ConflictParser.ToText(withBase.Hunks[0].Base));
    }

    // The straightforward case still works, i.e. joining is not applied where nothing was split
    [TestMethod]
    public void TestBaseIsMappedOneForOneWhenTheGroupingAgrees()
    {
        var file = Parse(
            "a\n<<<<<<< HEAD\nO1\n=======\nT1\n>>>>>>> topic\nb\n<<<<<<< HEAD\nO2\n=======\nT2\n>>>>>>> topic\nc\n"
        );
        var merged = Parse(
            "a\n<<<<<<< ours\nO1\n||||||| base\nB1\n=======\nT1\n>>>>>>> theirs\n"
                + "b\n<<<<<<< ours\nO2\n||||||| base\nB2\n=======\nT2\n>>>>>>> theirs\nc\n"
        );

        var withBase = ConflictParser.SetBasesFrom(file, merged);

        Assert.AreEqual("B1\n", ConflictParser.ToText(withBase.Hunks[0].Base));
        Assert.AreEqual("B2\n", ConflictParser.ToText(withBase.Hunks[1].Base));
    }

    // An ancestor that is empty is a real answer — both sides added lines where there were none
    [TestMethod]
    public void TestAnEmptyBaseIsMappedAsEmpty()
    {
        var file = Parse("a\n<<<<<<< HEAD\nO1\n=======\nT1\n>>>>>>> topic\nb\n");
        var merged = Parse("a\n<<<<<<< ours\nO1\n||||||| base\n=======\nT1\n>>>>>>> theirs\nb\n");

        var withBase = ConflictParser.SetBasesFrom(file, merged);

        Assert.AreEqual(0, withBase.Hunks[0].Base.Count);
        Assert.IsFalse(withBase.Hunks[0].HasBase, "So the pane says there is none rather than showing nothing");
    }

    // A merge of different content altogether, i.e. the file changed on disk since. Mapping it would
    // show an ancestor belonging to some other version of the file.
    [TestMethod]
    public void TestBaseIsNotMappedWhenTheOursTextDoesNotMatch()
    {
        var file = Parse("a\n<<<<<<< HEAD\nO1\n=======\nT1\n>>>>>>> topic\nb\n");
        var merged = Parse("a\nEXTRA\n<<<<<<< ours\nO1\n||||||| base\nB1\n=======\nT1\n>>>>>>> theirs\nb\n");

        var withBase = ConflictParser.SetBasesFrom(file, merged);

        Assert.IsFalse(withBase.Hunks[0].HasBase);
    }

    // A mismatched count means the file has been edited since the bases were computed, so pairing
    // them by position would show the wrong ancestor for a conflict — better to show none
    [TestMethod]
    public void TestSetBasesIsIgnoredWhenTheCountsDisagree()
    {
        var file = ConflictParser.SetBases(
            Parse(TwoSided),
            [ConflictParser.ToLines("a\n"), ConflictParser.ToLines("b\n")]
        );

        Assert.IsFalse(file.Hunks[0].HasBase);
    }

    // ---- lines ----

    [TestMethod]
    [DataRow("", 0)]
    [DataRow("a", 1)]
    [DataRow("a\n", 1)]
    [DataRow("a\nb", 2)]
    [DataRow("a\nb\n", 2)]
    [DataRow("\n", 1)]
    public void TestLineCount(string text, int expected) =>
        Assert.AreEqual(expected, ConflictParser.ToLines(text).Count);

    [TestMethod]
    public void TestEachLineKeepsItsOwnTerminator()
    {
        var lines = ConflictParser.ToLines("a\r\nb\nc");

        Assert.AreEqual("\r\n", lines[0].Eol);
        Assert.AreEqual("\n", lines[1].Eol);
        Assert.AreEqual("", lines[2].Eol, "The last line had no newline after it");
        Assert.AreEqual("a", lines[0].Text, "The terminator is not part of the text");
    }

    // A lone '\r' is not a line break, so it stays inside the text
    [TestMethod]
    public void TestLoneCarriageReturnIsNotALineBreak()
    {
        var lines = ConflictParser.ToLines("a\rb\n");

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual("a\rb", lines[0].Text);
    }
}
