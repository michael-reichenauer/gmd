using gmd.Cui.Common;

namespace gmd.Cui.Blame;

// The code of a blame's lines in the colors of its language, worked out as the rows are drawn and
// kept for as long as the rows are. A blame is the whole file in order, so a line is colored after
// the lines above it, from the state the line before ended in, inside a comment of many lines, say,
// which is simpler than the diff's hunks (DiffSyntax). A line far below the last one colored, after
// a jump to the end of a large file, is colored from a few lines above it instead, as the diff does,
// so that the jump does not stall.
class BlameSyntax
{
    const int MaxCatchUp = 2000;
    const int CatchUpFrom = 100;

    readonly ICodeGrammar grammar;
    readonly CodeLine?[] colored;

    public BlameSyntax(BlameRows rows, ICodeGrammar grammar)
    {
        Rows = rows;
        this.grammar = grammar;
        colored = new CodeLine?[rows.Rows.Count];
    }

    // The rows colored, which the view makes anew for each version blamed
    public BlameRows Rows { get; }

    // The spans of the code of the row, colored once it is first drawn, with the rows above it that
    // were not yet
    public IReadOnlyList<CodeSpan> SpansOf(int index)
    {
        if (index < 0 || index >= colored.Length)
            return [];
        if (colored[index] is CodeLine done)
            return done.Spans;

        var first = index;
        while (first > 0 && colored[first - 1] == null)
            first--;
        var state = first > 0 ? colored[first - 1]!.State : null;
        if (index - first > MaxCatchUp)
            (first, state) = (index - CatchUpFrom, null);

        for (var i = first; i <= index; i++)
        {
            var line = grammar.Tokenize(Rows.Rows[i].Text, state);
            colored[i] = line;
            state = line.State;
        }
        return colored[index]!.Spans;
    }
}
