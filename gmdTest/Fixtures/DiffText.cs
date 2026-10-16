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
//         ▌   1 one │ ▌   1 uno
//         """,
//         DiffText.BodyOf(rows));
static class DiffText
{
    // What the view separates the two columns with. Not a rune the diff itself produces, so
    // ColorsOf keeps it literal rather than turning it into a color letter.
    const string Separator = " │ ";

    // The whole drawn diff, one line per row
    public static string Of(DiffRows rows) => Join(Lines(rows.Rows, t => t.ToString()));

    // The color of every rune, aligned under the runes of Of(). The column padding and the
    // separator stay literal so the two pictures line up.
    public static string ColorsOf(DiffRows rows) => Join(Lines(rows.Rows, TextColors.Of));

    // Just the first file's lines, below its header to the next file, without the lines between
    // hunks and blank rows. Lets a test about the lines skip the commit and the file list every diff
    // starts with.
    public static string BodyOf(DiffRows rows) => Join(Lines(BodyRowsOf(rows), t => t.ToString()));

    public static string BodyColorsOf(DiffRows rows) => Join(Lines(BodyRowsOf(rows), TextColors.Of));

    public static IReadOnlyList<DiffRow> BodyRowsOf(DiffRows rows)
    {
        var header = rows.Rows.FindIndexBy(r => r.FilePath != "");
        if (header == -1)
            return [];

        return rows
            .Rows.Skip(header + 1)
            .TakeWhile(r => r.FilePath == "")
            .Where(r => r.Fill == null && !IsBlank(r)) // A row with a line after it separates hunks
            .ToList();
    }

    static bool IsBlank(DiffRow r) => r.Mode == DiffRowMode.SpanBoth && r.Left.ToString() == "";

    static IReadOnlyList<string> Lines(IReadOnlyList<DiffRow> rows, Func<Text, string> render)
    {
        var drawn = rows.Select(r =>
                (r.Mode, Left: render(Shown(r.Left)) + Fill(r, render), Right: render(Shown(r.Right)))
            )
            .ToList();

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

    // The side with no line is drawn blank, and 300 wide it would swamp every picture. Recognised by
    // identity, which is how the view itself tests for it.
    static Text Shown(Text text) => ReferenceEquals(text, DiffRowService.NoLine) ? Text.Empty : text;

    // The rule a header row is followed by to the end of the row, drawn as one rune
    static string Fill(DiffRow row, Func<Text, string> render) => row.Fill == null ? "" : " " + render(row.Fill);

    static string Join(IEnumerable<string> lines) => string.Join("\n", lines);
}
