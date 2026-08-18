using gmd.Cui.Common;

namespace gmd.Cui.Blame;

// Where a row sits within a run of consecutive lines from the same commit, which is what the
// gutter bracket draws. A run of one row is Single, since a lone '┌' with no foot would read as a
// run whose end scrolled off the screen.
enum RunPart
{
    Single,
    First,
    Middle,
    Last,
}

// How much of each commit the gutter names. Cycled by the user, and stepped down automatically
// when the view is too narrow to leave room for the code.
enum BlameDetails
{
    Full, // sid, author and date
    Compact, // sid and date
    Minimal, // sid
    Rail, // just the run bracket
}

// One blamed line, ready to draw. Commit is the one that last changed the line, looked up once
// here so the row builder and the view do not both have to.
record BlameRow(int LineNbr, Server.BlameCommit Commit, RunPart Part, Color Color, string Text);

class BlameRows
{
    readonly List<BlameRow> rows = [];

    public IReadOnlyList<BlameRow> Rows => rows;

    // The widest code line, which is what bounds the horizontal scroll of the code column
    public int MaxLength { get; private set; }

    public void Add(BlameRow row)
    {
        rows.Add(row);
        MaxLength = Math.Max(MaxLength, row.Text.Length);
    }
}
