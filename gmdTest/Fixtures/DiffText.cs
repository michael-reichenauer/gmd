using gmd.Cui.Common;
using gmd.Cui.Diff;

namespace gmdTest.Fixtures;

// Renders the rows of a diff as plain text, i.e. what the diff view draws. Text.ToString()
// flattens the styled output, so the drawn diff can be asserted as a picture without a
// Terminal.Gui driver — the same trick GraphText uses for the branch graph.
//
// Use like e.g.:
//     Assert.AreEqual(
//         """
//            1┃one       │    1┃one
//         """,
//         DiffText.BodyOf(rows));
static class DiffText
{
    // What the view separates the two columns with. Not a rune the diff itself produces, so
    // ColorsOf keeps it literal rather than turning it into a color letter.
    const string Separator = " │ ";

    // The filler drawn where one side has no line at all. DiffService.NoLine is 300 runes wide
    // and would swamp every picture, so it is collapsed to a single rune. Recognised by identity,
    // which is how DiffView itself tests for it (DiffView.cs:636-687).
    static readonly Text NoLine = Text.Dark("░");

    // The whole drawn diff, one line per row
    public static string Of(DiffRows rows) => Join(Lines(rows.Rows, t => t.ToString()));

    // The color of every rune, aligned under the runes of Of(). The column padding and the
    // separator stay literal so the two pictures line up.
    public static string ColorsOf(DiffRows rows) => Join(Lines(rows.Rows, TextColors.Of));

    // Just the first file's section, i.e. the diff body between the two '─' rules AddSectionDiff
    // wraps it in. Lets a test about the body skip the dozen rows of commit and file summary that
    // every diff starts with.
    public static string BodyOf(DiffRows rows) => Join(Lines(BodyRowsOf(rows), t => t.ToString()));

    public static string BodyColorsOf(DiffRows rows) => Join(Lines(BodyRowsOf(rows), TextColors.Of));

    public static IReadOnlyList<DiffRow> BodyRowsOf(DiffRows rows)
    {
        bool IsRule(DiffRow r) => r.Mode == DiffRowMode.DividerLine && r.Left.ToString() == "─";

        var first = rows.Rows.FindIndexBy(IsRule);
        var last = rows.Rows.FindLastIndexBy(IsRule);
        if (first == -1 || last <= first)
            return [];

        return rows.Rows.Skip(first + 1).Take(last - first - 1).ToList();
    }

    static IReadOnlyList<string> Lines(IReadOnlyList<DiffRow> rows, Func<Text, string> render)
    {
        var drawn = rows.Select(r => (r.Mode, Left: render(Shown(r.Left)), Right: render(Shown(r.Right)))).ToList();

        // Only the side by side rows share the column, so only they decide where it ends
        var width = drawn
            .Where(d => d.Mode == DiffRowMode.SideBySide)
            .Select(d => d.Left.Length)
            .DefaultIfEmpty(0)
            .Max();

        return drawn
            .Select(d =>
                d.Mode == DiffRowMode.SideBySide
                    ? $"{d.Left.PadRight(width)}{Separator}{d.Right}".TrimEnd()
                    : d.Left.TrimEnd()
            )
            .ToList();
    }

    static Text Shown(Text text) => ReferenceEquals(text, DiffService.NoLine) ? NoLine : text;

    static string Join(IEnumerable<string> lines) => string.Join("\n", lines);
}
