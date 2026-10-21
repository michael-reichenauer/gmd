using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Diff;

interface IDiffRowService
{
    DiffRows ToDiffRows(CommitDiff diff);
    DiffRows ToDiffRows(CommitDiff[] commitDiffs);

    // fileContext names the files shown with something other than the default context, so their
    // headers can say so; the diff itself carries no record of how much context it was made with.
    // conflictPaths are the files git reports as unmerged. Needed because the diff can only see a
    // conflict git wrote markers for: a modify/delete or a binary conflict has none, and would
    // otherwise be headed as added or modified as if there were nothing to resolve.
    // isIgnoreWhitespace is a diff read with whitespace left out, whose rows say so, and mark no change
    // of the spaces around a line's text, nor of its line end, either. isOneColumn is the layout of a
    // narrow view: a change's removed lines above its added ones, as git writes them, rather than
    // beside them. hiddenFiles are the files whose lines are hidden, drawn as their header alone.
    // isListBeside is a diff with the list of its files beside it, or of its commits in a file
    // history, which the list at its top then leaves out, keeping the row that counts them.
    DiffRows ToDiffRows(
        CommitDiff[] commitDiffs,
        IReadOnlyDictionary<string, int> fileContext,
        IReadOnlyList<string> conflictPaths,
        bool isIgnoreWhitespace = false,
        bool isOneColumn = false,
        IReadOnlySet<string>? hiddenFiles = null,
        bool isListBeside = false
    );
    IReadOnlyList<string> GetDiffFilePaths(CommitDiff diff);
    IReadOnlyList<string> GetDiffBinaryFilePaths(CommitDiff diff);
}

// Turns diffs into the rows the diff view draws. A source line is drawn after a gutter of a bar and
// its line number: the bar, in the red or the green of a change, is what says a line changed, and
// within a changed line only the words that differ are marked on a background, so the text reads as
// text rather than as a block of color.
class DiffRowService : IDiffRowService
{
    // The bar at the edge of a changed line's column. In one column a removed line has a '-' there
    // and an added one a '+', as git writes them: the sides no longer tell them apart, and the red and
    // the green alone are one color to a red-green color blind eye, and to a screen of no colors.
    const string ChangeBar = "▌";
    const string RemovedMark = "-";
    const string AddedMark = "+";

    // The side of a row with no line, e.g. beside a line added, drawn blank. 300 wide, so it fills a
    // column of any width, and known by identity, which is how DiffRows and the view tell it apart.
    public static readonly Text NoLine = Text.Black(new string(' ', 300));

    // The text of rows copied: the side in focus of the side by side rows, the one column of the
    // others, with no line number gutter and no filler of a side that has no line. A whole file
    // added or removed is drawn in one column, with its line number on its own side, which the copy
    // took from the left and so kept the gutter of an added file.
    public static string CopyText(IEnumerable<DiffRow> rows, bool isFocusLeft) =>
        string.Join(
            "\n",
            rows.Where(r => r.Mode != DiffRowMode.DividerLine)
                .Select(r =>
                    r.Mode != DiffRowMode.SideBySide ? (Text: r.Left, LineNbr: r.LineNbr)
                    : isFocusLeft ? (Text: r.Left, LineNbr: r.LeftLineNbr)
                    : (Text: r.Right, LineNbr: r.RightLineNbr)
                )
                .Where(r => r.Text != NoLine)
                .Select(r => WithoutLineNbr(r.Text.ToString(), r.LineNbr))
        );

    // Strips the line number gutter a row is drawn with, so a copy yields the file's own text
    public static string WithoutLineNbr(string rowText, int lineNbr)
    {
        var width = LineNbrWidth(lineNbr);
        return rowText.Length > width ? rowText[width..] : "";
    }

    // How wide the gutter is at the start of a row's text, which a copy and a search leave out and the
    // view keeps in place while the line scrolls sideways: the bar, the number right-aligned in 4,
    // wider if it does not fit, which a large file reaches, and a space; 0 for a row that is not a
    // source line
    public static int LineNbrWidth(int lineNbr) => lineNbr == 0 ? 0 : Math.Max(4, lineNbr.ToString().Length) + 2;

