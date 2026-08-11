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
        resolution.Set(2, HunkChoice.Neither);

        Assert.AreEqual(1, resolution.UnresolvedCount);
        Assert.IsTrue(resolution.IsChanged);
        Assert.AreEqual(HunkChoice.Ours, resolution.ChoiceOf(0));
        Assert.AreEqual(HunkChoice.None, resolution.ChoiceOf(1));
        Assert.AreEqual(HunkChoice.Neither, resolution.ChoiceOf(2));
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
    [DataRow(HunkChoice.Neither, "")]
    [DataRow(HunkChoice.None, "")]
    public void TestResultOfEachChoice(HunkChoice choice, string expected)
    {
        var file = FileWith(1);
        var resolution = new ConflictResolution(file);
        resolution.Set(0, choice);

        Assert.AreEqual(expected, string.Join(",", resolution.ResultOf(file.Hunks[0]).Select(l => l.Text)));
    }

    [TestMethod]
    public void TestResultOfAHandEdit()
    {
        var file = FileWith(1);
        var resolution = new ConflictResolution(file);
        resolution.Set(0, HunkChoice.Manual, "one\ntwo");

        Assert.AreEqual("one,two", string.Join(",", resolution.ResultOf(file.Hunks[0]).Select(l => l.Text)));
    }

    [TestMethod]
    public void TestNextAndPreviousStopAtTheEnds()
    {
        var resolution = new ConflictResolution(FileWith(3));

        Assert.AreEqual(1, resolution.NextHunk(0, 1));
        Assert.AreEqual(-1, resolution.NextHunk(2, 1), "There is nothing after the last one");
        Assert.AreEqual(1, resolution.NextHunk(2, -1));
        Assert.AreEqual(-1, resolution.NextHunk(0, -1), "There is nothing before the first one");
    }
}
