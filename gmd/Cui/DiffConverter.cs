using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

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

    public override string ToString() => $"Rows: {Count}";
}

record DiffRow(Text Left, Text Right, DiffRowMode Mode);

enum DiffRowMode
{
    LeftRight,
    SpanBoth,
    Line,
}

record Line(int lineNbr, string text, Attribute color);

class Block
{
    public List<Line> Lines { get; } = new List<Line>();
    public void Add(int lineNbr, string text, Attribute color)
    {
        Lines.Add(new Line(lineNbr, text, color));
    }
}

class DiffService : IDiffConverter
{
    const string diffMargin = "┃"; // │ ┃ ;
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
            var diffMode = ToDiffModeText(fd);
            var text = ToColorText($"  {diffMode,-12} {path}", fd);
            if (fd.IsRenamed)
            {
                text.Cyan(" (Renamed)");
            }
            if (fd.IsBinary)
            {
                text.Dark(" (Binary)");
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
        var diffMode = ToDiffModeText(fd);
        var text = ToColorText($"{diffMode} {path}", fd);
        if (fd.IsRenamed)
        {
            text.Cyan("  (Renamed)");
        }
        if (fd.IsBinary)
        {
            text.Dark("  (Binary)");
        }
        rows.Add(text);

        fileDiff.SectionDiffs.ForEach(sd => AddSectionDiff(sd, rows));
    }

    void AddSectionDiff(SectionDiff sectionDiff, DiffRows rows)
    {
        rows.Add(Text.None);
        rows.AddLine(Text.New.Dark("─"));

        var leftBlock = new Block();
        var rightBlock = new Block();

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
                    rows.AddToBoth(Text.New.BrightMagenta("=== Start of conflict"));
                    break;

                case DiffMode.DiffConflictSplit:
                    diffMode = DiffMode.DiffConflictSplit;
                    break;

                case DiffMode.DiffConflictEnd:
                    diffMode = DiffMode.DiffConflictEnd;
                    AddBlocks(ref leftBlock, ref rightBlock, rows);
                    rows.AddToBoth(Text.New.BrightMagenta("=== End of conflict"));
                    break;

                case DiffMode.DiffRemoved:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(leftNr, dl.Line, TextColor.Yellow);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(leftNr, dl.Line, TextColor.Yellow);
                    }
                    else
                    {
                        leftBlock.Add(leftNr, dl.Line, TextColor.Red);
                    }

                    leftNr++;
                    break;

