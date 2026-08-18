using gmd.Cui.Blame;
using gmd.Cui.Common;
using ServerBlame = gmd.Server.Blame;
using ServerBlameCommit = gmd.Server.BlameCommit;
using ServerBlameLine = gmd.Server.BlameLine;

namespace gmdTest.Cui.Blame;

[TestClass]
public class BlameServiceTest
{
    const string Id1 = "1111111111111111111111111111111111111111";
    const string Id2 = "2222222222222222222222222222222222222222";
    const string Id3 = "3333333333333333333333333333333333333333";
    const string Id0 = "0000000000000000000000000000000000000000";

    static readonly DateTime Time = new DateTime(2025, 1, 5, 10, 0, 0);

    // Builds a blame from '<sha> <text>' rows, in file order, so a test reads as the file it blames
    static ServerBlame BlameOf(params (string Id, string Text)[] lines)
    {
        var commits = lines
            .Select(l => l.Id)
            .Distinct()
            .Select(
                (id, i) =>
                    new ServerBlameCommit(
                        id,
                        id.Sid(),
                        id == Id0 ? "uncommitted" : $"Author{i + 1}",
                        "",
                        // Each distinct commit a year older than the one before it
                        Time.AddYears(-i),
                        $"Subject {i + 1}",
                        id == Id0,
                        false,
                        "",
                        "",
                        "file.txt"
                    )
            )
            .ToDictionary(c => c.Id, c => c);

        var blameLines = lines.Select((l, i) => new ServerBlameLine(l.Id, i + 1, i + 1, l.Text)).ToList();
        return new ServerBlame("file.txt", "HEAD", blameLines, commits);
    }

    // The rows drawn as plain text, which is what makes the gutter assertable as a picture.
    // Text.ToString() flattens the styled output, the same trick GraphText uses for the graph.
    static string Draw(ServerBlame blame, BlameDetails details = BlameDetails.Full, int width = 70)
    {
        var service = new BlameService();
        var rows = service.ToBlameRows(blame);
        var cw = BlameColumns.Calculate(details, width, rows.Rows.Count);
        return string.Join("\n", rows.Rows.Select(r => service.ToRowText(r, cw, 0, false, false).ToString().TrimEnd()));
    }

    static IReadOnlyList<BlameRow> RowsOf(ServerBlame blame) => new BlameService().ToBlameRows(blame).Rows;

    [TestMethod]
    public void TestSingleLineRunIsSingle()
    {
        var rows = RowsOf(BlameOf((Id1, "one"), (Id2, "two")));

        Assert.AreEqual(RunPart.Single, rows[0].Part);
        Assert.AreEqual(RunPart.Single, rows[1].Part);
    }

    [TestMethod]
    public void TestTwoLineRunIsFirstThenLast()
    {
        var rows = RowsOf(BlameOf((Id1, "one"), (Id1, "two")));

        Assert.AreEqual(RunPart.First, rows[0].Part);
        Assert.AreEqual(RunPart.Last, rows[1].Part);
    }

    [TestMethod]
    public void TestLongerRunHasMiddleRows()
    {
        var rows = RowsOf(BlameOf((Id1, "one"), (Id1, "two"), (Id1, "three"), (Id1, "four")));

        CollectionAssert.AreEqual(
            new[] { RunPart.First, RunPart.Middle, RunPart.Middle, RunPart.Last },
            rows.Select(r => r.Part).ToArray()
        );
    }

    // A commit that owns two separate blocks of the file is two runs, not one
    [TestMethod]
    public void TestSameCommitInTwoPlacesIsTwoRuns()
    {
        var rows = RowsOf(BlameOf((Id1, "one"), (Id2, "two"), (Id1, "three")));

        CollectionAssert.AreEqual(
            new[] { RunPart.Single, RunPart.Single, RunPart.Single },
            rows.Select(r => r.Part).ToArray()
        );
        Assert.AreEqual(Id1, rows[2].Commit.Id);
    }

    // The whole point of the view: the sid, author and date are named once per run, not per line
    [TestMethod]
    public void TestGutterNamesTheCommitOnlyOnTheFirstRowOfARun()
    {
        var blame = BlameOf((Id1, "one"), (Id1, "two"), (Id1, "three"), (Id2, "four"), (Id2, "five"), (Id3, "six"));

        Assert.AreEqual(
            """
            ┌ 111111 Author1     25-01-05 │   1┃one
            │                             │   2┃two
            └                             │   3┃three
            ┌ 222222 Author2     24-01-05 │   4┃four
            └                             │   5┃five
            ╺ 333333 Author3     23-01-05 │   6┃six
            """,
            Draw(blame)
        );
    }

    [TestMethod]
    public void TestUncommittedRunShowsTheUncommittedMarkerAndNoDate()
    {
        var blame = BlameOf((Id1, "one"), (Id0, "two"));

        Assert.AreEqual(
            """
            ╺ 111111 Author1     25-01-05 │   1┃one
            ╺ ©      Uncommitted          │   2┃two
            """,
            Draw(blame)
        );
    }

