using gmd.Cui.Common;

namespace gmd.Cui.Diff;

// The code of a diff's rows in the colors of its language, worked out as the rows are drawn and kept
// for as long as the rows are. A line is colored as the lines before it make it, inside a comment of
// many lines say, so each side of a file is colored line after line: the old file's lines, unchanged
// and removed, as the left column shows them, and the new file's, unchanged and added, as the right.
// Where unchanged lines are left out between two hunks, the next hunk starts afresh, as at the top of
// a file, where C#'s grammar, for one, reads members and statements alike, so a hunk is colored well
// wherever in a file it starts, unless inside a comment or a string of many lines, until it ends.
//
// Only the text of a line is colored, what is drawn in the text's color: not its gutter, nor a side of
// a conflict, nor a line left out of the commit, which have colors that say what they are. A changed
// word keeps its background.
class DiffSyntax
{
    // How many lines are colored to reach a line, past which it is colored afresh from a few lines
    // above it instead, so that jumping to the end of a large file added whole does not stall
    const int MaxCatchUp = 2000;
    const int CatchUpFrom = 100;

    readonly ICodeGrammars grammars;
    readonly Dictionary<int, FileLines?> files = []; // By the row of the file's header

    public DiffSyntax(DiffRows rows, ICodeGrammars grammars)
    {
        Rows = rows;
        this.grammars = grammars;
    }

    // The rows colored, which a view makes anew as their layout or context changes
    public DiffRows Rows { get; }

    // A side of a row, its gutter and its line, with the line's code colored; as it is where it is no
    // source line, or its file's language has no grammar. The side of a row across the view, which a
    // file added or removed whole is drawn in, or every row in one column, is the left.
    public Text Colored(Text side, int index, bool isRight)
    {
        var row = Rows.Rows[index];
        var lineNbr =
            row.Mode != DiffRowMode.SideBySide ? row.LineNbr
            : isRight ? row.RightLineNbr
            : row.LeftLineNbr;
        if (lineNbr == 0 || side == DiffRowService.NoLine)
            return side;

        var header = Rows.FileHeaderOf(index);
        if (header < 0)
            return side;
        if (!files.TryGetValue(header, out var file))
            files[header] = file = FileLinesOf(header);
        if (file == null)
            return side;

        // A row across the view is a line of the new file where the new file has it, unchanged or
        // added, and of the old one where it was removed
        var isNew = row.Mode == DiffRowMode.SideBySide ? isRight : row.RightLineNbr != 0;
        var spans = (isNew ? file.New : file.Old).SpansOf(index);
        return side.WithCode(DiffRowService.LineNbrWidth(lineNbr), spans);
    }

    // The lines of each side of a file, in the order they are shown, from its header row to the next
    // file's, or to the next commit's in a file history
    FileLines? FileLinesOf(int header)
    {
        var rows = Rows.Rows;
        if (grammars.For(rows[header].FilePath) is not ICodeGrammar grammar)
            return null;

        var file = new FileLines(new SideLines(grammar), new SideLines(grammar));
        for (var i = header + 1; i < rows.Count && rows[i].FilePath == "" && rows[i].CommitId == ""; i++)
        {
            var row = rows[i];
            if (row.Mode == DiffRowMode.SideBySide)
            {
                if (row.LeftLineNbr != 0 && row.Left != DiffRowService.NoLine)
                    file.Old.Add(i, row.LeftLineNbr, CodeOf(row.Left, row.LeftLineNbr));
                if (row.RightLineNbr != 0 && row.Right != DiffRowService.NoLine)
                    file.New.Add(i, row.RightLineNbr, CodeOf(row.Right, row.RightLineNbr));
            }
            else if (row.Mode == DiffRowMode.SpanBoth && row.LineNbr != 0)
            { // An unchanged line in one column is a line of both files
                var code = CodeOf(row.Left, row.LineNbr);
                if (row.LeftLineNbr != 0)
                    file.Old.Add(i, row.LeftLineNbr, code);
                if (row.RightLineNbr != 0)
                    file.New.Add(i, row.RightLineNbr, code);
            }
        }
        return file;
    }

    static string CodeOf(Text side, int lineNbr) => DiffRowService.WithoutLineNbr(side.ToString(), lineNbr);

    record FileLines(SideLines Old, SideLines New);

    // The lines of one side of a file, each colored once it is first drawn, after the lines of its hunk
    // before it, from the state the line before ended in
    class SideLines
    {
        readonly ICodeGrammar grammar;
        readonly List<int> lineNbrs = [];
        readonly List<string> codes = [];
        readonly List<CodeLine?> colored = [];
        readonly Dictionary<int, int> byRow = [];

        public SideLines(ICodeGrammar grammar) => this.grammar = grammar;

        public void Add(int row, int lineNbr, string code)
        {
            byRow[row] = codes.Count;
            lineNbrs.Add(lineNbr);
            codes.Add(code);
            colored.Add(null);
        }

        public IReadOnlyList<CodeSpan> SpansOf(int row) =>
            byRow.TryGetValue(row, out var i) ? ColoredLine(i).Spans : [];

        CodeLine ColoredLine(int i)
        {
            if (colored[i] is CodeLine done)
                return done;

            // From the line after the last one of its hunk colored, or where the hunk starts
            var first = i;
            while (first > 0 && IsHunkGoingOn(first) && colored[first - 1] == null)
                first--;
            var state = first > 0 && IsHunkGoingOn(first) ? colored[first - 1]!.State : null;
            if (i - first > MaxCatchUp)
                (first, state) = (i - CatchUpFrom, null);

            for (var k = first; k <= i; k++)
            {
                var line = grammar.Tokenize(codes[k], state);
                colored[k] = line;
                state = line.State;
            }
            return colored[i]!;
        }

        // Whether a line follows the one before it in the file, rather than starting a hunk
        bool IsHunkGoingOn(int i) => lineNbrs[i] == lineNbrs[i - 1] + 1;
    }
}