                case DiffMode.DiffAdded:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(rightNr, dl.Line, TextColor.Yellow);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(rightNr, dl.Line, TextColor.Yellow);
                    }
                    else
                    {
                        rightBlock.Add(rightNr, dl.Line, TextColor.Green);
                    }

                    rightNr++;
                    break;

                case DiffMode.DiffSame:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(rightNr, dl.Line, TextColor.Yellow);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(rightNr, dl.Line, TextColor.Yellow);
                    }
                    else
                    {
                        AddBlocks(ref leftBlock, ref rightBlock, rows);
                        leftBlock.Add(leftNr, dl.Line, TextColor.White);
                        rightBlock.Add(rightNr, dl.Line, TextColor.White);
                    }

                    leftNr++;
                    rightNr++;
                    break;
            }
        }

        AddBlocks(ref leftBlock, ref rightBlock, rows);
        rows.AddLine(Text.New.Dark("─"));
    }

    void AddBlocks(ref Block leftBlock, ref Block rightBlock, DiffRows rows)
    {
        // Add block parts where both block have lines
        var minCount = Math.Min(leftBlock.Lines.Count, rightBlock.Lines.Count);
        for (int i = 0; i < minCount; i++)
        {
            var (lT, rT) = GetDiffSides(leftBlock.Lines[i], rightBlock.Lines[i]);
            rows.Add(lT, rT);
        }

        // Add left lines where no corresponding on right
        if (leftBlock.Lines.Count > rightBlock.Lines.Count)
        {
            for (int i = rightBlock.Lines.Count; i < leftBlock.Lines.Count; i++)
            {
                var lL = leftBlock.Lines[i];
                Text lT = Text.New.Dark($"{lL.lineNbr,4}").Red(diffMargin).Color(lL.color, lL.text);
                rows.Add(lT, NoLine);
            }
        }

        // Add right lines where no corresponding on left
        if (rightBlock.Lines.Count > leftBlock.Lines.Count)
        {
            for (int i = leftBlock.Lines.Count; i < rightBlock.Lines.Count; i++)
            {
                var rL = rightBlock.Lines[i];
                Text rT = Text.New.Dark($"{rL.lineNbr,4}").Green(diffMargin).Color(rL.color, rL.text);
                rows.Add(NoLine, rT);
            }
        }

        leftBlock.Lines.Clear();
        rightBlock.Lines.Clear();
    }


    (Text, Text) GetDiffSides(Line lL, Line rL)
    {
        var leftString = lL.text.TrimStart();
        var rightString = rL.text.TrimStart();

        // Highlight characters that are different betweedn left and right
        var leftText = Text.New.Black(new string(' ', lL.text.Length - leftString.Length));
        var rightText = Text.New.Black(new string(' ', rL.text.Length - rightString.Length));
        leftString = leftString.TrimEnd();
        rightString = rightString.TrimEnd();

        var isDiff = false;
        int diffCount = 0;
        const int maxDiffCount = 4;

        if (leftString.Length < 4 || rightString.Length < 4)
        {   // Small lines, show as new lines to avoid to many diffs in a line
            diffCount = maxDiffCount;
        }

        for (int j = 0; j < Math.Max(leftString.Length, rightString.Length) && diffCount < maxDiffCount; j++)
        {
            var leftChar = j < leftString.Length ? leftString[j].ToString() : "";
            var rightChar = j < rightString.Length ? rightString[j].ToString() : "";

            if (leftChar == rightChar)
            {
                leftChar = leftChar == "" ? " " : leftChar;
                rightChar = rightChar == "" ? " " : rightChar;
                leftText.White(leftChar);
                rightText.White(rightChar);
                isDiff = false;
            }
            else
            {
                leftChar = leftChar == "" ? " " : leftChar;
                rightChar = rightChar == "" ? " " : rightChar;
                leftText.Color(lL.color, leftChar);
                rightText.Color(rL.color, rightChar);

                if (!isDiff) diffCount++;
                isDiff = true;
            }
        }


        if (lL.color == TextColor.White && rL.color == TextColor.White)
        {
            Text lT = Text.New.Dark($"{lL.lineNbr,4} ").Add(leftText);
            Text rT = Text.New.Dark($"{rL.lineNbr,4} ").Add(rightText);
            return (lT, rT);
        }
        else
        {
            if (diffCount < maxDiffCount)
            {   // if there are a few diffs, show them
                Text lT = Text.New.Dark($"{lL.lineNbr,4}").Cyan(diffMargin).Add(leftText);
                Text rT = Text.New.Dark($"{rL.lineNbr,4}").Cyan(diffMargin).Add(rightText);
                return (lT, rT);
            }
            else
            {   // To many diffs, show as new lines
                Text lT2 = Text.New.Dark($"{lL.lineNbr,4}").Cyan(diffMargin).Color(lL.color, lL.text);
                Text rT2 = Text.New.Dark($"{rL.lineNbr,4}").Cyan(diffMargin).Color(rL.color, rL.text);
                return (lT2, rT2);
            }
        }
    }


    Text ToColorText(string text, FileDiff fd)
    {
        if (fd.IsRenamed && !fd.SectionDiffs.Any())
        {
            return Text.New.Cyan(text);
        }
        if (fd.IsBinary && !fd.SectionDiffs.Any())
        {
            return Text.New.Dark(text);
        }

        switch (fd.DiffMode)
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

        throw (Asserter.FailFast($"Unknown diffMode {fd.DiffMode}"));
    }


    string ToDiffModeText(FileDiff fd)
    {
        if (fd.IsRenamed && !fd.SectionDiffs.Any())
        {
            return "Renamed:";
        }

        switch (fd.DiffMode)
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

        throw (Asserter.FailFast($"Unknown diffMode {fd.DiffMode}"));
    }
}
