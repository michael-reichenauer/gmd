using gmd.ViewRepos;

namespace gmd.Cui;


interface IDiffConverter
{
    DiffRows ToDiffRows(CommitDiff diff);
    DiffRows ToDiffRows(CommitDiff[] commitDiffs);
}

class DiffRows
{
    readonly List<DiffRow> rows = new List<DiffRow>();

    internal int MaxLength { get; private set; }
    internal int Count => rows.Count;

    public IReadOnlyList<DiffRow> Rows => rows;

    internal void Add(Text oneRow) =>
        Add(oneRow, Text.None, DiffRowMode.SpanBoth);

    internal void Add(Text left, Text right) =>
        Add(left, right, DiffRowMode.LeftRight);

    internal void AddToBoth(Text text) =>
        Add(text, text, DiffRowMode.LeftRight);

    internal void AddLine(Text line) =>
       Add(line, Text.None, DiffRowMode.Line);

    private void Add(Text left, Text right, DiffRowMode mode)
    {
        if (left.Length > MaxLength)
        {
            MaxLength = left.Length;
        }
        if (right.Length > MaxLength)
        {
            MaxLength = right.Length;
        }

        rows.Add(new DiffRow(left, right, mode));
    }
}

record DiffRow(Text Left, Text Right, DiffRowMode Mode);

enum DiffRowMode
{
    LeftRight,
    SpanBoth,
    Line,
}


class DiffService : IDiffConverter
{
    static readonly Text NoLine = Text.New.Dark(new string('░', 100));
    public DiffRows ToDiffRows(CommitDiff commitDiff)
    {
        return ToDiffRows(new[] { commitDiff });
    }

    public DiffRows ToDiffRows(CommitDiff[] commitDiffs)
    {
        DiffRows rows = new DiffRows();
        commitDiffs.ForEach(diff => AddCommitDiff(diff, rows));
        rows.Add(Text.None);
        rows.AddLine(Text.New.Yellow("━"));
        return rows;
    }

    void AddCommitDiff(CommitDiff commitDiff, DiffRows rows)
    {
        rows.AddLine(Text.New.Yellow("═"));
        if (commitDiff.Id == "")
        {   // Uncommitted changes
            rows.Add(Text.New.Dark("Commit: ").White("Uncommitted changes"));
            rows.Add(Text.New.Dark("Time:   ").White(DateTime.Now.Iso()));
        }
        else
        {   // Some specified commit id
            rows.Add(Text.New.Dark("Commit:  ").White(commitDiff.Id));
            rows.Add(Text.New.Dark("Author:  ").White(commitDiff.Author));
            rows.Add(Text.New.Dark("Date:    ").White(commitDiff.Date));
            rows.Add(Text.New.Dark("Message: ").White(commitDiff.Message));
        }

        rows.Add(Text.None);
        AddDiffFileNamesSummery(commitDiff, rows);

        commitDiff.FileDiffs.ForEach(fd => AddFileDiff(fd, rows));
    }


    void AddDiffFileNamesSummery(CommitDiff commitDiff, DiffRows rows)
    {
        rows.Add(Text.New.White($"{commitDiff.FileDiffs.Count} Files:"));

        commitDiff.FileDiffs.ForEach(fd =>
        {
            var path = fd.IsRenamed ? $"{fd.PathBefore} => {fd.PathAfter}" : $"{fd.PathAfter}";
            var diffMode = ToDiffModeText(fd.DiffMode);
            var text = ToColorText($"  {diffMode,-12} {path}", fd.DiffMode);
            if (fd.IsRenamed)
            {
                text.Cyan(" (Renamed)");
            }
            rows.Add(text);
        });
    }

    void AddFileDiff(FileDiff fileDiff, DiffRows rows)
    {
        rows.Add(Text.None);
        rows.AddLine(Text.New.Blue("━"));

        var fd = fileDiff;
        var path = fd.IsRenamed ? $"{fd.PathBefore} => {fd.PathAfter}" : $"{fd.PathAfter}";
        var diffMode = ToDiffModeText(fd.DiffMode);
        var text = ToColorText($"{diffMode} {path}", fd.DiffMode);
        if (fd.IsRenamed)
        {
            text.Cyan("  (Renamed)");
        }
        rows.Add(text);

        fileDiff.SectionDiffs.ForEach(sd => AddSectionDiff(sd, rows));
    }