    [TestMethod]
    public void TestCompactDetailDropsTheAuthor()
    {
        var blame = BlameOf((Id1, "one"), (Id1, "two"));

        Assert.AreEqual(
            """
            ┌ 111111 25-01-05 │   1┃one
            └                 │   2┃two
            """,
            Draw(blame, BlameDetails.Compact)
        );
    }

    [TestMethod]
    public void TestMinimalDetailIsTheSidOnly()
    {
        Assert.AreEqual(
            """
            ╺ 111111 │   1┃one
            """,
            Draw(BlameOf((Id1, "one")), BlameDetails.Minimal)
        );
    }

    [TestMethod]
    public void TestRailDetailIsTheBracketOnly()
    {
        Assert.AreEqual(
            """
            ╺ │   1┃one
            """,
            Draw(BlameOf((Id1, "one")), BlameDetails.Rail)
        );
    }

    // The newest commit takes the first color of the ramp and the oldest the last, so even a file
    // with only a couple of commits uses both ends
    [TestMethod]
    public void TestAgeHeatSpansTheRampFromNewestToOldest()
    {
        var rows = RowsOf(BlameOf((Id1, "one"), (Id2, "two"), (Id3, "three")));

        Assert.AreEqual(Color.Yellow, rows[0].Color);
        Assert.AreEqual(Color.BrightCyan, rows[1].Color);
        Assert.AreEqual(Color.Dark, rows[2].Color);
    }

    [TestMethod]
    public void TestSingleCommitIsTheNewestColor()
    {
        var rows = RowsOf(BlameOf((Id1, "one")));

        Assert.AreEqual(Color.Yellow, rows[0].Color);
    }

    [TestMethod]
    public void TestUncommittedIsBrightYellowRegardlessOfAge()
    {
        var rows = RowsOf(BlameOf((Id1, "one"), (Id0, "two")));

        Assert.AreEqual(Color.BrightYellow, rows[1].Color);
    }

    // Expanded for drawing, but the line itself keeps the file's own text so a copy is faithful
    [TestMethod]
    public void TestTabsAreExpandedInRowsButNotInTheBlame()
    {
        var blame = BlameOf((Id1, "\tindented"));
        var rows = RowsOf(blame);

        Assert.AreEqual("   indented", rows[0].Text);
        Assert.AreEqual("\tindented", blame.Lines[0].Text);
    }

    // The fallback details, for a commit the shown log does not have. Only the log is capped, not
    // the blame, so this is what a repo past the log cap falls back to.
    [TestMethod]
    public void TestDetailsRowsOfACommitNotInTheLog()
    {
        var commit = BlameOf((Id1, "one")).CommitById[Id1] with { AuthorMail = "alice@example.com" };

        var rows = new BlameService().ToDetailsRows(commit);

        Assert.AreEqual($"Id:         {Id1}", rows[0].ToString());
        // The time is local with its zone offset, as the commit details view shows it, so only the
        // part that does not depend on the machine's time zone is asserted
        StringAssert.StartsWith(rows[1].ToString(), "Author:     Author1 <alice@example.com>, time: 2025-01-05 ");
        Assert.AreEqual("Subject:    Subject 1", rows[2].ToString());
        StringAssert.Contains(rows[4].ToString(), "not in the shown log");
    }

    [TestMethod]
    public void TestDetailsRowsOfAnUncommittedCommit()
    {
        var commit = BlameOf((Id0, "one")).CommitById[Id0];

        var rows = new BlameService().ToDetailsRows(commit);

        Assert.AreEqual("Id:         Uncommitted", rows[0].ToString());
    }

    [TestMethod]
    public void TestMaxLengthIsTheWidestCodeLine()
    {
        var rows = new BlameService().ToBlameRows(BlameOf((Id1, "one"), (Id1, "a longer line"), (Id1, "two")));

        Assert.AreEqual("a longer line".Length, rows.MaxLength);
    }

    [TestMethod]
    public void TestCodeTooWideIsTruncatedWithAnEllipsis()
    {
        var blame = BlameOf((Id1, new string('x', 50)));

        // 36 of gutter leaves 34 for the code, so the last column becomes the cut marker
        Assert.AreEqual(
            "╺ 111111 Author1     25-01-05 │   1┃" + new string('x', 33) + "…",
            Draw(blame, BlameDetails.Full, 70)
        );
    }

    [TestMethod]
    public void TestScrolledCodeIsMarkedAtBothEnds()
    {
        var service = new BlameService();
        var rows = service.ToBlameRows(BlameOf((Id1, "0123456789abcdefghij")));
        var cw = BlameColumns.Calculate(BlameDetails.Rail, 18, 1);

        // 8 of gutter leaves 10 for the code, scrolled 5 in, so both ends are cut
        Assert.AreEqual("╺ │   1┃…6789abcd…", service.ToRowText(rows.Rows[0], cw, 5, false, false).ToString());
    }
}
