using gmd.Cui.Conflict;
using gmd.Server;

namespace gmdTest.Cui.Conflict;

// The row writer and the column math are pure, so the whole layout is asserted as text with no
// driver and no terminal — the same way BlameServiceTest snapshots the blame gutter.
[TestClass]
public class ConflictRowServiceTest
{
    static FileLine[] Lines(params string[] texts) => texts.Select(t => new FileLine(t)).ToArray();

    // One conflict with 'a' above it and 'b' below, as the resolver receives it from the Server
    static ConflictFile FileWithOneConflict(bool hasBase = false) =>
        new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [
                new ConflictSegment(Lines("a"), null),
                new ConflictSegment(
                    [],
                    new ConflictHunk(
                        0,
                        "HEAD",
                        hasBase ? "9c2f1a" : "",
                        "topic",
                        Lines("ours"),
                        hasBase ? Lines("base") : [],
                        Lines("theirs")
                    )
                ),
                new ConflictSegment(Lines("b"), null),
            ],
            true,
            true
        );

    static string Draw(ConflictFile file, ConflictResolution resolution, int width, bool isShowBase = false)
    {
        var service = new ConflictRowService();
        var columns = ConflictColumns.Calculate(width, isShowBase);

        return string.Join(
            '\n',
            service
                .ToRows(file, resolution, isShowBase)
                .Rows.Select(r => service.ToRowText(r, columns, 0).ToString().TrimEnd())
        );
    }

    [TestMethod]
    public void TestTwoPanesWithTheLabelsGitWrote()
    {
        var file = FileWithOneConflict();

        Assert.AreEqual(
            """
            a
            ─── Conflict 1 ── unr
            HEAD      │topic
            ours      │theirs
            b
            """,
            Draw(file, new ConflictResolution(file), 22)
        );
    }

    // The base pane is only drawn when there is one and there is room, and it goes in the middle,
    // which is where a three way merge reads: ours, what both changed, theirs
    [TestMethod]
    public void TestThreePanesWhenTheBaseIsShown()
    {
        var file = FileWithOneConflict(hasBase: true);

        Assert.AreEqual(
            """
            a
            ─── Conflict 1 ── unresolved ───────
            HEAD                              │9c2f1a                            │topic
            ours                              │base                              │theirs
            b
            """,
            Draw(file, new ConflictResolution(file), 104, isShowBase: true)
        );
    }

    // A conflict whose ancestor is empty — both sides added lines where there were none — still
    // gets the middle slot, or its 'theirs' would be drawn under the ancestor column: the widths
    // are worked out once for the whole view, not per conflict
    [TestMethod]
    public void TestAConflictWithNoBaseStillFillsTheMiddlePane()
    {
        var file = new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [
                new ConflictSegment(
                    [],
                    new ConflictHunk(0, "HEAD", "9c2f1a", "topic", Lines("ours"), Lines("base"), Lines("theirs"))
                ),
                new ConflictSegment([], new ConflictHunk(1, "HEAD", "", "topic", Lines("o2"), [], Lines("t2"))),
            ],
            true,
            true
        );

        Assert.AreEqual(
            """
            ─── Conflict 1 ── unresolved ───────
            HEAD                              │9c2f1a                            │topic
            ours                              │base                              │theirs
            ─── Conflict 2 ── unresolved ───────
            HEAD                              │common ancestor (nothing here)    │topic
            o2                                │                                  │t2
            """,
            Draw(file, new ConflictResolution(file), 104, isShowBase: true)
        );
    }

    // The rows are built once from what the user asked for, but the widths are worked out per draw
    // and drop the ancestor on a narrow view. When they disagree the *middle* pane is the one that
    // goes: taking the first two panes would drop 'theirs', losing a whole side of the conflict.
    [TestMethod]
    public void TestSteppingDownDropsTheAncestorAndNotTheirs()
    {
        var file = FileWithOneConflict(hasBase: true);
        var service = new ConflictRowService();
        var rows = service.ToRows(file, new ConflictResolution(file), isShowBase: true);

        // Rows made for three panes, drawn into the two a narrow view allows
        var narrow = ConflictColumns.Calculate(60, isShowBase: true);
        Assert.AreEqual(2, narrow.PaneCount);

        var drawn = string.Join('\n', rows.Rows.Select(r => service.ToRowText(r, narrow, 0).ToString().TrimEnd()));

        Assert.AreEqual(
            """
            a
            ─── Conflict 1 ── unresolved ───────
            HEAD                         │topic
            ours                         │theirs
            b
            """,
            drawn
        );
    }

    // A decided conflict says so in its header, naming the side rather than saying 'ours'
    [TestMethod]
    public void TestHeaderNamesTheChosenSide()
    {
        var file = FileWithOneConflict();
        var resolution = new ConflictResolution(file);
        resolution.Set(0, HunkChoice.Theirs);

        StringAssert.Contains(Draw(file, resolution, 40), "─── Conflict 1 ── using topic");
    }

    // Taking the ancestor says so in those words rather than naming it: a recovered ancestor is
    // labelled 'base' and a diff3 one whatever git wrote, and neither says what the choice means
    [TestMethod]
    public void TestHeaderOfAConflictTakenFromTheAncestor()
    {
        var file = FileWithOneConflict(hasBase: true);
        var resolution = new ConflictResolution(file);
        resolution.Set(0, HunkChoice.Base);

        // Wider than the tests above: the header is cut to the view width, and these words are long
        StringAssert.Contains(Draw(file, resolution, 60), "─── Conflict 1 ── using the common ancestor");
    }

    [TestMethod]
    public void TestUnevenSidesArePaddedNotMisaligned()
    {
        var file = new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [new ConflictSegment([], new ConflictHunk(0, "HEAD", "", "topic", Lines("o1", "o2"), [], Lines("t1")))],
            true,
            true
        );

        Assert.AreEqual(
            """
            ─── Conflict 1 ── unr
            HEAD      │topic
            o1        │t1
            o2        │
            """,
            Draw(file, new ConflictResolution(file), 22)
        );
    }

    [TestMethod]
    public void TestHeaderCountsTheConflictsLeft()
    {
        var file = FileWithOneConflict();
        var resolution = new ConflictResolution(file);
        var service = new ConflictRowService();

        Assert.AreEqual(
            "Merge  f.txt   conflict 1 of 1   1 still to resolve",
            service.ToHeader(file, resolution, 0, GitOperation.Merge).ToString()
        );

        resolution.Set(0, HunkChoice.Ours);
        Assert.AreEqual(
            "Merge  f.txt   conflict 1 of 1   all resolved",
            service.ToHeader(file, resolution, 0, GitOperation.Merge).ToString()
        );
    }

    // During a rebase the header says so, since that is when 'ours' and 'theirs' are back to front
    [TestMethod]
    public void TestHeaderNamesTheOperation()
    {
        var file = FileWithOneConflict();

        StringAssert.StartsWith(
            new ConflictRowService().ToHeader(file, new ConflictResolution(file), 0, GitOperation.Rebase).ToString(),
            "Rebase  f.txt"
        );
    }

    // ---- columns ----

    [TestMethod]
    public void TestTwoPanesSplitTheWidth()
    {
        var columns = ConflictColumns.Calculate(121, isShowBase: false);

        Assert.AreEqual(2, columns.PaneCount);
        Assert.AreEqual(60, columns.Pane);
        Assert.AreEqual(121, columns.TotalWidth);
    }

    [TestMethod]
    public void TestThreePanesSplitTheWidth()
    {
        var columns = ConflictColumns.Calculate(122, isShowBase: true);

        Assert.AreEqual(3, columns.PaneCount);
        Assert.AreEqual(40, columns.Pane);
        Assert.AreEqual(122, columns.TotalWidth);
    }

    // Below about ninety columns three panes are too narrow to read code in, so the base is
    // dropped for that draw — widening the terminal brings it back, as BlameColumns does
    [TestMethod]
    [DataRow(91, 2)]
    [DataRow(80, 2)]
    [DataRow(92, 3)]
    [DataRow(120, 3)]
    public void TestBaseIsDroppedOnANarrowView(int width, int expectedPanes)
    {
        Assert.AreEqual(expectedPanes, ConflictColumns.Calculate(width, isShowBase: true).PaneCount);
    }

    [TestMethod]
    public void TestPanesStartAfterTheSeparators()
    {
        var columns = ConflictColumns.Calculate(122, isShowBase: true);

        Assert.AreEqual(0, columns.StartOf(0));
        Assert.AreEqual(41, columns.StartOf(1));
        Assert.AreEqual(82, columns.StartOf(2));
    }
}
