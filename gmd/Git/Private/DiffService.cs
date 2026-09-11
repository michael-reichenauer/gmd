using System.Globalization;

namespace gmd.Git.Private;

interface IDiffService
{
    Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd);
    Task<R<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd);
    Task<R<CommitDiff>> GetUncommittedDiff(int contextLines, string wd);
    Task<R<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd);
    Task<R<CommitDiff>> GetRefsDiffAsync(string sha1, string sha2, string message, int contextLines, string wd);
    Task<R<CommitDiff>> GetDiffRangeAsync(string sha1, string sha2, string message, int contextLines, string wd);
    Task<R> RunDiffToolAsync(string path, string wd);
    Task<R> RunMergeToolAsync(string path, string wd);
}

// cSpell:ignore uFEFF
class DiffService : IDiffService
{
    private readonly ICmd cmd;

    public DiffService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<CommitDiff>> GetCommitDiffAsync(string commitId, int contextLines, string wd)
    {
        var args =
            "show --date=iso --first-parent --root --patch --no-color"
            + $" --find-renames --unified={contextLines} {commitId}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;
        var commitDiffs = ParseCommitDiffs(output, "", false);
        if (commitDiffs.Count == 0)
            return new Error("Failed to parse diff");

        return commitDiffs[0];
    }

    public async Task<R<CommitDiff>> GetStashDiffAsync(string name, int contextLines, string wd)
    {
        var args =
            "stash show -u --date=iso --first-parent --root --patch --no-color"
            + $" --find-renames --unified={contextLines} {name}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        return ParseDiff(output, $"Diff of stash {name}");
    }

    public async Task<R<CommitDiff>> GetUncommittedDiff(int contextLines, string wd)
    {
        // To be able to include renamed and added files in uncommitted diff, we first
        // stage all and after diff, the stage is reset.
        //
        // Never while an operation is in progress: 'git add .' stages an unmerged path with the
        // conflict markers as its content, which resolves the conflict, and the 'git reset' below
        // does not put the stages back. This used to test for .git/MERGE_MSG, which a rebase with
        // the --apply backend and 'git am' do not write — so merely opening the diff during one of
        // those destroyed the conflict with no way back but 'git rebase --abort'.
        var needReset = false;
        if (!StatusService.IsOperationInProgress(wd))
        {
            if (await cmd.RunAsync("git", "add .", wd) is Error err)
                return err;
            needReset = true;
        }

        var args =
            "diff --date=iso --first-parent --root --patch --no-color"
            + $" --find-renames --unified={contextLines} HEAD";
        var diff = await cmd.RunAsync("git", args, wd);
        if (diff is Error e && e.Message.Contains("ambiguous argument 'HEAD': unknown revision"))
        { // No commit yet, so there is no HEAD to diff against; what is staged is the whole diff
            diff = await cmd.RunAsync("git", $"diff --staged --unified={contextLines}", wd);
        }
        if (diff is not string output)
        { // The diff failed, reset the 'git add .' if needed
            if (needReset)
                await cmd.RunAsync("git", "reset", wd);
            return diff.Error;
        }

        if (needReset)
        { // Reset the 'git add .' previously
            if (await cmd.RunAsync("git", "reset", wd) is Error err)
                return err;
        }

        // Add commit prefix text to support parser.
        output = $"commit  \nMerge: \nAuthor: \nDate: \n\n  \n\n" + output;

        var commitDiffs = ParseCommitDiffs(output, "", false);
        if (!commitDiffs.Any())
        {
            return new Error("Failed to parse diff");
        }

        return commitDiffs[0] with
        {
            Message = "Uncommitted changes",
        };
    }

    public async Task<R<CommitDiff[]>> GetFileDiffAsync(string path, int contextLines, string wd)
    {
        var args = $"log --date=iso --patch --follow --unified={contextLines} -- \"{path}\"";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        var commitDiffs = ParseCommitDiffs(output, path, false);
        if (!commitDiffs.Any())
        {
            return new Error("Failed to parse diff");
        }

        return commitDiffs.ToArray();
    }

