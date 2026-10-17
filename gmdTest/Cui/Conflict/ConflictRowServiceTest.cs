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

    // A conflict's header is in the color of the result pane's title and of the line under the
    // resolver's header, but for what was decided, and its line goes on to the end of the row, which
    // the panes stop short of where the width does not split evenly
    [TestMethod]
    public void TestAConflictsHeaderIsLinedToTheEndOfTheRow()
    {
        var file = FileWithOneConflict();
        var service = new ConflictRowService();
        var resolution = new ConflictResolution(file);
        var header = service.ToRows(file, resolution, false).Rows.First(r => r.IsHunkHeader);
        var columns = ConflictColumns.Calculate(40, false);
        Assert.AreEqual(39, columns.TotalWidth, "The panes are a column short of the width");

        var text = service.ToRowText(header, columns, 0);

        Assert.AreEqual("─── Conflict 1 ── unresolved ───────────", text.ToString());
        Assert.AreEqual("mmm mmmmmmmm m mm RRRRRRRRRR mmmmmmmmmmm", gmdTest.Fixtures.TextColors.Of(text));
        resolution.Set(0, HunkChoice.Ours);
        var decided = service.ToRows(file, resolution, false).Rows.First(r => r.IsHunkHeader);
        Assert.AreEqual(
            "mmm mmmmmmmm m mm GGGGG GGGG mmmmmmmmmmm",
            gmdTest.Fixtures.TextColors.Of(service.ToRowText(decided, columns, 0))
        );
    }

    [TestMethod]
    public void TestTwoPanesWithTheLabelsGitWrote()
    {
        var file = FileWithOneConflict();

        Assert.AreEqual(
            """
                1 a
            ─── Conflict 1 ── unresolved ───
            HEAD           │topic
                2 ours     │    2 theirs
                3 b
            """,
            Draw(file, new ConflictResolution(file), 32)
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
                1 a
            ─── Conflict 1 ── unresolved ───────────────────────────────────────────────────────────────────────────
            HEAD                              │9c2f1a                            │topic
            -     base                        │      base                        │-     base
            +   2 ours                        │                                  │+   2 theirs
                3 b
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
            ─── Conflict 1 ── unresolved ───────────────────────────────────────────────────────────────────────────
            HEAD                              │9c2f1a                            │topic
            -     base                        │      base                        │-     base
            +   1 ours                        │                                  │+   1 theirs
            ─── Conflict 2 ── unresolved ───────────────────────────────────────────────────────────────────────────
            HEAD                              │common ancestor (nothing here)    │topic
                2 o2                          │                                  │    2 t2
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
                1 a
            ─── Conflict 1 ── unresolved ───────────────────────────────
            HEAD                         │topic
            -     base                   │-     base
            +   2 ours                   │+   2 theirs
                3 b
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
            ─── Conflict 1 ── unresolved ───
            HEAD           │topic
                1 o1       │    1 t1
                2 o2       │
            """,
            Draw(file, new ConflictResolution(file), 32)
        );
    }

    // A tab is drawn as three spaces, as in the diff and blame: drawn as it is, the terminal moved it
    // to its next tab stop and pushed the pane after it out of line
    [TestMethod]
    public void TestTabsAreExpandedSoThePanesLineUp()
    {
        var file = new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [
                new ConflictSegment(Lines("\tabove"), null),
                new ConflictSegment([], new ConflictHunk(0, "HEAD", "", "topic", Lines("\to1"), [], Lines("t1\tx"))),
            ],
            true,
            true
        );

        Assert.AreEqual(
            """
                1    above
            ─── Conflict 1 ── unresolved ───
            HEAD           │topic
                2    o1    │    2 t1   x
            """,
            Draw(file, new ConflictResolution(file), 32)
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

    // With the common ancestor known, each side is drawn against it: '~' a line it changed, its words
    // on its own color, '+' a line it added, '-' dark a line of the ancestor's it removed, and a line
    // it kept dark, each numbered as the side has it, the text between conflicts as ours has it
    [TestMethod]
    public void TestEachSideIsDrawnAgainstTheAncestor()
    {
        var file = new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [
                new ConflictSegment(Lines("above"), null),
                new ConflictSegment(
                    [],
                    new ConflictHunk(
                        0,
                        "HEAD",
                        "base",
                        "topic",
                        Lines("var x = 1;"),
                        Lines("var x = 0;", "gone"),
                        Lines("new", "var x = 0;", "gone")
                    )
                ),
                new ConflictSegment(Lines("below"), null),
            ],
            true,
            true,
            HasBase: true
        );

        Assert.AreEqual(
            """
                1 above
            ─── Conflict 1 ── unresolved ───────────────────────────────
            HEAD                         │topic
            ~   2 var x = 1;             │+   2 new
            -     gone                   │    3 var x = 0;
                                         │    4 gone
                3 below
            """,
            Draw(file, new ConflictResolution(file), 60)
        );
    }

    // Scrolled sideways, the gutter of mark and number stays where it is, and the text moves
    [TestMethod]
    public void TestTheGutterStaysWhileTheTextScrolls()
    {
        var file = FileWithOneConflict();
        var service = new ConflictRowService();
        var rows = service.ToRows(file, new ConflictResolution(file), false);
        var columns = ConflictColumns.Calculate(40, false);

        Assert.AreEqual(
            "    2 urs          │    2 heirs",
            service.ToRowText(rows.Rows[3], columns, 1).ToString().TrimEnd()
        );
    }

    // The result is the file as it will be saved: the text between conflicts, a decided conflict's
    // lines with a bar in the color of the side they came from, numbered as the file will be, and an
    // undecided one a row saying which keys decide it, where its lines start
    [TestMethod]
    public void TestTheResultIsTheFileAsItWillBeSaved()
    {
        var file = new ConflictFile(
            "f.txt",
            ConflictKind.BothModified,
            false,
            [
                new ConflictSegment(Lines("a"), null),
                new ConflictSegment([], new ConflictHunk(0, "HEAD", "", "topic", Lines("o1"), [], Lines("t1", "t2"))),
                new ConflictSegment(Lines("b"), null),
                new ConflictSegment([], new ConflictHunk(1, "HEAD", "", "topic", Lines("o3"), [], Lines("t3"))),
            ],
            true,
            true
        );
        var resolution = new ConflictResolution(file);
        resolution.Set(0, HunkChoice.TheirsThenOurs);
        var names = ConflictNames.Of(file, new ConflictSides("main", "", ""));

        var result = new ConflictRowService().ToResult(file, resolution, names);

        Assert.AreEqual(
            """
                1 a
            ▌   2 t1
            ▌   3 t2
            ▌   4 o1
                5 b
            ▌      Conflict 2 is not decided: 1 main, 2 topic, 3 or 4 both, 0 neither, e edit
            """,
            string.Join("\n", result.Rows.Select(r => r.ToString()))
        );
        Assert.AreEqual("C\nC\nY", string.Join("\n", result.Rows.Skip(1).Take(3).Select(r => TextColorsOfBar(r))));
        CollectionAssert.AreEqual(new[] { 1, 5 }, new[] { result.Starts[0], result.Starts[1] });
        CollectionAssert.AreEqual(new[] { 4, 6 }, new[] { result.Ends[0], result.Ends[1] });

        // Where the cursor is above, in the result: a line of the text between conflicts is the same
        // line, and a conflict its lines, or its one row while undecided
        Assert.AreEqual((0, 1), result.RangeOf(new ConflictAnchor(-1, 0, false)), "'a', above the first");
        Assert.AreEqual((1, 4), result.RangeOf(new ConflictAnchor(0, 2, true)), "In conflict 1, its 3 lines");
        Assert.AreEqual((4, 5), result.RangeOf(new ConflictAnchor(0, 0, false)), "'b', the line after it");
        Assert.AreEqual((5, 6), result.RangeOf(new ConflictAnchor(1, 0, true)), "Conflict 2, undecided");
        Assert.AreEqual((0, 0), ConflictResult.None.RangeOf(new ConflictAnchor(-1, 0, false)), "Nothing");
    }

    // The color letter of a row's bar, its first rune
    static string TextColorsOfBar(gmd.Cui.Common.Text row) => gmdTest.Fixtures.TextColors.Of(row)[..1];

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
