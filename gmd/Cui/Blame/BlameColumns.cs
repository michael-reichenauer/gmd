namespace gmd.Cui.Blame;

// The widths of the blame gutter columns, i.e. how much of each commit is named beside the code.
// Pure index math with no view, so it is unit testable, the same reason MenuDimensions exists.
record BlameColumns(int Sid, int Author, int Date, int LineNbr, int Code)
{
    // The run bracket and the space after it. Never dropped, it is the whole point of the view.
    public const int RunWidth = 2;
    public const int SidWidth = 6; // Same as the log view, see StringExtensions.Sid()
    public const int AuthorWidth = 11; // Fits the 'Uncommitted' label exactly
    public const int DateWidth = 8; // yy-MM-dd
    public const int MinLineNbrWidth = 4;

    // Below this the code column is too narrow to read, so a gutter column is dropped instead
    public const int MinCodeWidth = 30;

    // The '│' before the line number and the '┃' after it
    const int SeparatorsWidth = 2;

    public int GutterWidth => RunWidth + Pad(Sid) + Pad(Author) + Pad(Date) + SeparatorsWidth + LineNbr;

    public static BlameColumns Calculate(BlameDetails details, int width, int lineCount)
    {
        var lineNbrWidth = Math.Max(MinLineNbrWidth, $"{lineCount}".Length);

        // Step the detail down until the code column is wide enough to be worth reading. Done per
        // draw rather than by changing the user's choice, so widening the view brings it back.
        for (var d = details; ; d++)
        {
            var (sid, author, date) = WidthsOf(d);
            var cw = new BlameColumns(sid, author, date, lineNbrWidth, 0);
            var code = width - cw.GutterWidth;
            if (code >= MinCodeWidth || d == BlameDetails.Rail)
                return cw with { Code = Math.Max(0, code) };
        }
    }

    // A shown column is followed by one space, a dropped one takes no room at all
    static int Pad(int width) => width == 0 ? 0 : width + 1;

    static (int sid, int author, int date) WidthsOf(BlameDetails details) =>
        details switch
        {
            BlameDetails.Full => (SidWidth, AuthorWidth, DateWidth),
            BlameDetails.Compact => (SidWidth, 0, DateWidth),
            BlameDetails.Minimal => (SidWidth, 0, 0),
            _ => (0, 0, 0),
        };
}