    // The text of each side of a row that a search looks in: its line without the line number, and
    // nothing of a divider or of the filler of a side with no line, which are no text of the diff. A
    // header row, e.g. a file's, spans both sides and is the left one.
    public static (string Left, string Right) SearchText(DiffRow row) =>
        row.Mode switch
        {
            DiffRowMode.DividerLine => ("", ""),
            DiffRowMode.SpanBoth => (WithoutLineNbr(row.Left.ToString(), row.LineNbr), ""),
            _ => (SideText(row.Left, row.LeftLineNbr), SideText(row.Right, row.RightLineNbr)),
        };

    static string SideText(Text side, int lineNbr) => side == NoLine ? "" : WithoutLineNbr(side.ToString(), lineNbr);

    public DiffRows ToDiffRows(CommitDiff commitDiff)
    {
        return ToDiffRows([commitDiff]);
    }

    public DiffRows ToDiffRows(CommitDiff[] commitDiffs) => ToDiffRows(commitDiffs, new Dictionary<string, int>());

    public DiffRows ToDiffRows(CommitDiff[] commitDiffs, IReadOnlyDictionary<string, int> fileContext) =>
        ToDiffRows(commitDiffs, fileContext, []);

    public DiffRows ToDiffRows(
        CommitDiff[] commitDiffs,
        IReadOnlyDictionary<string, int> fileContext,
        IReadOnlyList<string> conflictPaths,
        bool isIgnoreWhitespace = false,
        bool isOneColumn = false,
        IReadOnlySet<string>? hiddenFiles = null,
        bool isListBeside = false
    )
    {
        commitDiffs = commitDiffs.Select(d => AsConflicted(d, conflictPaths)).ToArray();
        var layout = new Layout(
            isIgnoreWhitespace,
            isOneColumn,
            hiddenFiles ?? new HashSet<string>(),
            conflictPaths.ToHashSet(),
            isListBeside
        );
        DiffRows rows = new DiffRows();

        // A file history is one file over many commits, headed with the list of them, which the view's
        // top row counts, unless they are listed beside it
        var isHistory = commitDiffs.Length > 1;
        if (isHistory && !layout.IsListBeside)
        {
            commitDiffs.ForEach(diff =>
                rows.Add(
                    Text.Cyan($"  {diff.Id.Sid()}")
                        .White($"  {diff.Message.Max(60, true)}")
                        .Dark($"  {diff.Time.Iso()}  {diff.Author}")
                )
            );
        }

        commitDiffs.ForEach(diff => AddCommitDiff(diff, rows, fileContext, layout, isHistory));
        return rows;
    }

    // The row of the file starting at fileIndex whose source line is closest to lineNbr. Used to
    // put the cursor back on the line it was on after the file was redrawn with more or less
    // context, where the exact line may no longer be shown at all. Falls back to the file header.
    public static int FindClosestLineIndex(IReadOnlyList<DiffRow> rows, int fileIndex, int lineNbr)
    {
        if (lineNbr == 0)
            return fileIndex;

        var index = fileIndex;
        var closest = int.MaxValue;

        // Walk the file's own rows, i.e. up to the header of the next file
        for (int i = fileIndex + 1; i < rows.Count && rows[i].FilePath == ""; i++)
        {
            if (rows[i].LineNbr == 0)
                continue;

            var distance = Math.Abs(rows[i].LineNbr - lineNbr);
            if (distance < closest)
            {
                closest = distance;
                index = i;
            }
        }

        return index;
    }

    // Replaces one file of a diff with the same file fetched at a different context, leaving every
    // other file as it was. A pathspec would have been cheaper than re-fetching the whole diff and
    // keeping one file of it, but it suppresses git's rename detection, so a renamed file would
    // come back as added with its history lost.
    public static CommitDiff[] ReplaceFileDiff(CommitDiff[] diffs, string path, CommitDiff[] fetched)
    {
        // Full file history is one file over many commits, so there is nothing to pick out
        if (diffs.Length > 1 || fetched.Length != 1)
            return fetched;

        var index = diffs[0].FileDiffs.FindIndexBy(fd => fd.PathAfter == path);
        var fetchedFile = fetched[0].FileDiffs.FirstOrDefault(fd => fd.PathAfter == path);
        if (index == -1 || fetchedFile == null)
            return diffs;

        var fileDiffs = diffs[0].FileDiffs.ToList();
        fileDiffs[index] = fetchedFile;

        return [diffs[0] with { FileDiffs = fileDiffs }];
    }

