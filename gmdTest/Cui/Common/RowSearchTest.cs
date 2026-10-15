using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

[TestClass]
public class RowSearchTest
{
    static readonly string[] Lines = ["using System;", "var count = 0;", "count++;", "Console.WriteLine(Count);", "}"];

    static RowSearch Find(string text) => RowSearch.Find(text, Lines.Length, i => Lines[i]);

    // A part of a line, as typed, whatever its case: a line of code is looked for by a piece of it
    [TestMethod]
    public void TestATextIsFoundAsTypedInAnyCase()
    {
        var search = Find("COUNT");

        Assert.AreEqual(3, search.Count);
        Assert.IsTrue(search.IsActive);
        Assert.AreEqual("Match 2 of 3 for 'COUNT'", search.MatchText(2));
        Assert.AreEqual(1, Find("count++").Count, "Not word by word, as the log matches a commit");
    }

    // A search that found nothing leaves nothing for n to go on with, and nothing searched for none
    [TestMethod]
    public void TestASearchThatFindsNothingIsNotActive()
    {
        Assert.IsFalse(Find("absent").IsActive);
        Assert.AreEqual("absent", Find("absent").Text, "The text, for saying it was not found");
        Assert.AreSame(RowSearch.None, Find(""));
    }

    // A new search goes to the first match at the cursor or below, else to the last above it
    [TestMethod]
    public void TestANewSearchGoesFromTheCursorDown()
    {
        var search = Find("count");

        Assert.AreEqual(1, search.First(0));
        Assert.AreEqual(2, search.First(2), "The cursor's own row");
        Assert.AreEqual(3, search.First(4), "None below, so the last one above");
        Assert.AreEqual(-1, Find("absent").First(0));
    }

    // n and Shift-N go on from the cursor, wherever it was moved since, and stop at the last match
    // each way rather than going round, as the log's do
    [TestMethod]
    public void TestNextGoesOnFromTheCursorAndStopsAtTheEnds()
    {
        var search = Find("count");

        Assert.AreEqual(2, search.Next(1, 1));
        Assert.AreEqual(3, search.Next(2, 1));
        Assert.AreEqual(-1, search.Next(3, 1));
        Assert.AreEqual(2, search.Next(3, -1));
        Assert.AreEqual(-1, search.Next(1, -1));
        Assert.AreEqual(1, search.Next(0, 1), "From a row with no match");
        Assert.AreEqual("No more matches for 'count' below: ⇧n goes back up", search.NoMoreText(1));
        Assert.AreEqual("No more matches for 'count' above: n goes down", search.NoMoreText(-1));
    }

    // Each place in a line, as char indexes, from a start on and none overlapping
    [TestMethod]
    public void TestTheMatchesInALine()
    {
        CollectionAssert.AreEqual(new[] { (0, 2), (4, 6) }, RowSearch.MatchRanges("ab, aB", "AB").ToArray());
        CollectionAssert.AreEqual(new[] { (0, 2), (2, 4) }, RowSearch.MatchRanges("aaaa", "aa").ToArray());
        CollectionAssert.AreEqual(new[] { (5, 7) }, RowSearch.MatchRanges("  12┃12", "12", 5).ToArray());
        Assert.AreEqual(0, RowSearch.MatchRanges("text", "").Count);
        Assert.AreEqual(0, RowSearch.MatchRanges("text", "t", 9).Count, "From past the end");
    }

    // A match in the window as it is is shown with no scroll. Further right, the line is scrolled just
    // far enough for the match to end before the '…' of the cut on the right; and a match wider than
    // the window shows its start, after the '…' of the cut on the left.
    [TestMethod]
    public void TestALineIsScrolledSidewaysToShowAMatch()
    {
        // A window of 10 shows 9 columns and the '…' unscrolled: 0..8
        Assert.AreEqual(0, RowSearch.ScrollToShow(5, 9, 10));
        // Scrolled to 1, it shows '…', then columns 2..9, then '…'
        Assert.AreEqual(1, RowSearch.ScrollToShow(5, 10, 10));
        Assert.AreEqual(21, RowSearch.ScrollToShow(25, 30, 10));
        Assert.AreEqual(24, RowSearch.ScrollToShow(25, 50, 10), "Wider than the window");
    }
}
