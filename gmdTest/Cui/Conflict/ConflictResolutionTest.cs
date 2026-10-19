using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// What the user has decided, kept out of the view so it can be tested without a terminal — the
// same reason ContentScroll and Hoover exist.
[TestClass]
public class ConflictResolutionTest
{
    static FileLine[] Lines(params string[] texts) => texts.Select(t => new FileLine(t)).ToArray();

    static ConflictFile FileWith(int hunkCount)
    {
        var segments = new List<ConflictSegment>();
        for (int i = 0; i < hunkCount; i++)
        {
            segments.Add(new ConflictSegment(Lines($"before {i}"), null));
            segments.Add(
                new ConflictSegment([], new ConflictHunk(i, "HEAD", "", "topic", Lines($"o{i}"), [], Lines($"t{i}")))
            );
        }

        return new ConflictFile("f.txt", ConflictKind.BothModified, false, segments, true, true);
    }

    [TestMethod]
    public void TestNothingIsResolvedToStartWith()
    {
        var resolution = new ConflictResolution(FileWith(3));

        Assert.AreEqual(3, resolution.HunkCount);
        Assert.AreEqual(3, resolution.UnresolvedCount);
        Assert.IsFalse(resolution.IsFullyResolved);
        Assert.IsFalse(resolution.IsChanged);
    }

    [TestMethod]
    public void TestChoosingCountsDown()
    {
        var resolution = new ConflictResolution(FileWith(3));

        resolution.Set(0, HunkChoice.Ours);
        resolution.Set(2, HunkChoice.Base);

        Assert.AreEqual(1, resolution.UnresolvedCount);
        Assert.IsTrue(resolution.IsChanged);
        Assert.AreEqual(HunkChoice.Ours, resolution.ChoiceOf(0));
        Assert.AreEqual(HunkChoice.None, resolution.ChoiceOf(1));
        Assert.AreEqual(HunkChoice.Base, resolution.ChoiceOf(2));
    }

    // Setting None is how a decision is taken back, which the un-choose key needs
    [TestMethod]
    public void TestChoosingNoneUndoesTheDecision()
    {
        var resolution = new ConflictResolution(FileWith(1));
        resolution.Set(0, HunkChoice.Manual, "edited");

        resolution.Set(0, HunkChoice.None);

        Assert.AreEqual(1, resolution.UnresolvedCount);
        Assert.AreEqual("", resolution.ManualTextOf(0));
    }

    // Choosing a side after editing by hand must not leave the edit behind to be written
    // The result pane shows a conflict as it is typed from a copy, the decisions so far with the text
    // typed for it, and the decision is made only when the typing is done
    [TestMethod]
    public void TestTypingIsShownFromACopy()
    {
        var resolution = new ConflictResolution(FileWith(2));
        resolution.Set(0, HunkChoice.Theirs);

        var typed = resolution.WithTyped(1, "a\nb");

        Assert.AreEqual(HunkChoice.Theirs, typed.ChoiceOf(0));
        Assert.AreEqual(HunkChoice.Manual, typed.ChoiceOf(1));
        Assert.AreEqual("a\nb", typed.ManualTextOf(1));
        Assert.AreEqual(HunkChoice.None, resolution.ChoiceOf(1), "Not decided until done");
        Assert.AreEqual(1, resolution.UnresolvedCount);
    }

    [TestMethod]
    public void TestChoosingASideForgetsTheHandEdit()
    {
        var resolution = new ConflictResolution(FileWith(1));
        resolution.Set(0, HunkChoice.Manual, "edited");

        resolution.Set(0, HunkChoice.Ours);

        Assert.AreEqual("", resolution.ManualTextOf(0));
        Assert.AreEqual("", resolution.ToResolutions()[0].ManualText);
    }

    // Every conflict is sent, decided or not: the count is what the Git layer checks the file
    // against, so a file that changed on disk is caught rather than resolved by position
    [TestMethod]
    public void TestResolutionsCoverEveryConflictInOrder()
    {
        var resolution = new ConflictResolution(FileWith(3));
        resolution.Set(1, HunkChoice.Theirs);

        var resolutions = resolution.ToResolutions();

        Assert.AreEqual(3, resolutions.Count);
        Assert.AreEqual("0: None, 1: Theirs, 2: None", string.Join(", ", resolutions));
    }