    public IReadOnlyList<string> GetDiffFilePaths(CommitDiff diff)
    {
        return diff.FileDiffs.Select(fd => fd.PathAfter).ToList();
    }

    public IReadOnlyList<string> GetDiffBinaryFilePaths(CommitDiff diff)
    {
        return diff.FileDiffs.Where(fd => fd.IsBinary).Select(fd => fd.PathAfter).ToList();
    }

    // Marks the files git reports as unmerged, which is what the dead SetConflictsFilesMode in the
    // Git layer's parser used to try to do. Done here rather than there because this is the layer
    // that has the status: the parser only ever sees the diff text.
    static CommitDiff AsConflicted(CommitDiff diff, IReadOnlyList<string> conflictPaths)
    {
        if (conflictPaths.Count == 0)
            return diff;

        return diff with
        {
            FileDiffs = diff
                .FileDiffs.Select(fd =>
                    fd.DiffMode != DiffMode.DiffConflicts && conflictPaths.Contains(fd.PathAfter)
                        ? fd with
                        {
                            DiffMode = DiffMode.DiffConflicts,
                        }
                        : fd
                )
                .ToList(),
        };
    }

    static void AddCommitDiff(
        CommitDiff commitDiff,
        DiffRows rows,
        IReadOnlyDictionary<string, int> fileContext,
        Layout layout,
        bool isHistory
    )
    {
        AddCommitSummery(commitDiff, rows, isHistory);
        // The files of a commit are listed beside it, but those of a file history's commits are not
        AddDiffFileNamesSummery(
            commitDiff,
            rows,
            layout.IsIgnoreWhitespace,
            isFiles: isHistory || !layout.IsListBeside
        );

        commitDiff.FileDiffs.ForEach(fd => AddFileDiff(fd, rows, fileContext, layout));
    }

    // The commit: its id and subject, and who made it when, each field left out that the diff has none
    // of, e.g. the id of the uncommitted changes. The id and subject are the view's top row, where they
    // stay in sight as the diff is scrolled (TitleOf), so they are a row only in a file history, of
    // many commits, where a rule goes above each, which is what tells one commit from the next. The
    // row counting the files follows with no blank row between, as part of what is said about the
    // commit, and the blank row above the first file's header is what sets the files apart.
    static void AddCommitSummery(CommitDiff commitDiff, DiffRows rows, bool isHistory)
    {
        var title = TitleOf(commitDiff);
        if (isHistory)
        {
            rows.Add(Text.Empty);
            rows.AddLine(Text.Yellow("═"));
            if (title.Length > 0)
                rows.Add(title, "", commitDiff.Id);
        }

        List<string> by = [];
        if (commitDiff.Author != "")
            by.Add(commitDiff.Author);
        if (commitDiff.Time != DateTime.MinValue)
            by.Add(commitDiff.Time.Iso());
        // A merge's diff is what it changed of its first parent, which nothing said
        if (commitDiff.MergeFirstParent != "")
            by.Add($"merge, against its first parent {commitDiff.MergeFirstParent.Sid()}");
        if (by.Count > 0)
            rows.Add(Text.Dark(string.Join("  ", by)));
    }

    // A commit's id and subject, e.g. 'f44b73c6  VAT rate from config', or only one of them
    public static Text TitleOf(CommitDiff commitDiff)
    {
        var title = new TextBuilder();
        if (commitDiff.Id != "")
            title.Cyan(commitDiff.Id.Sid());
        if (commitDiff.Message != "")
            title.Dark(title.Length > 0 ? "  " : "").White(commitDiff.Message);
        return title;
    }

    // The files of the commit, below a row counting them, each as the commit details list it: its
    // letter, its path and the lines added and deleted in it, which line up in a column; the row
    // counting them alone where they are listed beside the diff
    static void AddDiffFileNamesSummery(CommitDiff commitDiff, DiffRows rows, bool isIgnoreWhitespace, bool isFiles)
    {
        rows.Add(FilesRow(commitDiff, isIgnoreWhitespace));
        if (!isFiles)
            return;

        var width = Math.Min(MaxPathWidth, commitDiff.FileDiffs.Select(f => PathOf(f).Length).DefaultIfEmpty(0).Max());
        commitDiff.FileDiffs.ForEach(fd => rows.Add(Text.Dark("  ").Add(FileText(fd, PathOf(fd).PadRight(width)))));
    }

