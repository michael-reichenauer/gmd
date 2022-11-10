using gmd.ViewRepos;

namespace gmd.Cui;


interface IDiffService
{
    DiffRows CreateRows(CommitDiff diff);
    DiffRows CreateRows(CommitDiff[] commitDiffs);
}

class DiffRows
{
    readonly List<DiffRow> rows = new List<DiffRow>();

    public int Count => rows.Count;
    public IReadOnlyList<DiffRow> Rows => rows;

    internal void Add(Text oneRow) =>
        rows.Add(new DiffRow(oneRow, Text.None, DiffRowMode.Left));

    internal void Add(Text left, Text right) =>
        rows.Add(new DiffRow(left, right, DiffRowMode.LeftRight));

    internal void AddToBoth(Text text) =>
        rows.Add(new DiffRow(text, text, DiffRowMode.LeftRight));

    internal void AddLine(Text line) =>
       rows.Add(new DiffRow(line, Text.None, DiffRowMode.Line));
}

record DiffRow(Text Left, Text Right, DiffRowMode Mode);

enum DiffRowMode
{
    LeftRight,
    Left,
    Line,
}


class DiffService : IDiffService
{
    static readonly Text NoLine = Text.New.DarkGray(new string('░', 100));
    public DiffRows CreateRows(CommitDiff commitDiff)
    {
        return CreateRows(new[] { commitDiff });
    }

    public DiffRows CreateRows(CommitDiff[] commitDiffs)
    {
        DiffRows rows = new DiffRows();
        commitDiffs.ForEach(diff => AddCommitDiff(diff, rows));
        return rows;
    }

    void AddCommitDiff(CommitDiff commitDiff, DiffRows rows)
    {
        rows.AddLine(Text.New.Yellow("═"));
        rows.Add(Text.New.DarkGray("Commit:  ").White(commitDiff.Id));
        rows.Add(Text.New.DarkGray("Author:  ").White(commitDiff.Author));
        rows.Add(Text.New.DarkGray("Date:    ").White(commitDiff.Date));
        rows.Add(Text.New.DarkGray("Message: ").White(commitDiff.Message));
        rows.Add(Text.None);

        AddDiffFileNames(commitDiff, rows);

        commitDiff.FileDiffs.ForEach(fd => AddFileDiff(fd, rows));
    }



    void AddDiffFileNames(CommitDiff commitDiff, DiffRows rows)
    {
        rows.Add(Text.New.White($"{commitDiff.FileDiffs.Count} Files:"));

        commitDiff.FileDiffs.ForEach(fd =>
        {
            if (fd.IsRenamed)
            {
                rows.Add(
                    ToColorText($"  {ToDiffModeText(fd.DiffMode),-12} {fd.PathBefore} => {fd.PathAfter}",
                    fd.DiffMode));
                return;
            }

            rows.Add(
                ToColorText($"  {ToDiffModeText(fd.DiffMode),-12} {fd.PathAfter}",
                 fd.DiffMode));
        });
    }

    void AddFileDiff(FileDiff fileDiff, DiffRows rows)
    {
        rows.Add(Text.None);
        rows.AddLine(Text.New.Blue("━"));

        var fd = fileDiff;
        if (fd.IsRenamed)
        {
            rows.Add(
                ToColorText($"{ToDiffModeText(fd.DiffMode)} {fd.PathBefore} => {fd.PathAfter}",
                    fd.DiffMode));
        }
        else
        {
            rows.Add(
                ToColorText($"{ToDiffModeText(fd.DiffMode)} {fd.PathAfter}",
                    fd.DiffMode));
        }

        fileDiff.SectionDiffs.ForEach(sd => AddSectionDiff(sd, rows));
    }

    void AddSectionDiff(SectionDiff sectionDiff, DiffRows rows)
    {
        rows.Add(Text.None);
        rows.AddLine(Text.New.DarkGray("─"));

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
                    rows.AddToBoth(Text.New.DarkGray("=== Start of conflict "));
                    break;

                case DiffMode.DiffConflictSplit:
                    diffMode = DiffMode.DiffConflictSplit;
                    break;

                case DiffMode.DiffConflictEnd:
                    diffMode = DiffMode.DiffConflictEnd;
                    AddBlocks(ref leftBlock, ref rightBlock, rows);
                    rows.Add(Text.New.DarkGray("=== End of conflict "));
                    break;

                case DiffMode.DiffRemoved:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(Text.New.DarkGray($"{leftNr,4}").Yellow($" {dl.Line}"));
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(Text.New.DarkGray($"{leftNr,4}").Yellow($" {dl.Line}"));
                    }
                    else
                    {
                        leftBlock.Add(Text.New.DarkGray($"{leftNr,4}").Red($" {dl.Line}"));
                    }

                    leftNr++;
                    break;

                case DiffMode.DiffAdded:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(Text.New.DarkGray($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(Text.New.DarkGray($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else
                    {
                        rightBlock.Add(Text.New.DarkGray($"{rightNr,4}").Green($" {dl.Line}"));
                    }

                    rightNr++;
                    break;

                case DiffMode.DiffSame:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(Text.New.DarkGray($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(Text.New.DarkGray($"{rightNr,4}").Yellow($" {dl.Line}"));
                    }
                    else
                    {
                        AddBlocks(ref leftBlock, ref rightBlock, rows);
                        leftBlock.Add(Text.New.DarkGray($"{leftNr,4}").White($" {dl.Line}"));
                        rightBlock.Add(Text.New.DarkGray($"{rightNr,4}").White($" {dl.Line}"));
                    }

                    leftNr++;
                    rightNr++;
                    break;
            }
        }

        AddBlocks(ref leftBlock, ref rightBlock, rows);
        rows.AddLine(Text.New.DarkGray("─"));
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
