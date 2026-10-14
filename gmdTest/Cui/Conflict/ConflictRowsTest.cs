using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// Next/previous conflict is index math over the drawn rows, so it is testable with no terminal —
// the same reason ContentScroll and Hoover exist.
[TestClass]
public class ConflictRowsTest
{
    static FileLine[] Lines(params string[] texts) => texts.Select(t => new FileLine(t)).ToArray();

    // 'text, conflict, text, conflict, …, text', i.e. with a row above the first conflict and one
    // below the last, which is where the cursor spends most of its time
    static ConflictRows RowsOfFileWith(int hunkCount)
    {
        var segments = new List<ConflictSegment>();
        for (int i = 0; i < hunkCount; i++)
        {
            segments.Add(new ConflictSegment(Lines($"before {i}"), null));
            segments.Add(
                new ConflictSegment([], new ConflictHunk(i, "HEAD", "", "topic", Lines($"o{i}"), [], Lines($"t{i}")))
            );
        }
        segments.Add(new ConflictSegment(Lines("after"), null));

        var file = new ConflictFile("f.txt", ConflictKind.BothModified, false, segments, true, true);
        return new ConflictRowService().ToRows(file, new ConflictResolution(file), false);
    }

    // The one conflict of a file is reachable from either side of it. Counting by conflict number
    // instead had nowhere to go in either direction, since there is neither a second conflict to
    // step to nor one before the first, which left ']' and '[' looking like dead keys.
    [TestMethod]
    public void TestTheOnlyConflictIsReachedFromAboveAndFromBelow()
    {
        var rows = RowsOfFileWith(1);
        var hunkRow = rows.IndexOfHunk(0);

        Assert.AreEqual(hunkRow, rows.NextHunkRow(0, 1), "']' from the top of the file goes to it");
        Assert.AreEqual(hunkRow, rows.NextHunkRow(rows.Count - 1, -1), "'[' from below it goes back to it");
        Assert.AreEqual(-1, rows.NextHunkRow(hunkRow, 1), "There is nothing after it");
        Assert.AreEqual(-1, rows.NextHunkRow(hunkRow, -1), "or before it");
    }

    // From above the first conflict, next is that first conflict — not the second one, which is
    // where counting from the nearest conflict at or before the cursor sent it
    [TestMethod]
    public void TestNextFromTheTopGoesToTheFirstConflict()
    {
        var rows = RowsOfFileWith(3);

        Assert.AreEqual(rows.IndexOfHunk(0), rows.NextHunkRow(0, 1));
    }

    [TestMethod]
    public void TestWalkingForwardsAndBackwardsThroughEveryConflict()
    {
        var rows = RowsOfFileWith(3);

        var forwards = new List<int>();
        for (int at = 0; (at = rows.NextHunkRow(at, 1)) != -1; )
            forwards.Add(at);

        var backwards = new List<int>();
        for (int at = rows.Count - 1; (at = rows.NextHunkRow(at, -1)) != -1; )
            backwards.Add(at);

        CollectionAssert.AreEqual(new[] { rows.IndexOfHunk(0), rows.IndexOfHunk(1), rows.IndexOfHunk(2) }, forwards);
        CollectionAssert.AreEqual(new[] { rows.IndexOfHunk(2), rows.IndexOfHunk(1), rows.IndexOfHunk(0) }, backwards);
    }

    // Previous from inside a conflict is that conflict's own header, so the first '[' from within
    // a long conflict shows its start rather than skipping to the one before it
    [TestMethod]
    public void TestPreviousFromInsideAConflictGoesToItsOwnHeader()
    {
        var rows = RowsOfFileWith(3);
        var hunkRow = rows.IndexOfHunk(1);

        Assert.AreEqual(hunkRow, rows.NextHunkRow(hunkRow + 2, -1));
    }

    // Showing the common ancestor makes a conflict as tall as its longest side, so the rows after it
    // move down: the cursor is kept on the line it was on, where 'b' left it at its row index, which
    // could be in another conflict
    [TestMethod]
    public void TestTheCursorKeepsItsLineWhenTheAncestorIsShown()
    {
        var segments = new List<ConflictSegment>();
        for (int i = 0; i < 2; i++)
        {
            segments.Add(new ConflictSegment(Lines($"before {i}"), null));
            var hunk = new ConflictHunk(
                i,
                "HEAD",
                "base",
                "topic",
                Lines($"o{i}"),
                Lines("b1", "b2", "b3"),
                Lines($"t{i}")
            );
            segments.Add(new ConflictSegment([], hunk));
        }
        segments.Add(new ConflictSegment(Lines("after"), null));
        var file = new ConflictFile("f.txt", ConflictKind.BothModified, false, segments, true, true);
        var service = new ConflictRowService();
        var without = service.ToRows(file, new ConflictResolution(file), false);
        var with = service.ToRows(file, new ConflictResolution(file), true);

        foreach (var text in new[] { "before 0", "before 1", "after", "o1 │ t1" })
        {
            var at = without.Rows.FindIndexBy(r => r.ToString() == text);
            var line = with.Rows[with.IndexOf(without.AnchorAt(at))].ToString();
            Assert.AreEqual(text, line.Replace(" │ b1", ""), $"The cursor on '{text}'");
        }

        // A row of a conflict that is not there once the ancestor is hidden again is its last row
        var lastOfFirst = with.IndexOfHunk(1) - 2;
        Assert.AreEqual("b3", with.Rows[lastOfFirst].ToString().Split(" │ ")[1]);
        var back = without.IndexOf(with.AnchorAt(lastOfFirst));
        Assert.AreEqual("o0 │ t0", without.Rows[back].ToString());
    }

    // A file with no conflicts left to walk, e.g. one that only differs in a way git resolved
    [TestMethod]
    public void TestAFileWithNoConflictsHasNowhereToGo()
    {
        var rows = RowsOfFileWith(0);

        Assert.AreEqual(-1, rows.NextHunkRow(0, 1));
        Assert.AreEqual(-1, rows.NextHunkRow(0, -1));
    }
}