    [TestMethod]
    [DataRow(HunkChoice.Ours, "o0")]
    [DataRow(HunkChoice.Theirs, "t0")]
    [DataRow(HunkChoice.OursThenTheirs, "o0,t0")]
    [DataRow(HunkChoice.TheirsThenOurs, "t0,o0")]
    [DataRow(HunkChoice.Base, "")] // this fixture's conflicts have no ancestor lines
    [DataRow(HunkChoice.None, "")]
    public void TestResultOfEachChoice(HunkChoice choice, string expected)
    {
        var file = FileWith(1);
        var resolution = new ConflictResolution(file);
        resolution.Set(0, choice);

        Assert.AreEqual(expected, string.Join(",", resolution.ResultOf(file.Hunks[0]).Select(l => l.Text)));
    }

    // Taking the ancestor puts back what both sides started from, i.e. undoes both changes. The
    // fixture above has no ancestor lines anywhere, so this needs a conflict that does.
    [TestMethod]
    public void TestResultOfTheBaseIsTheAncestorsLines()
    {
        var hunk = new ConflictHunk(0, "HEAD", "base", "topic", Lines("ours"), Lines("was", "here"), Lines("theirs"));
        var file = new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [new ConflictSegment([], hunk)],
            true,
            true
        );
        var resolution = new ConflictResolution(file);

        resolution.Set(0, HunkChoice.Base);