    // How far the paths of the file list are padded for the lines after them to line up: a longer
    // path pushes its own lines out of the column rather than every other file's
    const int MaxPathWidth = 50;

    // A file as the file list and its header name it: its letter, its path, the lines added and
    // deleted, or that it is binary, and that its changes are all whitespace, hidden
    static TextBuilder FileText(FileDiff fd, string path)
    {
        var text = ChangedFileText.Row(KindOf(fd), path).ToTextBuilder();
        if (fd.IsBinary)
            text.Dark("  binary");
        else
            ChangedFileText.AddCounts(text, CountOf(fd, DiffMode.DiffAdded), CountOf(fd, DiffMode.DiffRemoved));
        if (fd.IsWhitespaceOnly)
            text.Dark("  whitespace only");
        return text;
    }

    internal static string PathOf(FileDiff fd) => fd.IsRenamed ? $"{fd.PathBefore} → {fd.PathAfter}" : fd.PathAfter;

    // Every changed line is in the hunks whatever their context, so the lines are counted as
    // 'git diff --stat' counts them; a binary file has none, and a conflict's markers are not counted
    internal static int CountOf(FileDiff fd, DiffMode mode) =>
        fd.SectionDiffs.SelectMany(s => s.LineDiffs).Count(l => l.DiffMode == mode);

    // "3 files changed  1 added, 2 modified  +25 -7": how many files of each kind, worded and colored
    // as the letters of the list below it, and the lines added and deleted. With whitespace left out
    // the row says so, since it is then not every change that is counted and shown.
    static Text FilesRow(CommitDiff diff, bool isIgnoreWhitespace)
    {
        var files = diff.FileDiffs;
        var text = Text.White(files.Count == 1 ? "1 file changed" : $"{files.Count} files changed");

        var kinds = files.GroupBy(KindOf).ToDictionary(g => g.Key, g => g.Count());
        var separator = "  ";
        foreach (var (kind, name) in Kinds)
        {
            if (kinds.GetValueOrDefault(kind) is var count and > 0)
            {
                text.Dark(separator).Color(ChangedFileText.ColorOf(kind), $"{count} {name}");
                separator = ", ";
            }
        }

        var added = files.Sum(f => CountOf(f, DiffMode.DiffAdded));
        var removed = files.Sum(f => CountOf(f, DiffMode.DiffRemoved));
        ChangedFileText.AddCounts(text, added, removed);
        if (isIgnoreWhitespace)
            text.Dark("  ").Yellow("whitespace hidden");

        return text;
    }

    // The kinds of files the files row counts, by their letter, in the order it names them
    static readonly (char Kind, string Name)[] Kinds =
    [
        ('A', "added"),
        ('M', "modified"),
        ('R', "renamed"),
        ('D', "deleted"),
        ('C', "conflicted"),
    ];

    // A file renamed and modified is counted as renamed, which the list says it is too
    internal static char KindOf(FileDiff fd) =>
        fd.DiffMode switch
        {
            DiffMode.DiffAdded => 'A',
            DiffMode.DiffRemoved => 'D',
            DiffMode.DiffConflicts => 'C',
            _ => fd.IsRenamed ? 'R' : 'M',
        };

