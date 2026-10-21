using gmd.Cui.Common;

namespace gmd.Cui.Diff;

class DiffRows
{
    readonly List<DiffRow> rows = [];
    string[]? files;

    internal int MaxLength { get; private set; }
    internal int Count => rows.Count;

    public IReadOnlyList<DiffRow> Rows => rows;

    internal void Add(
        Text oneRow,
        string filePath = "",
        string commitId = "",
        int leftLineNbr = 0,
        int rightLineNbr = 0,
        string? removedText = null,
        string? addedText = null
    ) =>
        Add(
            oneRow,
            Text.Empty,
            DiffRowMode.SpanBoth,
            filePath,
            commitId,
            leftLineNbr,
            rightLineNbr,
            removedText,
            addedText
        );

    internal void Add(
        Text left,
        Text right,
        int leftLineNbr = 0,
        int rightLineNbr = 0,
        string? removedText = null,
        string? addedText = null
    ) => Add(left, right, DiffRowMode.SideBySide, "", "", leftLineNbr, rightLineNbr, removedText, addedText);

    internal void AddLine(Text line) => Add(line, Text.Empty, DiffRowMode.DividerLine, "", "", 0, 0, null, null);

    // A file's header, which the view follows with a rule across the rest of the row
    internal void AddFileHeader(Text header, string filePath)
    {
        Add(header, filePath);
        rows[^1] = rows[^1] with { Fill = Text.BrightCyan("─") };
    }

    // Where unchanged lines are left out between two hunks, '⋯' and a line
    internal void AddHunkSeparator(Text separator)
    {
        Add(separator);
        rows[^1] = rows[^1] with { Fill = Text.Dark("─"), IsSeparator = true };
    }

    // The marker a conflict starts with, on both sides or across them, which a conflict is gone to by
    internal void AddConflictStart(Text marker, bool isOneColumn)
    {
        if (isOneColumn)
            Add(marker);
        else
            Add(marker, marker);
        rows[^1] = rows[^1] with { IsConflictStart = true };
    }

    // The row the next or previous change starts at, from a row, or -1 when there is none that way.
    // A change is a run of changed rows, or a conflict, from its first marker. From inside a change,
    // the previous one is its own start, as an editor's previous change is.
    internal int NextChange(int fromRow, int step) => Next(fromRow, step, IsChangeStart);

    // The header of the next or previous file, from a row, or -1 when there is none that way
    internal int NextFile(int fromRow, int step) => Next(fromRow, step, i => rows[i].FilePath != "");

    // Which change a row starts, and how many there are, e.g. (3, 9)
    internal (int Number, int Count) ChangeNumber(int index) => Number(index, IsChangeStart);

    // Which file a row is the header of, and how many there are
    internal (int Number, int Count) FileNumber(int index) => Number(index, i => rows[i].FilePath != "");

    // The header of the file a row is in, -1 above the first file, of its commit in a file history,
    // where the file above a commit's title is the commit before's
    internal int FileHeaderOf(int index)
    {
        for (var i = Math.Min(index, rows.Count - 1); i >= 0; i--)
        {
            if (rows[i].FilePath != "")
                return i;
            if (rows[i].CommitId != "")
                return -1;
        }
        return -1;
    }

    bool IsChangeStart(int i) => rows[i].IsConflictStart || (rows[i].IsChange && (i == 0 || !rows[i - 1].IsChange));

    int Next(int fromRow, int step, Func<int, bool> isWanted)
    {
        for (var i = fromRow + step; i >= 0 && i < rows.Count; i += step)
        {
            if (isWanted(i))
                return i;
        }
        return -1;
    }

    (int Number, int Count) Number(int index, Func<int, bool> isWanted)
    {
        var starts = Enumerable.Range(0, rows.Count).Where(isWanted).ToList();
        return (starts.IndexOf(index) + 1, starts.Count);
    }

    void Add(
        Text left,
        Text right,
        DiffRowMode mode,
        string filePath,
        string commitId,
        int leftLineNbr,
        int rightLineNbr,
        string? removedText,
        string? addedText
    )
    {
        // The filler of a side with no line is no text to scroll to: it is 300 wide, and counted, it
        // let the diff scroll that far into blank columns
        if (left != DiffRowService.NoLine && left.Length > MaxLength)
        {
            MaxLength = left.Length;
        }
        if (right != DiffRowService.NoLine && right.Length > MaxLength)
        {
            MaxLength = right.Length;
        }

        files = null;
        rows.Add(
            new DiffRow(left, right, mode, filePath, commitId, leftLineNbr, rightLineNbr)
            {
                RemovedText = removedText,
                AddedText = addedText,
            }
        );
    }

