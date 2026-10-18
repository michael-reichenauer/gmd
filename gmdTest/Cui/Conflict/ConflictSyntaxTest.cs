using gmd.Cui.Common;
using gmd.Cui.Conflict;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Conflict;

// The code of the resolver's rows in its language's colors, each side as its version of the whole
// file has it, and the result as the file will be saved
[TestClass]
public class ConflictSyntaxTest
{
    static FileLine[] Lines(params string[] texts) => texts.Select(t => new FileLine(t)).ToArray();

    static ConflictFile FileOf(params ConflictSegment[] segments) =>
        new ConflictFile("a.cs", ConflictKind.BothModified, false, segments, true, true, true);

    static ConflictSegment Stable(params string[] lines) => new(Lines(lines), null);

    static ConflictSegment Hunk(string[] ours, string[] ancestor, string[] theirs) =>
        new([], new ConflictHunk(0, "HEAD", "base", "topic", Lines(ours), Lines(ancestor), Lines(theirs)));

    // Ours opens a comment that the text after the conflict closes, which theirs does not: the
    // comment is ours alone, and the text after it is colored as ours has it, as it is numbered
    static ConflictFile FileWithACommentOnOneSide() =>
        FileOf(
            Stable("int x;"),
            Hunk(["int k;", "/* ours"], ["int k;", "int b;"], ["int k;", "int t;"]),
            Stable("int y; */")
        );

    static string Draw(IEnumerable<Text> rows, Func<Text, string> of) => string.Join("\n", rows.Select(of));

    // Each side is colored as its version of the file has it: ours' comment runs on into the text after
    // the conflict, and theirs has none. Only what is in the text's color is: not the lines a side
    // kept, nor the one it removed, dark so that what it did stands out, nor a word it changed, on its
    // color, though the words of the line it kept are.
    [TestMethod]
    public void TestEachSideIsColoredAsItsVersionOfTheFile()
    {
        var file = FileWithACommentOnOneSide();
        var service = new ConflictRowService();
        var syntax = new ConflictSyntax(file, new FakeCodeGrammar());
        var columns = ConflictColumns.Calculate(60, false);
        var rows = service
            .ToRows(file, new ConflictResolution(file), false)
            .Rows.Select(r => service.ToRowText(syntax.Colored(r), columns, 0))
            .ToList();

        Assert.AreEqual(
            """
                1 int x;
            ─── Conflict 1 ── unresolved ───────────────────────────────
            HEAD                         │topic
                2 int k;                 │    2 int k;
            -     int b;                 │~   3 int t;
            +   3 /* ours                │
                4 int y; */
            """,
            Draw(rows, r => r.ToString().TrimEnd())
        );
        Assert.AreEqual(
            """
                D KKK WW
            mmm mmmmmmmm m mm RRRRRRRRRR mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm
            YYYY                         DCCCCC
                D DDD DD                 D    D DDD DD
            R     DDD DD                 DC   D KKK .W
            Y   D // ////                D
                D /// // //
            """,
            Draw(rows, r => TextColors.Of(r).TrimEnd())
        );
    }

    // The result is colored as the file will be saved: with the conflict not decided, its row is no
    // line of the file, and the text after it is code; with ours taken, it is in ours' comment. Made
    // anew with the decision, it keeps the colors of the lines before the conflict.
    [TestMethod]
    public void TestTheResultIsColoredAsTheFileWillBeSaved()
    {
        var file = FileWithACommentOnOneSide();
        var service = new ConflictRowService();
        var grammar = new FakeCodeGrammar();
        var syntax = new ConflictSyntax(file, grammar);
        var names = ConflictNames.Of(file, ConflictSides.None);
        var resolution = new ConflictResolution(file);
        IReadOnlyList<Text> ResultOf(ConflictResult result) =>
            Enumerable.Range(0, result.Rows.Count).Select(i => syntax.ColoredResult(result, i)).ToList();

        var undecided = ResultOf(service.ToResult(file, resolution, names));
        Assert.AreEqual(
            """
                D KKK WW
            R      RRRRRRRR R RR RRR RRRRRRRR D YYYYD D CCCCCD D DD D DDDDD D DDDDDDDD D DDDD
                D KKK WW WW
            """,
            Draw(undecided, TextColors.Of)
        );

        resolution.Set(0, HunkChoice.Ours);
        grammar.Lines.Clear();
        var decided = ResultOf(service.ToResult(file, resolution, names));
        Assert.AreEqual(
            """
                1 int x;
            ▌   2 int k;
            ▌   3 /* ours
                4 int y; */
            """,
            Draw(decided, r => r.ToString())
        );
        Assert.AreEqual(
            """
                D KKK WW
            Y   D KKK WW
            Y   D // ////
                D /// // //
            """,
            Draw(decided, TextColors.Of)
        );
        CollectionAssert.AreEqual(new[] { "int k;", "/* ours", "int y; */" }, grammar.Lines, "int x; is kept");
    }
}