    // A file's diff: a row naming it, which the view draws a rule after across the width, and its
    // hunks, a blank row between two
    static void AddFileDiff(
        FileDiff fileDiff,
        DiffRows rows,
        IReadOnlyDictionary<string, int> fileContext,
        Layout layout
    )
    {
        rows.Add(Text.Empty);

        var fd = fileDiff;
        var header = Text.Magenta(ChangeBar).Dark(" ").Add(FileText(fd, PathOf(fd)));
        if (fileContext.TryGetValue(fd.PathAfter, out var contextLines) && contextLines != DiffContext.Default)
        {
            header.Dark($"  {DiffContext.ToText(contextLines)}");
        }
        if (layout.HiddenFiles.Contains(fd.PathAfter))
        { // Its header alone, h shows the lines again
            rows.AddFileHeader(header.Dark("  lines hidden"), fd.PathAfter);
            return;
        }
        if (layout.Unmerged.Contains(fd.PathAfter))
        { // A file in conflict now is resolved in the resolver, where its sides are, not read here
            rows.AddFileHeader(header, fd.PathAfter);
            rows.Add(UnmergedText(fd));
            return;
        }
        rows.AddFileHeader(header, fd.PathAfter);

        if (fd.IsWhitespaceOnly)
        { // Its lines are in the diff with whitespace, which has nothing else of it to show
            rows.Add(Text.Dark("      Only whitespace changed, which is hidden"));
        }

        // Between two hunks, where unchanged lines are left out, a line says so; the file's header is
        // the first hunk's, whether lines are left out above it or not
        for (var i = 0; i < fileDiff.SectionDiffs.Count; i++)
        {
            if (i > 0)
                rows.AddHunkSeparator(HunkSeparator);
            AddSectionDiff(fileDiff, fileDiff.SectionDiffs[i], rows, layout);
        }
    }

    // What a file in conflict has, and the key that resolves it, e.g. '2 conflicts: Enter opens the
    // resolver', which said 'Enter resolves them', not that it opens a view of its own; one with no
    // markers, e.g. deleted on one side, is a conflict of the whole file, which is asked about instead
    static Text UnmergedText(FileDiff fd)
    {
        var count = fd.SectionDiffs.SelectMany(s => s.LineDiffs).Count(l => l.DiffMode == DiffMode.DiffConflictStart);
        var what = count switch
        {
            0 => "A conflict of the whole file",
            1 => "1 conflict",
            _ => $"{count} conflicts",
        };
        return Text.Dark("      ")
            .Yellow(what)
            .Dark(count == 0 ? ": Enter asks how to resolve it" : ": Enter opens the resolver");
    }