        Assert.AreEqual("was,here", string.Join(",", resolution.ResultOf(hunk).Select(l => l.Text)));
    }

    [TestMethod]
    public void TestResultOfAHandEdit()
    {
        var file = FileWith(1);
        var resolution = new ConflictResolution(file);
        resolution.Set(0, HunkChoice.Manual, "one\ntwo");

        Assert.AreEqual("one,two", string.Join(",", resolution.ResultOf(file.Hunks[0]).Select(l => l.Text)));
    }

    // Emptying the edit box is how a conflicted region is dropped altogether, which the result pane
    // has to say in words — an empty list is what it draws "(nothing — the whole region is
    // removed)" for. Splitting an empty text into one empty line instead previewed a blank line
    // that would never be written.
    [TestMethod]
    public void TestAHandEditEmptiedOutIsNothingAtAll()
    {
        var file = FileWith(1);
        var resolution = new ConflictResolution(file);
        resolution.Set(0, HunkChoice.Manual, "");

        Assert.AreEqual(0, resolution.ResultOf(file.Hunks[0]).Count);
    }

    // The result pane is a preview of what the Git layer will write, so the two have to split text
    // into lines by the same rule — and they cannot share the code: ConflictParser is below the
    // Server layer and every line of it carries a terminator this side of the model does not have.
    // This is what holds them together. It is the only place a Cui test reaches into gmd.Git, and
    // the disagreement it pins was real: "" and a trailing newline each previewed one line too many.
    [TestMethod]
    [DataRow("")]
    [DataRow("a")]
    [DataRow("a\n")]
    [DataRow("a\nb")]
    [DataRow("a\nb\n")]
    [DataRow("\n")]
    [DataRow("a\n\n")]
    [DataRow("\n\na")]
    [DataRow("a\r\nb\r\n")]
    public void TestPreviewedLinesAreTheLinesTheGitLayerWillWrite(string text)
    {
        var previewed = ConflictResolution.ToLines(text).Select(l => l.Text);
        var written = gmd.Git.Private.ConflictParser.ToLines(text.Replace("\r\n", "\n")).Select(l => l.Text);

        Assert.AreEqual(string.Join("|", written), string.Join("|", previewed), $"For {text.Replace("\n", "\\n")}");
    }

    // A combination is taken as text the user wrote, as an edit by hand is, which is what goes down
    // to be written, and is kept as its lines with where each came from until decided otherwise
    [TestMethod]
    public void TestACombinationIsTextKeptWithItsLines()
    {
        var file = FileWith(1);
        var resolution = new ConflictResolution(file);
        var lines = new[] { new CombinedLine("t", LineOrigin.Theirs), new CombinedLine("both", LineOrigin.Both) };

        resolution.SetCombined(0, lines);

        Assert.AreEqual(HunkChoice.Manual, resolution.ChoiceOf(0));
        Assert.AreEqual("t\nboth", resolution.ToResolutions()[0].ManualText);
        CollectionAssert.AreEqual(
            new[] { "t", "both" },
            resolution.ResultOf(file.Hunks[0]).Select(l => l.Text).ToArray()
        );
        Assert.AreSame(lines, resolution.CombinedOf(0));

        resolution.Set(0, HunkChoice.Ours);
        Assert.IsNull(resolution.CombinedOf(0), "Decided otherwise");
    }

    // Lines made text, both sides to start typing from or a combination taken as text, are read back
    // as the same lines, by the result pane and by the Git layer that writes them: joined alone, a
    // blank last line was drawn in the result and never written, and one blank line wrote nothing
    [TestMethod]
    [DataRow(new string[0])]
    [DataRow(new[] { "" })]
    [DataRow(new[] { "a" })]
    [DataRow(new[] { "a", "b" })]
    [DataRow(new[] { "a", "" })]
    [DataRow(new[] { "", "a" })]
    [DataRow(new[] { "a", "", "" })]
    [DataRow(new[] { "", "" })]
    public void TestLinesMadeTextAreReadBackAsTheSameLines(string[] lines)
    {
        var text = ConflictResolution.ToText(lines);

        var previewed = ConflictResolution.ToLines(text).Select(l => l.Text);
        var written = gmd.Git.Private.ConflictParser.ToLines(text).Select(l => l.Text);
        Assert.AreEqual(string.Join("|", lines), string.Join("|", previewed), "Previewed");
        Assert.AreEqual(string.Join("|", lines), string.Join("|", written), "Written");
    }

    // A combination that ends in a blank line keeps it, in the result and in the text written
    [TestMethod]
    public void TestACombinationEndingInABlankLineKeepsIt()
    {
        var file = FileWith(1);
        var resolution = new ConflictResolution(file);

        resolution.SetCombined(
            0,
            [new CombinedLine("y = a + c;", LineOrigin.Both), new CombinedLine("", LineOrigin.Ours)]
        );

        CollectionAssert.AreEqual(
            new[] { "y = a + c;", "" },
            resolution.ResultOf(file.Hunks[0]).Select(l => l.Text).ToArray()
        );
        var written = gmd.Git.Private.ConflictParser.ToLines(resolution.ToResolutions()[0].ManualText);
        CollectionAssert.AreEqual(new[] { "y = a + c;", "" }, written.Select(l => l.Text).ToArray());
    }

    // The decisions carry over to the file read again, as the resolver goes back to it, where it has
    // the same conflicts; the ancestor, read only where it can be, may have come or gone
    [TestMethod]
    public void TestTheDecisionsAreForTheSameFileReadAgain()
    {
        var resolution = new ConflictResolution(FileWith(2));
        resolution.Set(0, HunkChoice.Ours);
        resolution.Set(1, HunkChoice.Manual, "typed");
        var withBase = FileWith(2) with { HasBase = true };

        var again = resolution.For(withBase);

        Assert.IsNotNull(again);
        Assert.AreSame(withBase, again.File);
        Assert.AreEqual(HunkChoice.Ours, again.ChoiceOf(0));
        Assert.AreEqual("typed", again.ManualTextOf(1));
        Assert.IsTrue(again.IsFullyResolved);
    }

    // Changed on disk, the decisions would be applied by number to other conflicts, so they are not
    [TestMethod]
    public void TestTheDecisionsAreNotForAFileThatChanged()
    {
        var resolution = new ConflictResolution(FileWith(2));
        resolution.Set(0, HunkChoice.Ours);
        var file = FileWith(2);
        var hunk = file.Hunks[1] with { Theirs = Lines("changed") };
        var changed = file with { Segments = [.. file.Segments.Take(3), new ConflictSegment([], hunk)] };
        var textBetween = file with
        {
            Segments = [new ConflictSegment(Lines("edited"), null), .. file.Segments.Skip(1)],
        };

        Assert.IsNull(resolution.For(FileWith(1)), "A conflict fewer");
        Assert.IsNull(resolution.For(changed), "A side changed");
        Assert.IsNull(resolution.For(textBetween), "The text between them changed");
        Assert.IsNull(resolution.For(FileWith(2) with { Path = "other.txt" }), "Another file");
    }
}
