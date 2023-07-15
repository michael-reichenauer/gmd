using DiffPlex;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Diff;


interface IDiffService
{
    DiffRows ToDiffRows(CommitDiff diff);
    DiffRows ToDiffRows(CommitDiff[] commitDiffs);
    IReadOnlyList<string> GetDiffFilePaths(CommitDiff diff);
    IReadOnlyList<string> GetDiffBinaryFilePaths(CommitDiff diff);
}


class DiffService : IDiffService
{
    const int maxLineDiffsCount = 4;
    const string diffMargin = "┃"; // │ ┃ ;
    public static readonly Text NoLine = Text.Dark(new string('░', 100));

    public DiffRows ToDiffRows(CommitDiff commitDiff)
    {
        return ToDiffRows(new[] { commitDiff });
    }

    public DiffRows ToDiffRows(CommitDiff[] commitDiffs)
    {
        DiffRows rows = new DiffRows();

        if (commitDiffs.Length > 1)
        {
            rows.AddLine(Text.BrightCyan("═"));
            rows.Add(Text.White($"{commitDiffs.Length} Commits:"));

            commitDiffs.ForEach(diff => rows.Add(Text.White(
                $"  {diff.Time.Iso()}  {diff.Message.Max(60, true)}").Dark($"  {diff.Id.Sid(),6} {diff.Author}")));
            rows.Add(Text.Empty);
        }

        commitDiffs.ForEach(diff => AddCommitDiff(diff, rows));
        rows.Add(Text.Empty);
        rows.AddLine(Text.Yellow("━"));
        return rows;
    }

    public IReadOnlyList<string> GetDiffFilePaths(CommitDiff diff)
    {
        return diff.FileDiffs.Select(fd => fd.PathAfter).ToList();
    }


    public IReadOnlyList<string> GetDiffBinaryFilePaths(CommitDiff diff)
    {
        return diff.FileDiffs.Where(fd => fd.IsBinary).Select(fd => fd.PathAfter).ToList();
    }

    void AddCommitDiff(CommitDiff commitDiff, DiffRows rows)
    {
        AddCommitSummery(commitDiff, rows);
        AddDiffFileNamesSummery(commitDiff, rows);

        commitDiff.FileDiffs.ForEach(fd => AddFileDiff(fd, rows));
    }

    // Add a summery of the commit with id, author, date and message
    void AddCommitSummery(CommitDiff commitDiff, DiffRows rows)
    {
        rows.AddLine(Text.Yellow("═"));
        if (commitDiff.Id != "") rows.Add(Text.Dark("Commit:  ").White(commitDiff.Id), "", commitDiff.Id);
        if (commitDiff.Author != "") rows.Add(Text.Dark("Author:  ").White(commitDiff.Author));
        if (commitDiff.Time != DateTime.MinValue) rows.Add(Text.Dark("Date:    ").White(commitDiff.Time.Iso()));
        if (commitDiff.Message != "") rows.Add(Text.Dark("Message: ").White(commitDiff.Message));

        rows.Add(Text.Empty);
    }

