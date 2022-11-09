using System.Diagnostics.CodeAnalysis;

namespace gmd.Utils.Git.Private;

interface IDiffService
{
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId);
    Task<R<CommitDiff>> GetUncommittedDiff();
}

class DiffService : IDiffService
{
    private readonly ICmd cmd;
    private readonly IStatusService statusService;

    public DiffService(ICmd cmd, IStatusService statusService)
    {
        this.cmd = cmd;
        this.statusService = statusService;
    }

    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId)
    {
        var args = "show --date=iso --first-parent --root --patch --ignore-space-change --no-color" +
            $" --find-renames --unified=6 {commitId}";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        var commitDiffs = ParseCommitDiffs(cmdResult.Output, "", false);
        if (commitDiffs.Count == 0)
        {
            return Error.From("Failed to parse diff");
        }

        return commitDiffs[0];
    }


    public async Task<R<CommitDiff>> GetUncommittedDiff()
    {
        var args = "diff --date=iso --first-parent --root --patch --ignore-space-change --no-color" +
            " --find-renames --unified=6 HEAD";
        CmdResult cmdResult = await cmd.RunAsync("git", args);
        if (cmdResult.ExitCode != 0)
        {
            return Error.From(cmdResult.Error);
        }

        // Add commit prefix text to support parser.
        var dateText = DateTime.Now.Iso();
        string output = $"commit  \nMerge: \nAuthor: \nDate:   {dateText}\n\n    Uncommitted files\n\n" +
            cmdResult.Output;

        var commitDiffs = ParseCommitDiffs(output, "", false);
        if (commitDiffs.Count == 0)
        {
            return Error.From("Failed to parse diff");
        }

        var commitDiff = commitDiffs[0];


        var status = await statusService.GetStatusAsync();
        if (status.IsError)
        {
            return status.Error;
        }

        var fileDiffs = commitDiff.FileDiffs.ToList();

        // If status know files are conflicted, the diff mode property needs to be adjusted
        fileDiffs = SetConflictsFilesMode(fileDiffs, status.Value);

        // Add file diffs for new/added files
        var addedFileDiffs = GetAddedFilesDiffs(status.Value, cmd.WorkingDirectory);
        if (addedFileDiffs.Any())
        {
            fileDiffs = fileDiffs.Concat(addedFileDiffs).OrderBy(d => d.PathAfter.ToLower()).ToList();
            return commitDiff with { FileDiffs = fileDiffs };
        }

        return commitDiff;
    }


    IReadOnlyList<CommitDiff> ParseCommitDiffs(string output, string path, bool isUncommitted)
    {
        var lines = output.Split('\n');
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
        if (i < lines.Length && lines[i].StartsWith("Date:   "))
        {
            date = lines[i++].Substring("Date:   ".Length).Trim();
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
            i = i + 3;// Step over index, ---, +++ lines
            (var conflictSectionDiffs, i) = ParseSectionDiffs(i, lines);
            return (new FileDiff(file, file, false, DiffMode.DiffConflicts, conflictSectionDiffs), i, true);
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
        i = i + 3; // Step over index, ---, +++ lines

        (var sectionDiffs, i) = ParseSectionDiffs(i, lines);

        if (i < lines.Length && lines[i].StartsWith("\\ No newline at end of file"))
        {
            i++;  // Skip git diff comment
        }

        return (new FileDiff(before, after, isRenamed, diffMode, sectionDiffs), i, true);
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

        return (DiffMode.DiffModified, i);
    }

    (IReadOnlyList<SectionDiff>, int) ParseSectionDiffs(int i, string[] lines)
    {
        var sectionDiffs = new List<SectionDiff>();
        while (i < lines.Length)
        {
            if (!lines[i].StartsWith("@@"))
            {
                break;
            }

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
            string file = "";

            try
            {
                file = File.ReadAllText(filePath);
            }
            catch (Exception e)
            {
                file = $"<Error reading File {e.ToString()}";
            }

            var lines = file.Split('\n');
            var lineDiffs = lines.Select(l => new LineDiff(DiffMode.DiffAdded, l.TrimEnd().Replace("\t", "   "))).ToList();


            var sectionDiffs = new List<SectionDiff>() { new SectionDiff($"-0,0 +1,{lines.Length}", 0, 0, 0, lines.Length, lineDiffs) };
            var fileDiff = new FileDiff("", name, false, DiffMode.DiffAdded, sectionDiffs);
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