    public async Task<R<CommitDiff>> GetDiffRangeAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    )
    {
        var args = $"diff --find-renames --unified={contextLines} --full-index {sha1}~..{sha2}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        return ParseDiff(output, message);
    }

    public async Task<R<CommitDiff>> GetRefsDiffAsync(
        string sha1,
        string sha2,
        string message,
        int contextLines,
        string wd
    )
    {
        var args = $"diff --find-renames --unified={contextLines} --full-index {sha1} {sha2}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        return ParseDiff(output, message);
    }

    public async Task<R> RunDiffToolAsync(string path, string wd)
    {
        var args = $"difftool --no-prompt {path}";
        return await cmd.RunAsync("git", args, wd);
    }

    public async Task<R> RunMergeToolAsync(string path, string wd)
    {
        var args = $"mergetool --no-prompt {path}";
        return await cmd.RunAsync("git", args, wd);
    }

    // Parse diff output from git diff command
    static CommitDiff ParseDiff(string output, string message = "")
    {
        // Split string and ignore some lines
        var lines = output.Split('\n').Where(l => l != "\\ No newline at end of file").ToArray();

        (var fileDiffs, var i) = ParseFileDiffs(0, lines);

        return new CommitDiff("", "", DateTime.UtcNow, message, fileDiffs);
    }

    // Parse diff output for possible multiple commits
    static IReadOnlyList<CommitDiff> ParseCommitDiffs(string output, string _, bool __)
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

    static (CommitDiff?, int, bool) ParseCommitDiff(int i, string[] lines)
    {
        if (i >= lines.Length || !lines[i].StartsWith("commit "))
        {
            return (null, i, false);
        }

        string author = "";
        DateTime time = DateTime.UtcNow;
        string message = "";

        string commitId = lines[i++]["commit ".Length..].Trim();

        if (i < lines.Length && lines[i].StartsWith("Merge: "))
        { // Skip Merge line
            i++;
        }
        if (i < lines.Length && lines[i].StartsWith("Author: "))
        {
            author = lines[i++]["Author: ".Length..].Trim();
        }
        if (i < lines.Length && lines[i].StartsWith("Date: "))
        {
            var dateText = lines[i++]["Date: ".Length..].Trim();
            // Parsed culture invariant, the git commands use '--date=iso' (see above), which is
            // the same regardless of locale. See the comment in GitLog.ParseRow.
            if (DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                time = dt;
            }
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

        var commitDiff = new CommitDiff(commitId, author, time, message, fileDiffs);
        return (commitDiff, i, true);
    }

    static (IReadOnlyList<FileDiff>, int) ParseFileDiffs(int i, string[] lines)
    {
        var fileDiffs = new List<FileDiff>();
        while (i < lines.Length)
        {
            if (lines[i].StartsWith("commit "))
            { // Next commit
                break;
            }
            if (!lines[i].StartsWith("diff --"))
            { // between file diffs, let try next line
                i++;
                continue;
            }
            (var fileDiff, i, bool ok) = ParseFileDiff(i, lines);
            if (!ok)
            { // A 'diff --' header of a format we do not parse. Skip the header, the loop above
                // then skips its body too, so the remaining files are still shown
                Log.Warn($"Skipped file diff of unknown format: '{lines[i]}'");
                i++;
                continue;
            }
            fileDiffs.Add(fileDiff!);
        }

        return (fileDiffs, i);
    }

    // Only 'diff --git' is parsed. A combined diff ('diff --cc', a merge commit against all its
    // parents) is a different format: n+1 '@' in the hunk header and one prefix column per parent.
    // No gmd git command asks for one, they all use --first-parent. See MODERNIZATION.md for what
    // it would take to support them.
    static (FileDiff?, int, bool) ParseFileDiff(int i, string[] lines)
    {
        if (i >= lines.Length || !lines[i].StartsWith("diff --git "))
        {
            return (null, i, false);
        }

        string files = lines[i][11..];
        var otherIndex = files.IndexOf(" b/");
        string before = files[2..otherIndex];
        string after = files[(otherIndex + 3)..];
        bool isRenamed = before != after;
        i++;

        (DiffMode diffMode, i) = ParseDiffMode(i, lines);
        (i, var isBinary) = ParsePossibleIndexRows(i, lines);

        (var sectionDiffs, i) = ParseSectionDiffs(i, lines);
        if (sectionDiffs.Any(sd => sd.LineDiffs.Any(ld => ld.DiffMode == DiffMode.DiffConflictStart)))
        {
            diffMode = DiffMode.DiffConflicts;
        }

        return (new FileDiff(before, after, isRenamed, isBinary, diffMode, sectionDiffs), i, true);
    }

    static (DiffMode, int) ParseDiffMode(int i, string[] lines)
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
        { // 3 lines with rename info
            i += 3;
            return (DiffMode.DiffModified, i);
        }
        if (lines[i].StartsWith("rename "))
        { // 2 lines with rename info
            i += 2;
            return (DiffMode.DiffModified, i);
        }

        return (DiffMode.DiffModified, i);
    }

    static (int, bool) ParsePossibleIndexRows(int i, string[] lines)
    {
        bool isBinary = false;
        if (i >= lines.Length)
            return (i, isBinary);
        if (lines[i].StartsWith("index "))
        {
            i++;
        }
        if (i >= lines.Length)
            return (i, isBinary);
        if (lines[i].StartsWith("Binary "))
        {
            isBinary = true;
            i++;
        }
        if (i >= lines.Length)
            return (i, isBinary);
        if (lines[i].StartsWith("--- "))
        {
            i++;
        }
        if (i >= lines.Length)
            return (i, isBinary);
        if (lines[i].StartsWith("+++ "))
        {
            i++;
        }
        return (i, isBinary);
    }

    static (IReadOnlyList<SectionDiff>, int) ParseSectionDiffs(int i, string[] lines)
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

    static (SectionDiff?, int, bool) ParseSectionDiff(int i, string[] lines)
    {
        if (!lines[i].StartsWith("@@ "))
        {
            return (null, i, false);
        }

        int endIndex = lines[i][2..].IndexOf("@@");
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

    static (IReadOnlyList<LineDiff>, int) ParseLineDiffs(int i, string[] lines)
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

    static (LineDiff?, int, bool) ParseLineDiff(int i, string[] lines)
    {
        // Replace BOM if present
        lines[i] = lines[i].Replace("\uFEFF", "");

        if (lines[i].StartsWith("+<<<<<<<"))
        {
            return (new LineDiff(DiffMode.DiffConflictStart, AsConflictLine(lines[i++])), i, true);
        }
        // The common ancestor section of the 'diff3' and 'zdiff3' conflict styles. Without this
        // it falls through to '+' and is drawn as part of the 'ours' side, which is what a user
        // with one of those styles set has been seeing.
        if (lines[i].StartsWith("+|||||||"))
        {
            return (new LineDiff(DiffMode.DiffConflictBase, AsConflictLine(lines[i++])), i, true);
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

    static string AsLine(string line)
    {
        return line[1..].Replace("\t", "   ");
    }

    static string AsConflictLine(string line)
    {
        return line[2..].Replace("\t", "   ");
    }
}
