namespace gmd.Cui.Common;

// Lines of a file in the colors of their language, each colored as it is first drawn, after the lines
// above it that were not yet, from the state the line before ended in, since what a line is depends
// on the lines before, e.g. inside a comment of many lines. A line far below the last one colored,
// after a jump to the end of a large file, is colored from a few lines above it instead, so that the
// jump does not stall. A null line is a row that is no line of the file, which the state goes on past,
// e.g. the row of a conflict not decided yet in the resolver's result. The blame's file is colored so
// (BlameSyntax), and the resolver's versions of a conflicted file (ConflictSyntax).
class CodeLines
{
    const int MaxCatchUp = 2000;
    const int CatchUpFrom = 100;

    readonly ICodeGrammar grammar;
    readonly IReadOnlyList<string?> lines;
    readonly CodeLine?[] colored;

    public CodeLines(IReadOnlyList<string?> lines, ICodeGrammar grammar)
        : this(lines, grammar, null) { }

    // The lines of another version of the file, which keeps the colors of the lines before as far as
    // the two are the same, a line being colored by the lines above it alone: the resolver's result,
    // made anew for each decision and each key typed, and mostly the same file
    public CodeLines(IReadOnlyList<string?> lines, ICodeGrammar grammar, CodeLines? before)
    {
        this.lines = lines;
        this.grammar = grammar;
        colored = new CodeLine?[lines.Count];
        if (before == null || before.grammar != grammar)
            return;

        for (var i = 0; i < lines.Count && i < before.lines.Count && before.colored[i] != null; i++)
        {
            if (lines[i] != before.lines[i])
                break;
            colored[i] = before.colored[i];
        }
    }

    public int Count => lines.Count;

    // The spans of the code of the line, colored once it is first drawn, with the lines above it that
    // were not yet
    public IReadOnlyList<CodeSpan> SpansOf(int index)
    {
        if (index < 0 || index >= colored.Length || lines[index] == null)
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
            var line = lines[i] is string code ? grammar.Tokenize(code, state) : new CodeLine([], state);
            colored[i] = line;
            state = line.State;
        }
        return colored[index]!.Spans;
    }
}
