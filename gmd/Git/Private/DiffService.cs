namespace gmd.Git.Private;

interface IDiffService
{
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd);
    Task<R<CommitDiff>> GetUncommittedDiff(string wd);
    Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd);
}

class DiffService : IDiffService
{
    private readonly ICmd cmd;

    public DiffService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, string wd)
    {
        var args = "show --date=iso --first-parent --root --patch --ignore-space-change --no-color" +
            $" --find-renames --unified=6 {commitId}";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        var commitDiffs = ParseCommitDiffs(output, "", false);
        if (commitDiffs.Count == 0)
        {
            return R.Error("Failed to parse diff");
        }

        return commitDiffs[0];
    }


    public async Task<R<CommitDiff>> GetUncommittedDiff(string wd)
    {
        // To be able to include renamed and added files in uncommitted diff, we first
        // stage all and after diff, the stage is reset.  
        var needReset = false;
        if (!StatusService.IsMergeInProgress(wd))
        {
            if (!Try(out var _, out var err, await cmd.RunAsync("git", "add .", wd))) return err;
            needReset = true;
        }

        var args = "diff --date=iso --first-parent --root --patch --ignore-space-change --no-color" +
            " --find-renames --unified=6 HEAD";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        if (needReset)
        {   // Reset the git add . previously 
            if (!Try(out var _, out var err, await cmd.RunAsync("git", "reset", wd))) return err;
        }

        // Add commit prefix text to support parser.
        var dateText = DateTime.Now.Iso();
        output = $"commit  \nMerge: \nAuthor: \nDate: \n\n  \n\n" + output;

        var commitDiffs = ParseCommitDiffs(output, "", false);
        if (!commitDiffs.Any())
        {
            return R.Error("Failed to parse diff");
        }

        return commitDiffs[0];
    }

    public async Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd)
    {
        var args = $"log --date=iso --patch --follow -- \"{path}\"";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        var commitDiffs = ParseCommitDiffs(output, path, false);
        if (!commitDiffs.Any())
        {
            return R.Error("Failed to parse diff");
        }

        return commitDiffs.ToArray();
    }

    IReadOnlyList<CommitDiff> ParseCommitDiffs(string output, string path, bool isUncommitted)
    {
        // Split string and ignore some lines
        var lines = output.Split('\n').Where(l => l != "\\ No newline at end of file").ToArray();
        var commitDiffs = new List<CommitDiff>();
        int index = 0;
        while (index < lines.Length)
        {
            (var commitDiff, index, bool ok) = ParseCommitDiff(index, lines);
            if (!ok)
            {
                break;
            }

            commitDiffs.Add(commitDiff!);
        }

        return commitDiffs;
    }

    (CommitDiff?, int, bool) ParseCommitDiff(int i, string[] lines)
    {
        if (i >= lines.Length || !lines[i].StartsWith("commit "))
        {
            return (null, i, false);
        }

        string author = "";
        string date = "";
        string message = "";

        string commitId = lines[i++].Substring("commit ".Length).Trim();
        i++; // Skipping next line

        if (i < lines.Length && lines[i].StartsWith("Author: "))
        {
            author = lines[i++].Substring("Author: ".Length).Trim();
        }
        if (i < lines.Length && lines[i].StartsWith("Date: "))
        {
            date = lines[i++].Substring("Date: ".Length).Trim();
        }
        i++; // Skipping next line
        if (i < lines.Length)
        {
            message = lines[i++].Trim();
        }

        while (i < lines.Length)
        {
            if (lines[i++] == "")
            {
                break;
            }
        }

        (var fileDiffs, i) = ParseFileDiffs(i, lines);

        var commitDiff = new CommitDiff(Id: commitId, Author: author, Date: date, Message: message, FileDiffs: fileDiffs);
        return (commitDiff, i, true);
    }

    (IReadOnlyList<FileDiff>, int) ParseFileDiffs(int i, string[] lines)
    {
        var fileDiffs = new List<FileDiff>();
        while (i < lines.Length)
        {
            if (lines[i].StartsWith("commit "))
            {   // Next commit
                break;
            }
            if (!lines[i].StartsWith("diff --"))
            {   // between file diffs, let try next line
                i++;
                continue;
            }
            (var fileDiff, i, bool ok) = ParseFileDiff(i, lines);
            if (!ok)
            {
                break;
            }
            fileDiffs.Add(fileDiff!);
        }

        return (fileDiffs, i);
    }

    (FileDiff?, int, bool) ParseFileDiff(int i, string[] lines)
    {
        if (i >= lines.Length)
        {
            return (null, i, false);
        }

        if (lines[i].StartsWith("diff --cc "))
        {
            string file = lines[i++].Substring(10);
            (DiffMode df, i) = ParseDiffMode(i, lines);
            (i, bool isBin) = ParsePossibleIndexRows(i, lines);
            (var conflictSectionDiffs, i) = ParseSectionDiffs(i, lines);
            return (new FileDiff(file, file, false, isBin, DiffMode.DiffConflicts, conflictSectionDiffs), i, true);
        }

        if (!lines[i].StartsWith("diff --git "))
        {
            return (null, i, false);
        }

        string files = lines[i].Substring(11);
        var parts = files.Split(' ');
        string before = parts[0].Substring(2);
        string after = parts[1].Substring(2);
        bool isRenamed = before != after;
        i++;

        (DiffMode diffMode, i) = ParseDiffMode(i, lines);
        (i, var isBinary) = ParsePossibleIndexRows(i, lines);

        (var sectionDiffs, i) = ParseSectionDiffs(i, lines);

        return (new FileDiff(before, after, isRenamed, isBinary, diffMode, sectionDiffs), i, true);
    }

    (DiffMode, int) ParseDiffMode(int i, string[] lines)
    {
        if (lines[i].StartsWith("new file mode"))
        {
            i++;
            return (DiffMode.DiffAdded, i);
        }
        if (lines[i].StartsWith("deleted file mode"))
        {
            i++;
            return (DiffMode.DiffRemoved, i);
        }
        if (lines[i].StartsWith("similarity "))
        {   // 3 lines with rename info
            i += 3;
            return (DiffMode.DiffModified, i);
        }
        if (lines[i].StartsWith("rename "))
        {   // 2 lines with rename info
            i += 2;
            return (DiffMode.DiffModified, i);
        }

        return (DiffMode.DiffModified, i);
    }

    (int, bool) ParsePossibleIndexRows(int i, string[] lines)
    {
        bool isBinay = false;
        if (i >= lines.Length) return (i, isBinay);
        if (lines[i].StartsWith("index ")) { i++; }
        if (i >= lines.Length) return (i, isBinay);
        if (lines[i].StartsWith("Binary "))
        {
            isBinay = true;
            i++;
        }
        if (i >= lines.Length) return (i, isBinay);
        if (lines[i].StartsWith("--- ")) { i++; }
        if (i >= lines.Length) return (i, isBinay);
        if (lines[i].StartsWith("+++ ")) { i++; }
        return (i, isBinay);
    }

    (IReadOnlyList<SectionDiff>, int) ParseSectionDiffs(int i, string[] lines)
    {
        var sectionDiffs = new List<SectionDiff>();
        while (i < lines.Length)
        {
            (var sectionDiff, i, bool ok) = ParseSectionDiff(i, lines);
            if (!ok)
            {
                break;
            }
            sectionDiffs.Add(sectionDiff!);
        }

        return (sectionDiffs, i);
    }

    (SectionDiff?, int, bool) ParseSectionDiff(int i, string[] lines)
    {
        if (!lines[i].StartsWith("@@ "))
        {
            return (null, i, false);
        }

        int endIndex = lines[i].Substring(2).IndexOf("@@");
        if (endIndex == -1)
        {
            return (null, i, false);
        }

        string changedIndexes = lines[i].Substring(4, endIndex - 2).Trim();
        var parts = changedIndexes.Split('+');

        var leftIndexes = parts[0].Trim().Split(',');
        var rightIndexes = parts[1].Trim().Split(',');

        int leftLine = int.Parse(leftIndexes[0]);
        int leftCount = leftIndexes.Length > 1 ? int.Parse(leftIndexes[1]) : 0;

        int rightLine = int.Parse(rightIndexes[0]);
        int rightCount = rightIndexes.Length > 1 ? int.Parse(rightIndexes[1]) : 0;

        i++;

        (var linesDiffs, i) = ParseLineDiffs(i, lines);

        return (new SectionDiff(changedIndexes, leftLine, leftCount, rightLine, rightCount, linesDiffs), i, true);
    }

    (IReadOnlyList<LineDiff>, int) ParseLineDiffs(int i, string[] lines)
    {
        var lineDiffs = new List<LineDiff>();
        while (i < lines.Length)
        {
            (var lineDiff, i, bool ok) = ParseLineDiff(i, lines);
            if (!ok)
            {
                break;
            }

            lineDiffs.Add(lineDiff!);
        }

        return (lineDiffs, i);
    }

    (LineDiff?, int, bool) ParseLineDiff(int i, string[] lines)
    {
        if (lines[i].StartsWith("+<<<<<<<"))
        {
            return (new LineDiff(DiffMode.DiffConflictStart, AsConflictLine(lines[i++])), i, true);
        }
        if (lines[i].StartsWith("+======="))
        {
            return (new LineDiff(DiffMode.DiffConflictSplit, AsConflictLine(lines[i++])), i, true);
        }
        if (lines[i].StartsWith("+>>>>>>>"))
        {
            return (new LineDiff(DiffMode.DiffConflictEnd, AsConflictLine(lines[i++])), i, true);
        }
        if (lines[i].StartsWith("+"))
        {
            return (new LineDiff(DiffMode.DiffAdded, AsLine(lines[i++])), i++, true);
        }
        if (lines[i].StartsWith("-"))
        {
            return (new LineDiff(DiffMode.DiffRemoved, AsLine(lines[i++])), i++, true);
        }
        if (lines[i].StartsWith(" "))
        {
            return (new LineDiff(DiffMode.DiffSame, AsLine(lines[i++])), i++, true);
        }

        return (null, i, false);
    }


    List<FileDiff> SetConflictsFilesMode(IReadOnlyList<FileDiff> fileDiffs, Status status)
    {
        // Update diffmode on files, which satus has determined are conflicted
        return fileDiffs
            .Select(fd => status.ConflictsFiles.Contains(fd.PathAfter)
                            ? fd with { DiffMode = DiffMode.DiffConflicts } : fd)
            .ToList();
    }

    IReadOnlyList<FileDiff> GetAddedFilesDiffs(Status status, string dirPath)
    {
        var fileDiffs = new List<FileDiff>();
        foreach (var name in status.AddedFiles)
        {
            string filePath = Path.Join(dirPath, name);

            if (!Try(out var file, out var e, Files.ReadAllText(filePath)))
            {
                file = $"<Error reading File {e.ToString()}";
            }

            var lines = file.Split('\n');
            var lineDiffs = lines.Select(l => new LineDiff(DiffMode.DiffAdded, l.TrimEnd().Replace("\t", "   "))).ToList();

            var sectionDiffs = new List<SectionDiff>() { new SectionDiff($"-0,0 +1,{lines.Length}", 0, 0, 0, lines.Length, lineDiffs) };
            var fileDiff = new FileDiff("", name, false, false, DiffMode.DiffAdded, sectionDiffs);
            fileDiffs.Add(fileDiff);
        }

        return fileDiffs;
    }

    string AsLine(string line)
    {
        return line.Substring(1).Replace("\t", "   ");
    }

    string AsConflictLine(string line)
    {
        return line.Substring(2).Replace("\t", "   ");
    }
}
