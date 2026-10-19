using gmd.Cui.Common;

namespace gmd.Cui.Blame;

// The code of a blame's lines in the colors of its language, worked out as the rows are drawn and
// kept for as long as the rows are. A blame is the whole file in order, so a line is colored after
// the lines above it, from the state the line before ended in, inside a comment of many lines, say,
// which is simpler than the diff's hunks (DiffSyntax), see CodeLines.
class BlameSyntax
{
    readonly CodeLines lines;

    public BlameSyntax(BlameRows rows, ICodeGrammar grammar)
    {
        Rows = rows;
        lines = new CodeLines(rows.Rows.Select(r => (string?)r.Text).ToList(), grammar);
    }

    // The rows colored, which the view makes anew for each version blamed
    public BlameRows Rows { get; }

    // The spans of the code of the row, colored once it is first drawn, with the rows above it that
    // were not yet
    public IReadOnlyList<CodeSpan> SpansOf(int index) => lines.SpansOf(index);
}
