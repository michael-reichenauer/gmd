    Task<R<CommitDiff[]>> GetFileDiffAsync(string path, string wd);
        if (!commitDiffs.Any())
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
            (i, bool isBin) = ParsePossibleIndexRows(i, lines);
            return (new FileDiff(file, file, false, isBin, DiffMode.DiffConflicts, conflictSectionDiffs), i, true);
        (i, var isBinary) = ParsePossibleIndexRows(i, lines);

        if (i < lines.Length && lines[i] == "")
        {   // Skip empty last line
            i++;
        }
        return (new FileDiff(before, after, isRenamed, isBinary, diffMode, sectionDiffs), i, true);
    (int, bool) ParsePossibleIndexRows(int i, string[] lines)
        bool isBinay = false;
        if (i >= lines.Length) return (i, isBinay);
        if (i >= lines.Length) return (i, isBinay);
        if (lines[i].StartsWith("Binary "))
        {
            isBinay = true;
            i++;
        }
        if (i >= lines.Length) return (i, isBinay);
        if (i >= lines.Length) return (i, isBinay);
        return (i, isBinay);
            if (!Try(out var file, out var e, Files.ReadAllText(filePath)))
            var fileDiff = new FileDiff("", name, false, false, DiffMode.DiffAdded, sectionDiffs);