    // Add a summery of the files in the commit
    void AddDiffFileNamesSummery(CommitDiff commitDiff, DiffRows rows)
    {
        rows.Add(Text.White($"{commitDiff.FileDiffs.Count} Files:"));

        commitDiff.FileDiffs.ForEach(fd =>
        {
            var path = fd.IsRenamed ? $"{fd.PathBefore} => {fd.PathAfter}" : $"{fd.PathAfter}";
            var diffMode = ToDiffModeText(fd);
            var text = ToColorText($"  {diffMode,-12} {path}", fd).ToTextBuilder();
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

    // Add a diff for the file, which consists of several file section diffs
    void AddFileDiff(FileDiff fileDiff, DiffRows rows)
    {
        rows.Add(Text.Empty);
        rows.AddLine(Text.Blue("━"));

        var fd = fileDiff;
        var path = fd.IsRenamed ? $"{fd.PathBefore} => {fd.PathAfter}" : $"{fd.PathAfter}";
        var diffMode = ToDiffModeText(fd);
        var text = ToColorText($"{diffMode} {path}", fd).ToTextBuilder();
        if (fd.IsRenamed)
        {
            text.Cyan("  (Renamed)");
        }
        if (fd.IsBinary)
        {
            text.Dark("  (Binary)");
        }
        rows.Add(text, fd.PathAfter);

        fileDiff.SectionDiffs.ForEach(sd => AddSectionDiff(sd, rows));
    }

    // Add a file section diff (the actual diff lines for a sub section of a file)
    void AddSectionDiff(SectionDiff sectionDiff, DiffRows rows)
    {
        rows.Add(Text.Empty);
        rows.AddLine(Text.Dark("─"));

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
                    var txt = Text.BrightMagenta("=== Start of conflict");
                    rows.Add(txt, txt);
                    break;

                case DiffMode.DiffConflictSplit:
                    diffMode = DiffMode.DiffConflictSplit;
                    break;

                case DiffMode.DiffConflictEnd:
                    diffMode = DiffMode.DiffConflictEnd;
                    AddBlocks(ref leftBlock, ref rightBlock, rows);
                    var txt2 = Text.BrightMagenta("=== End of conflict");
                    rows.Add(txt2, txt2);
                    break;

                case DiffMode.DiffRemoved:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(leftNr, dl.Line, Color.Yellow);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(leftNr, dl.Line, Color.Yellow);
                    }
                    else
                    {
                        leftBlock.Add(leftNr, dl.Line, Color.Red);
                    }

                    leftNr++;
                    break;

                case DiffMode.DiffAdded:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(rightNr, dl.Line, Color.Yellow);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(rightNr, dl.Line, Color.Yellow);
                    }
                    else
                    {
                        rightBlock.Add(rightNr, dl.Line, Color.Green);
                    }

                    rightNr++;
                    break;

                case DiffMode.DiffSame:
                    if (diffMode == DiffMode.DiffConflictStart)
                    {
                        leftBlock.Add(rightNr, dl.Line, Color.Yellow);
                    }
                    else if (diffMode == DiffMode.DiffConflictSplit)
                    {
                        rightBlock.Add(rightNr, dl.Line, Color.Yellow);
                    }
                    else
                    {
                        AddBlocks(ref leftBlock, ref rightBlock, rows);
                        leftBlock.Add(leftNr, dl.Line, Color.White);
                        rightBlock.Add(rightNr, dl.Line, Color.White);
                    }

                    leftNr++;
                    rightNr++;
                    break;
            }
        }

