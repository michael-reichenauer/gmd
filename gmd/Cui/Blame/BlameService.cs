using gmd.Cui.Common;

namespace gmd.Cui.Blame;

interface IBlameService
{
    BlameRows ToBlameRows(Server.Blame blame);
    Text ToRowText(BlameRow row, BlameColumns cw, int rowStartX, bool isCurrent, bool isSelected);
}

// Turns a blame into the rows the blame view draws. Kept out of the view so the run aggregation,
// the coloring and the column math are testable without a terminal driver.
class BlameService : IBlameService
{
    // The run bracket. The three light glyphs are one bracket the eye follows down, with feet that
    // say exactly where the run ends. A run of one row gets a heavy stub instead, so it stands out
    // rather than reading as a '┌' whose foot scrolled off the screen.
    const string RunFirst = "┌";
    const string RunMiddle = "│";
    const string RunLast = "└";
    const string RunSingle = "╺";

    const string LineNbrPrefix = "│";
    const string LineNbrSuffix = "┃"; // Same margin glyph as the diff view

    // Newest to oldest. Red is left out, it means an error or a deletion everywhere else in gmd.
    static readonly Color[] AgeColors =
    [
        Color.Yellow,
        Color.BrightGreen,
        Color.Green,
        Color.BrightCyan,
        Color.Cyan,
        Color.Blue,
        Color.Dark,
    ];

    public BlameRows ToBlameRows(Server.Blame blame)
    {
        var rows = new BlameRows();
        var colorById = AgeColorsOf(blame);
        var lines = blame.Lines;

        for (var i = 0; i < lines.Count; i++)
        {
            var id = lines[i].CommitId;

            // A run is consecutive lines of the same commit in the file as it stands now, which is
            // what the reader sees as one block. The porcelain group counts are deliberately not
            // used for this: a group is contiguous in the file as it was, not as it is.
            var isFirst = i == 0 || lines[i - 1].CommitId != id;
            var isLast = i == lines.Count - 1 || lines[i + 1].CommitId != id;
            var part =
                isFirst && isLast ? RunPart.Single
                : isFirst ? RunPart.First
                : isLast ? RunPart.Last
                : RunPart.Middle;

            // Tabs are expanded here rather than in the git layer, so a copy of the selected rows
            // still yields the file's own text. One rune per column, as the diff view does.
            var text = lines[i].Text.Replace("\t", "   ");

            rows.Add(new BlameRow(lines[i].LineNbr, blame.CommitById[id], part, colorById[id], text));
        }

        return rows;
    }

    // Shades each commit by how recent it is, by rank rather than by absolute age. Rank means the
    // whole ramp is used whether the file is a week or a decade old, and it keeps recent edits
    // apart in a file that is mostly one ancient bulk commit, which is the case worth reading.
    static IReadOnlyDictionary<string, Color> AgeColorsOf(Server.Blame blame)
    {
        var colorById = new Dictionary<string, Color>();

        var committed = blame
            .CommitById.Values.Where(c => !c.IsUncommitted)
            .OrderByDescending(c => c.AuthorTime)
            .ToList();

        for (var i = 0; i < committed.Count; i++)
        {
            // The newest commit is always the first color and the oldest always the last, so a
            // file with only a few commits still uses both ends of the ramp
            var index = committed.Count == 1 ? 0 : i * (AgeColors.Length - 1) / (committed.Count - 1);
            colorById[committed[i].Id] = AgeColors[index];
        }

        // Uncommitted lines are yellow, as they are in the log view
        foreach (var c in blame.CommitById.Values.Where(c => c.IsUncommitted))
        {
            colorById[c.Id] = Color.BrightYellow;
        }

        return colorById;
    }

    public Text ToRowText(BlameRow row, BlameColumns cw, int rowStartX, bool isCurrent, bool isSelected)
    {
        // The run bracket is drawn outside the highlight, the same way the log view highlights the
        // commit columns but not the branch graph beside them
        var text = Text.Color(row.Color, GlyphOf(row.Part)).Dark(" ");

        var rest = new TextBuilder();
        WriteCommit(rest, row, cw);
        rest.Dark(LineNbrPrefix).Dark(TxtRight($"{row.LineNbr}", cw.LineNbr)).Dark(LineNbrSuffix);
        rest.Add(ClipCode(CodeText(row), cw.Code, rowStartX));

        Text restText =
            isSelected ? rest.Select()
            : isCurrent ? rest.Highlight()
            : rest;
        return text.Add(restText);
    }

    // The commit is named only on the first row of a run, the rest of the run is blank under it
    static void WriteCommit(TextBuilder text, BlameRow row, BlameColumns cw)
    {
        var c = row.Commit;
        var isRunStart = row.Part is RunPart.First or RunPart.Single;

        if (cw.Sid > 0)
        {
            // '©' is the uncommitted marker used in the log view, a sid of all '0' would say less
            var sid =
                !isRunStart ? ""
                : c.IsUncommitted ? "©"
                : c.Sid;
            text.Color(row.Color, Txt(sid, cw.Sid)).Dark(" ");
        }

        if (cw.Author > 0)
        {
            // Git names the uncommitted author 'Not Committed Yet', which is a sentence rather
            // than a name and does not fit the column
            var author =
                !isRunStart ? ""
                : c.IsUncommitted ? "Uncommitted"
                : c.Author;
            text.Dark(Txt(author, cw.Author)).Dark(" ");
        }

        if (cw.Date > 0)
        {
            // The time of an uncommitted line is 'now', which says nothing, so it is left blank
            var date = isRunStart && !c.IsUncommitted ? c.AuthorTime.ToString("yy-MM-dd") : "";
            text.Dark(Txt(date, cw.Date)).Dark(" ");
        }
    }

    static Text CodeText(BlameRow row) =>
        Text.Color(row.Commit.IsUncommitted ? Color.BrightYellow : Color.White, row.Text);

    // Clips the code to the scrolled window, marking whichever ends were cut with a '…', and pads
    // the rest so the current row highlight reaches the right edge
    static Text ClipCode(Text code, int width, int startX)
    {
        if (width <= 0)
            return Text.Empty;

        var isCutLeft = startX > 0;
        var isCutRight = code.Length > startX + width;
        var length = Math.Max(0, width - (isCutLeft ? 1 : 0) - (isCutRight ? 1 : 0));

        var text = new TextBuilder();
        if (isCutLeft)
            text.Dark("…");
        text.Add(code.Subtext(isCutLeft ? startX + 1 : startX, length, !isCutRight));
        if (isCutRight)
            text.Dark("…");

        return text;
    }

    static string GlyphOf(RunPart part) =>
        part switch
        {
            RunPart.First => RunFirst,
            RunPart.Middle => RunMiddle,
            RunPart.Last => RunLast,
            _ => RunSingle,
        };

    static string Txt(string text, int width) =>
        text.Length <= width ? text + new string(' ', width - text.Length) : text[..width];

    static string TxtRight(string text, int width) =>
        text.Length <= width ? new string(' ', width - text.Length) + text : text[^width..];
}