    // Where a row is, as content rather than as an index, which means nothing once the rows are
    // rebuilt with another context: the commit it is in, since a file history shows one diff per
    // commit, the file and the source line
    internal DiffAnchor AnchorAt(int index)
    {
        if (index < 0 || index >= rows.Count)
            return DiffAnchor.None;

        return new DiffAnchor(Above(index, r => r.CommitId), Above(index, r => r.FilePath), rows[index].LineNbr);
    }

    // The anchor of the file the commands on a file act on: the file a row is in, or on the rows of a
    // commit above its files, its header and summary, where a diff opens, its first file. Otherwise
    // + and - did nothing on the first key after opening the diff, even of a commit of one file.
    internal DiffAnchor FileAnchorAt(int index)
    {
        if (index < 0 || index >= rows.Count || FileHeaderOf(index) != -1)
            return AnchorAt(index);

        var first = rows.FindIndex(index, r => r.FilePath != "");
        return first == -1 ? AnchorAt(index) : AnchorAt(first);
    }

    // Whether a row is above the files of its commit, where FileAnchorAt is its first file's
    internal bool IsAboveFiles(int index) => index >= 0 && index < rows.Count && FileHeaderOf(index) == -1;

    // The row an anchor is at, or the nearest line of its file, or -1 when the file is not there.
    // The file is looked for from the anchor's commit on, since in a file history every commit
    // shows the same path, and taking the first of them put the cursor back on the newest commit
    // whenever + or - was pressed on an older one.
    internal int IndexOf(DiffAnchor anchor)
    {
        if (anchor.FilePath == "")
            return -1;

        var start = anchor.CommitId == "" ? 0 : Math.Max(0, rows.FindIndexBy(r => r.CommitId == anchor.CommitId));
        for (int i = start; i < rows.Count; i++)
        {
            if (rows[i].FilePath == anchor.FilePath)
                return DiffRowService.FindClosestLineIndex(rows, i, anchor.LineNbr);
        }

        return -1;
    }

    // The file a row is in, "" above the first, as AnchorAt finds it, worked out once for all rows,
    // since every row drawn asks
    internal string FileAt(int index)
    {
        files ??= Files();
        return index >= 0 && index < files.Length ? files[index] : "";
    }

    string[] Files()
    {
        var path = "";
        return rows.Select(r => path = r.FilePath != "" ? r.FilePath : path).ToArray();
    }

    // The value of the closest row at or above the index that has one, "" above them all
    string Above(int index, Func<DiffRow, string> value)
    {
        for (int i = index; i >= 0; i--)
        {
            if (value(rows[i]) != "")
                return value(rows[i]);
        }

        return "";
    }

    public override string ToString() => $"Rows: {Count}";
}

// A drawn row. LeftLineNbr/RightLineNbr are the source line numbers the row shows, or 0 for a row
// that is not a source line (a divider, a header, a conflict marker) or for the side that has no
// line (the '░' filler). They are what the gutter is drawn from, so they also say how wide it is,
// which is how a copy strips it and how the cursor is put back on the same line after a reload.
record DiffRow(
    Text Left,
    Text Right,
    DiffRowMode Mode,
    string FilePath,
    string CommitId,
    int LeftLineNbr,
    int RightLineNbr
)
{
    // The line number this row is at, preferring the new file's side, or 0 if it is not a source line
    internal int LineNbr => RightLineNbr != 0 ? RightLineNbr : LeftLineNbr;

    // The text of the removed line on the left and of the added line on the right, as the diff has
    // it, null where that side is no change: what a discard of the row's lines is told by (ChosenLine)
    public string? RemovedText { get; init; }
    public string? AddedText { get; init; }

    public bool IsChange => RemovedText != null || AddedText != null;

    // The rune a header row is followed by to the end of the row, e.g. a file's '─', or null
    public Text? Fill { get; init; }

    // Whether the row is where unchanged lines are left out between two hunks, which side by side
    // has its '⋯' on both sides
    public bool IsSeparator { get; init; }

    // Whether the row is the marker a conflict starts with
    public bool IsConflictStart { get; init; }
}

// Where the cursor is, as content rather than a row index, so it survives the rows being rebuilt
// with a different amount of context. LineNbr is 0 when the cursor is not on a source line, and
// CommitId "" in a diff that names no commit, e.g. of the uncommitted changes.
record DiffAnchor(string CommitId, string FilePath, int LineNbr)
{
    public static readonly DiffAnchor None = new DiffAnchor("", "", 0);
}

enum DiffRowMode
{
    SideBySide,
    SpanBoth,
    DividerLine,
}

// What a line of a diff is: unchanged, removed or added, or a line of one side of a conflict
enum LineKind
{
    Same,
    Removed,
    Added,
    Ours,
    Theirs,
}

record Line(int LineNbr, string Text, LineKind Kind);

class Block
{
    public List<Line> Lines { get; } = [];

    public void Add(int lineNbr, string text, LineKind kind)
    {
        Lines.Add(new Line(lineNbr, text, kind));
    }
}