    // Add a file section diff (the actual diff lines for a sub section of a file)
    static void AddSectionDiff(FileDiff fileDiff, SectionDiff sectionDiff, DiffRows rows, Layout layout)
    {
        var leftBlock = new Block();
        var rightBlock = new Block();

        var diffMode = DiffMode.DiffConflictEnd;
        int leftNr = sectionDiff.LeftLine;
        int rightNr = sectionDiff.RightLine;

        foreach (var dl in sectionDiff.LineDiffs)
        {
            // A marker is a line git wrote into the working tree file, so it counts on the right,
            // where the lines after a conflict were numbered 3 low, 4 with a common ancestor
            if (
                dl.DiffMode
                is DiffMode.DiffConflictStart
                    or DiffMode.DiffConflictBase
                    or DiffMode.DiffConflictSplit
                    or DiffMode.DiffConflictEnd
            )
                rightNr++;

            switch (dl.DiffMode)
            {
                case DiffMode.DiffConflictStart:
                    diffMode = DiffMode.DiffConflictStart;
                    AddBlocks(leftBlock, rightBlock, rows, layout);
                    // In one column, the sides one after the other, each named by git's marker
                    var start = Text.BrightMagenta("=== Start of conflict");
                    if (layout.IsOneColumn)
                        start.Dark("  ").Yellow(MarkerName(dl.Line));
                    rows.AddConflictStart(start, layout.IsOneColumn);
                    break;

                // The common ancestor of a 'diff3' or 'zdiff3' conflict. It belongs to neither
                // side, so it is drawn dark across both columns rather than folded into 'ours',
                // which is where it used to end up for want of being recognised at all.
                case DiffMode.DiffConflictBase:
                    diffMode = DiffMode.DiffConflictBase;
                    AddBlocks(leftBlock, rightBlock, rows, layout);
                    rows.Add(Text.Dark("=== Common ancestor"));
                    break;

                case DiffMode.DiffConflictSplit:
                    diffMode = DiffMode.DiffConflictSplit;
                    break;

                case DiffMode.DiffConflictEnd:
                    diffMode = DiffMode.DiffConflictEnd;
                    var end = Text.BrightMagenta("=== End of conflict");
                    if (layout.IsOneColumn)
                    { // Ours, which is gone if the common ancestor came after it, and then theirs
                        AddLines(leftBlock, rows);
                        rows.Add(Text.BrightMagenta("=== ").Cyan(MarkerName(dl.Line)));
                        AddLines(rightBlock, rows);
                        rows.Add(end);
                    }
                    else
                    {
                        AddBlocks(leftBlock, rightBlock, rows, layout);
                        rows.Add(end, end);
                    }
                    break;

                case DiffMode.DiffRemoved:
                    if (diffMode == DiffMode.DiffConflictBase)
                    {
                        rows.Add(Text.Dark(WithoutCr(dl.Line)));
                    }
                    else if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(leftNr, dl.Line, LineKind.Ours);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(leftNr, dl.Line, LineKind.Theirs);
                    }
                    else if (fileDiff.DiffMode == DiffMode.DiffRemoved)
                    { // Whole file removed, use one column
                        var line = new Line(leftNr, dl.Line, LineKind.Removed);
                        rows.Add(LineText(line, layout.IsOneColumn), leftLineNbr: leftNr, removedText: dl.Line);
                    }
                    else
                    {
                        leftBlock.Add(leftNr, dl.Line, LineKind.Removed);
                    }

                    leftNr++;
                    break;

                case DiffMode.DiffAdded:
                    if (diffMode == DiffMode.DiffConflictBase)
                    {
                        rows.Add(Text.Dark(WithoutCr(dl.Line)));
                    }
                    else if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(rightNr, dl.Line, LineKind.Ours);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(rightNr, dl.Line, LineKind.Theirs);
                    }
                    else if (fileDiff.DiffMode == DiffMode.DiffAdded)
                    { // Whole file added, use one column
                        var line = new Line(rightNr, dl.Line, LineKind.Added);
                        rows.Add(LineText(line, layout.IsOneColumn), rightLineNbr: rightNr, addedText: dl.Line);
                    }
                    else
                    {
                        rightBlock.Add(rightNr, dl.Line, LineKind.Added);
                    }

                    rightNr++;
                    break;

                case DiffMode.DiffSame:
                    if (diffMode == DiffMode.DiffConflictBase)
                    {
                        rows.Add(Text.Dark(WithoutCr(dl.Line)));
                    }
                    else if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(rightNr, dl.Line, LineKind.Ours);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(rightNr, dl.Line, LineKind.Theirs);
                    }
                    else
                    {
                        AddBlocks(leftBlock, rightBlock, rows, layout);
                        leftBlock.Add(leftNr, dl.Line, LineKind.Same);
                        rightBlock.Add(rightNr, dl.Line, LineKind.Same);
                    }