    void AddSectionDiff(SectionDiff sectionDiff, DiffRows rows)
    {
        rows.Add(Text.None);
        rows.AddLine(Text.New.Dark("─"));

        var leftBlock = new List<Text>();
        var rightBlock = new List<Text>();
        var diffMode = DiffMode.DiffConflictEnd;
        int leftNr = sectionDiff.LeftLine;
        int rightNr = sectionDiff.RightLine;

        foreach (var dl in sectionDiff.LineDiffs)
        {
            switch (dl.DiffMode)
            {
                case DiffMode.DiffConflictStart:
                    diffMode = DiffMode.DiffConflictStart;
                    AddBlocks(ref leftBlock, ref rightBlock, rows);
                    rows.AddToBoth(Text.New.Dark("=== Start of conflict"));
                    break;

                case DiffMode.DiffConflictSplit:
                    diffMode = DiffMode.DiffConflictSplit;
                    break;

                case DiffMode.DiffConflictEnd:
                    diffMode = DiffMode.DiffConflictEnd;
                    AddBlocks(ref leftBlock, ref rightBlock, rows);
                    rows.AddToBoth(Text.New.Dark("=== End of conflict"));
                    break;

                case DiffMode.DiffRemoved:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(Text.New.Dark($"{leftNr,4}").Yellow($" {dl.Line}"));
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(Text.New.Dark($"{leftNr,4}").Yellow($" {dl.Line}"));
                    }
                    else
                    {
                        leftBlock.Add(Text.New.Dark($"{leftNr,4}").Red($" {dl.Line}"));
                    }

                    leftNr++;
                    break;

                case DiffMode.DiffAdded:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(Text.New.Dark($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(Text.New.Dark($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else
                    {
                        rightBlock.Add(Text.New.Dark($"{rightNr,4}").Green($" {dl.Line}"));
                    }

                    rightNr++;
                    break;

                case DiffMode.DiffSame:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(Text.New.Dark($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(Text.New.Dark($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else
                    {
                        AddBlocks(ref leftBlock, ref rightBlock, rows);
                        leftBlock.Add(Text.New.Dark($"{leftNr,4}").White($" {dl.Line}"));
                        rightBlock.Add(Text.New.Dark($"{rightNr,4}").White($" {dl.Line}"));
                    }

                    leftNr++;
                    rightNr++;
                    break;
            }
        }

        AddBlocks(ref leftBlock, ref rightBlock, rows);
        rows.AddLine(Text.New.Dark("─"));
    }

    private void AddBlocks(ref List<Text> leftBlock, ref List<Text> rightBlock, DiffRows rows)
    {
        // Add block parts where both block have lines
        var minCount = Math.Min(leftBlock.Count, rightBlock.Count);
        for (int i = 0; i < minCount; i++)
        {
            rows.Add(leftBlock[i], rightBlock[i]);
        }

        // Add left lines where no corresponding on right
        if (leftBlock.Count > rightBlock.Count)
        {
            for (int i = rightBlock.Count; i < leftBlock.Count; i++)
            {
                rows.Add(leftBlock[i], NoLine);
            }
        }

        // Add right lines where no corresponding on left
        if (rightBlock.Count > leftBlock.Count)
        {
            for (int i = leftBlock.Count; i < rightBlock.Count; i++)
            {
                rows.Add(NoLine, rightBlock[i]);
            }
        }

        leftBlock.Clear();
        rightBlock.Clear();
    }

    Text ToColorText(string text, DiffMode diffMode)
    {
        switch (diffMode)
        {
            case DiffMode.DiffModified:
                return Text.New.White(text);
            case DiffMode.DiffAdded:
                return Text.New.Green(text);
            case DiffMode.DiffRemoved:
                return Text.New.Red(text);
            case DiffMode.DiffConflicts:
                return Text.New.BrightYellow(text);
        }

        throw (Asserter.FailFast($"Unknown diffMode {diffMode}"));
    }


    string ToDiffModeText(DiffMode diffMode)
    {
        switch (diffMode)
        {
            case DiffMode.DiffModified:
                return "Modified:";
            case DiffMode.DiffAdded:
                return "Added:";
            case DiffMode.DiffRemoved:
                return "Removed:";
            case DiffMode.DiffConflicts:
                return "Conflicted:";
        }

        throw (Asserter.FailFast($"Unknown diffMode {diffMode}"));
    }
}
