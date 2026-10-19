using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

// The code of the resolver's rows in the colors of its language, worked out as the rows are drawn.
// Each side of a conflicted file is a version of the whole file, the text between the conflicts and
// its lines of each, colored line after line from the top, as the blame colors its file (CodeLines),
// a line being found in it by its number, which its gutter shows. The text between conflicts is
// colored as the left side has it, as it is numbered. The result below is the file as it will be
// saved, colored the same way. It is made anew with each decision and each key typed, and its lines
// keep their colors down to the first that is not as it was.
//
// Only what is drawn in the text's color is colored, as in the diff (DiffSyntax): not a line a side
// kept or removed, dark so that what the side did stands out, nor a word it changed, on its color, nor
// the common ancestor, nor a side's lines in its color where no ancestor is known.
class ConflictSyntax
{
    readonly ICodeGrammar grammar;
    readonly CodeLines ours;
    readonly CodeLines theirs;
    ConflictResult? result;
    CodeLines? resultLines;

    public ConflictSyntax(ConflictFile file, ICodeGrammar grammar)
    {
        File = file;
        this.grammar = grammar;
        ours = new CodeLines(VersionOf(file, h => h.Ours), grammar);
        theirs = new CodeLines(VersionOf(file, h => h.Theirs), grammar);
    }

    // The file colored, which the view makes anew for each file and each read of one
    public ConflictFile File { get; }

    // The row with the code of its lines colored
    public ConflictRow Colored(ConflictRow row)
    {
        if (row.Lines is not IReadOnlyList<PaneLine> lines)
            return row;

        var panes = row.Panes.Select((pane, i) => i < lines.Count ? Colored(pane, lines[i], row.GutterWidth) : pane);
        return row with { Panes = panes.ToList() };
    }

    // A row of the result with its code colored
    public Text ColoredResult(ConflictResult result, int index)
    {
        if (!ReferenceEquals(result, this.result))
        {
            resultLines = new CodeLines(result.Code, grammar, resultLines);
            this.result = result;
        }

        var row = result.Rows[index];
        return row.WithCode(ConflictRowService.GutterWidth, resultLines!.SpansOf(index));
    }

    Text Colored(Text pane, PaneLine line, int gutterWidth) =>
        line.Version switch
        {
            CodeVersion.Ours => pane.WithCode(gutterWidth, ours.SpansOf(line.LineNbr - 1)),
            CodeVersion.Theirs => pane.WithCode(gutterWidth, theirs.SpansOf(line.LineNbr - 1)),
            _ => pane,
        };

    // A side's version of the whole file, its lines as drawn
    static IReadOnlyList<string?> VersionOf(ConflictFile file, Func<ConflictHunk, IReadOnlyList<FileLine>> side) =>
        file
            .Segments.SelectMany(s => s.Hunk is ConflictHunk hunk ? side(hunk) : s.Lines)
            .Select(l => (string?)ConflictRowService.Shown(l))
            .ToList();
}