        AddBlocks(ref leftBlock, ref rightBlock, rows);
        rows.AddLine(Text.Dark("─"));
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
                Text lT = Text.Dark($"{lL.lineNbr,4}").Red(diffMargin).Color(lL.color, lL.text);
                rows.Add(lT, NoLine);
            }
        }

        // Add right lines where no corresponding on left
        if (rightBlock.Lines.Count > leftBlock.Lines.Count)
        {
            for (int i = leftBlock.Lines.Count; i < rightBlock.Lines.Count; i++)
            {
                var rL = rightBlock.Lines[i];
                Text rT = Text.Dark($"{rL.lineNbr,4}").Green(diffMargin).Color(rL.color, rL.text);
                rows.Add(NoLine, rT);
            }
        }

        leftBlock.Lines.Clear();
        rightBlock.Lines.Clear();
    }

    (Text, Text) GetDiffSides(Line lL, Line rL)
    {
        // Ignore leading spaces in diff
        var leftString = lL.text.TrimStart();
        var rightString = rL.text.TrimStart();

        // Add leading spaces back
        var leftText = Text.Black(new string(' ', lL.text.Length - leftString.Length));
        var rightText = Text.Black(new string(' ', rL.text.Length - rightString.Length));

        // Ignore trailing spaces in diff
        leftString = leftString.TrimEnd();
        rightString = rightString.TrimEnd();

        var differ = new Differ();
        var result = differ.CreateCharacterDiffs(leftString, rightString, true);

        if (result.DiffBlocks.Count == 0)
        {   // Same on both sides, just show both sides as same
            Text lT2 = Text.Dark($"{lL.lineNbr,4} ").Color(lL.color, lL.text);
            Text rT2 = Text.Dark($"{rL.lineNbr,4} ").Color(rL.color, rL.text);
            return (lT2, rT2);
        }

        if (result.DiffBlocks.Count > maxLineDiffsCount)
        {   // To many differences in the line, show whole line side as diff
            Text lT2 = Text.Dark($"{lL.lineNbr,4}").Cyan(diffMargin).Color(lL.color, lL.text);
            Text rT2 = Text.Dark($"{rL.lineNbr,4}").Cyan(diffMargin).Color(rL.color, rL.text);
            return (lT2, rT2);
        }

        // There are a few differences in a line, show them marked differently in both sides
        int leftIndex = 0;
        int rightIndex = 0;
        foreach (var diff in result.DiffBlocks)
        {
            // Left side
            if (diff.DeleteStartA > leftIndex)
            {   // Add text before the delete
                leftText.White(leftString.Substring(leftIndex, diff.DeleteStartA - leftIndex));
            }
            if (diff.DeleteCountA > 0)
            {   // Add text int read that is deleted
                leftText.Red(leftString.Substring(diff.DeleteStartA, diff.DeleteCountA));
            }
            leftIndex = diff.DeleteStartA + diff.DeleteCountA;

            // Right side
            if (diff.InsertStartB > rightIndex)
            {   // Add text before the insert
                rightText.White(rightString.Substring(rightIndex, diff.InsertStartB - rightIndex));
            }
            if (diff.InsertCountB > 0)
            {   // Add text int green that is inserted
                rightText.Green(rightString.Substring(diff.InsertStartB, diff.InsertCountB));
            }
            rightIndex = diff.InsertStartB + diff.InsertCountB;
        }

        if (leftIndex < leftString.Length)
        {   // Add text after the last delete
            leftText.White(leftString.Substring(leftIndex));
        }
        if (rightIndex < rightString.Length)
        {   // Add text after the last insert
            rightText.White(rightString.Substring(rightIndex));
        }

        Text lT = Text.Dark($"{lL.lineNbr,4}").Cyan(diffMargin).Add(leftText);
        Text rT = Text.Dark($"{rL.lineNbr,4}").Cyan(diffMargin).Add(rightText);
        return (lT, rT);
    }


    // Returns the colored text based on the diff mode like "Modified:", "Added:", "Removed:" or "Conflicted:"
    Text ToColorText(string text, FileDiff fd)
    {
        if (fd.IsRenamed && !fd.SectionDiffs.Any())
        {
            return Text.Cyan(text);
        }
        if (fd.IsBinary && !fd.SectionDiffs.Any())
        {
            return Text.Dark(text);
        }

        switch (fd.DiffMode)
        {
            case DiffMode.DiffModified:
                return Text.White(text);
            case DiffMode.DiffAdded:
                return Text.Green(text);
            case DiffMode.DiffRemoved:
                return Text.Red(text);
            case DiffMode.DiffConflicts:
                return Text.BrightYellow(text);
        }

        throw Asserter.FailFast($"Unknown diffMode {fd.DiffMode}");
    }


    // Returns the text to show for the diff mode like "Modified:", "Added:", "Removed:" or "Conflicted:"
    string ToDiffModeText(FileDiff fd)
    {
        if (fd.IsRenamed && !fd.SectionDiffs.Any())
        {
            return "Renamed:";
        }

        switch (fd.DiffMode)
        {
            case DiffMode.DiffConflicts:
                return "Conflicts:";
            case DiffMode.DiffModified:
                return "Modified:";
            case DiffMode.DiffAdded:
                return "Added:";
            case DiffMode.DiffRemoved:
                return "Removed:";
        }

        throw Asserter.FailFast($"Unknown diffMode {fd.DiffMode}");
    }
}