                    leftNr++;
                    rightNr++;
                    break;
            }
        }

        AddBlocks(leftBlock, rightBlock, rows, layout);
    }

    // Where unchanged lines are left out between two hunks, '⋯', which the line numbers say how many
    // of. Not the text git writes after a hunk's '@@', the function it found the hunk in: with no
    // pattern for the file's language (e.g. '*.cs diff=csharp' in .gitattributes), git names the
    // nearest line above that is not indented, a C# file's class, or any line of a text, which was
    // more puzzling than telling.
    static Text HunkSeparator => Text.Dark($"{LeftOutIndent}{LeftOutMark}");

    // The mark of lines left out, where a side's line numbers are, which the view draws on the right
    // side too
    internal const string LeftOutMark = "⋯";
    internal const string LeftOutIndent = "   ";

    // The lines of a change side by side: the removed lines on the left, each beside the added line
    // at the same place on the right, as FilePatch pairs them when some of a change's lines are
    // committed or discarded, so what is chosen ends up where the view shows it
    static void AddBlocks(Block leftBlock, Block rightBlock, DiffRows rows, Layout layout)
    {
        if (layout.IsOneColumn)
        {
            AddOneColumn(leftBlock, rightBlock, rows, layout.IsIgnoreWhitespace);
            return;
        }

        var isIgnoreWhitespace = layout.IsIgnoreWhitespace;
        // Add block parts where both block have lines
        var minCount = Math.Min(leftBlock.Lines.Count, rightBlock.Lines.Count);
        for (int i = 0; i < minCount; i++)
        {
            var (left, right) = (leftBlock.Lines[i], rightBlock.Lines[i]);
            var (lT, rT) =
                left.Kind == LineKind.Removed && right.Kind == LineKind.Added
                    ? ChangedSides(left, right, isIgnoreWhitespace)
                    : (LineText(left), LineText(right));
            rows.Add(lT, rT, left.LineNbr, right.LineNbr, RemovedText(left), AddedText(right));
        }

        // Add left lines where no corresponding on right
        for (int i = minCount; i < leftBlock.Lines.Count; i++)
        {
            var lL = leftBlock.Lines[i];
            rows.Add(LineText(lL), NoLine, lL.LineNbr, 0, RemovedText(lL));
        }

        // Add right lines where no corresponding on left
        for (int i = minCount; i < rightBlock.Lines.Count; i++)
        {
            var rL = rightBlock.Lines[i];
            rows.Add(NoLine, LineText(rL), 0, rL.LineNbr, addedText: AddedText(rL));
        }

        leftBlock.Lines.Clear();
        rightBlock.Lines.Clear();
    }

    // The lines of a change in one column, as git writes them: the removed lines and then the added
    // ones, each with the words marked that differ from the line it pairs with side by side. The
    // unchanged line a block starts with, which is on both sides, is drawn once, numbered as it is now.
    static void AddOneColumn(Block leftBlock, Block rightBlock, DiffRows rows, bool isIgnoreWhitespace)
    {
        var (left, right) = (leftBlock.Lines, rightBlock.Lines);
        var same = 0;
        for (; same < Math.Min(left.Count, right.Count) && left[same].Kind == LineKind.Same; same++)
            rows.Add(LineText(right[same]), leftLineNbr: left[same].LineNbr, rightLineNbr: right[same].LineNbr);

        var leftTexts = left.Select(l => LineText(l, isOneColumn: true)).ToList();
        var rightTexts = right.Select(l => LineText(l, isOneColumn: true)).ToList();
        for (int i = same; i < Math.Min(left.Count, right.Count); i++)
        {
            if (left[i].Kind == LineKind.Removed && right[i].Kind == LineKind.Added)
                (leftTexts[i], rightTexts[i]) = ChangedSides(left[i], right[i], isIgnoreWhitespace, isOneColumn: true);
        }

        for (int i = same; i < left.Count; i++)
            rows.Add(leftTexts[i], leftLineNbr: left[i].LineNbr, removedText: RemovedText(left[i]));
        for (int i = same; i < right.Count; i++)
            rows.Add(rightTexts[i], rightLineNbr: right[i].LineNbr, addedText: AddedText(right[i]));

        left.Clear();
        right.Clear();
    }

    // The lines of a side of a conflict in one column
    static void AddLines(Block block, DiffRows rows)
    {
        block.Lines.ForEach(l => rows.Add(LineText(l), leftLineNbr: l.LineNbr));
        block.Lines.Clear();
    }

    // The name git wrote on a conflict's marker, e.g. 'HEAD' of '<<<<<<< HEAD'
    static string MarkerName(string marker) => marker.TrimStart('<', '>', '|', '=').Trim();

    // How the rows are made: with whitespace left out, in one column rather than two, with the lines
    // of some files hidden, which files are in conflict now, which the resolver resolves, and whether
    // the files or the commits are listed beside the diff
    record Layout(
        bool IsIgnoreWhitespace,
        bool IsOneColumn,
        IReadOnlySet<string> HiddenFiles,
        IReadOnlySet<string> Unmerged,
        bool IsListBeside
    );

    // The text of a removed or an added line of a block, as the diff has it, null for an unchanged
    // line or a conflict's, which are no change of the commit
    static string? RemovedText(Line line) => line.Kind == LineKind.Removed ? line.Text : null;

    static string? AddedText(Line line) => line.Kind == LineKind.Added ? line.Text : null;

    // A source line's gutter: the bar of a change in its color, or a space, and the line number
    static TextBuilder Gutter(int lineNbr, Color? bar, Color nbrColor, string mark = ChangeBar) =>
        (bar != null ? Text.Color(bar, mark) : Text.Black(" ")).Color(nbrColor, $"{lineNbr, 4} ");

    // A line as it is, with the gutter of its kind: a change's bar, or in one column its '-' or '+',
    // and its number in its red or green, and the sides of a conflict in the colors the resolver
    // gives them, ours yellow, theirs cyan
    static Text LineText(Line line, bool isOneColumn = false)
    {
        var (nbr, text) = (line.LineNbr, WithoutCr(line.Text));
        return line.Kind switch
        {
            LineKind.Removed => Gutter(nbr, Color.Red, Color.Red, isOneColumn ? RemovedMark : ChangeBar).White(text),
            LineKind.Added => Gutter(nbr, Color.Green, Color.Green, isOneColumn ? AddedMark : ChangeBar).White(text),
            LineKind.Ours => Gutter(nbr, Color.Yellow, Color.Dark).Yellow(text),
            LineKind.Theirs => Gutter(nbr, Color.Cyan, Color.Dark).Cyan(text),
            _ => Gutter(nbr, null, Color.Dark).White(text),
        };
    }

    // A line's '\r', kept of a file's '\r\n' line ends (DiffService reads the diff with them), is shown
    // only where it is the change, a change of line ends: as '␍' on the side of a changed pair that
    // has it when the other has not, which was the same text on both sides when every '\r' was
    // dropped. Elsewhere it is left out, so a file with '\r\n' line ends looks as any other, and so
    // it is with whitespace left out, which git takes the '\r' to be.
    const string CrMark = "␍";

    static (Line, Line) WithCrShown(Line left, Line right, bool isIgnoreWhitespace) =>
        isIgnoreWhitespace || left.Text.EndsWith('\r') == right.Text.EndsWith('\r')
            ? (left with { Text = WithoutCr(left.Text) }, right with { Text = WithoutCr(right.Text) })
            : (left with { Text = CrMarked(left.Text) }, right with { Text = CrMarked(right.Text) });

    static string WithoutCr(string text) => text.EndsWith('\r') ? text[..^1] : text;

    static string CrMarked(string text) => text.EndsWith('\r') ? text[..^1] + CrMark : text;

    // A removed line beside the added line it pairs with. Where most of the two is the same, the
    // words that differ are marked, and the spaces one has before or after its text that the other
    // has not, which no word shows, unless whitespace is left out. Where they share little, nothing
    // is marked: the line was rewritten, and the marks would be noise.
    static (Text, Text) ChangedSides(Line removed, Line added, bool isIgnoreWhitespace, bool isOneColumn = false)
    {
        var (lL, rL) = WithCrShown(removed, added, isIgnoreWhitespace);
        var left = Gutter(lL.LineNbr, Color.Red, Color.Red, isOneColumn ? RemovedMark : ChangeBar);
        var right = Gutter(rL.LineNbr, Color.Green, Color.Green, isOneColumn ? AddedMark : ChangeBar);
        // The spaces before and after the text are marked like words, unless whitespace is left out
        var (removedSpace, addedSpace) = isIgnoreWhitespace ? (Color.White, Color.White) : (Color.RedBg, Color.GreenBg);

        // The text within the spaces before and after it, which the words are compared in. A line of
        // spaces alone is all indent.
        var (leftStart, rightStart) = (lL.Text.TrimStart(), rL.Text.TrimStart());
        var (leftString, rightString) = (leftStart.TrimEnd(), rightStart.TrimEnd());
        var words = LineWords.Of(leftString, rightString, isIgnoreWhitespace);
        if (leftString != rightString && !words.IsAlike)
            return (left.White(lL.Text), right.White(rL.Text));

        // The indent both have, and what one has more of marked
        var leftIndent = lL.Text.Length - leftStart.Length;
        var rightIndent = rL.Text.Length - rightStart.Length;
        var indent = Math.Min(leftIndent, rightIndent);
        left.Black(new string(' ', indent)).Color(removedSpace, new string(' ', leftIndent - indent));
        right.Black(new string(' ', indent)).Color(addedSpace, new string(' ', rightIndent - indent));

        foreach (var run in words.Left)
            left.Color(run.IsChanged ? Color.RedBg : Color.White, run.Text);
        foreach (var run in words.Right)
            right.Color(run.IsChanged ? Color.GreenBg : Color.White, run.Text);

        // The spaces after the text, which only a mark shows
        left.Color(removedSpace, new string(' ', leftStart.Length - leftString.Length));
        right.Color(addedSpace, new string(' ', rightStart.Length - rightString.Length));
        return (left, right);
    }
}
