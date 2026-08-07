namespace gmd.Git.Private;

interface IBlameService
{
    Task<R<Blame>> GetBlameAsync(string path, string reference, string wd);
}

// Blames a file, i.e. which commit last changed each line, using 'git blame --porcelain'.
// The porcelain format emits the author/summary block only the first time a commit is seen, so
// parsing keeps a dictionary of commits and the lines just reference them by id. That also happens
// to be the shape the blame view wants, since it groups consecutive lines of the same commit.
class BlameService : IBlameService
{
    readonly ICmd cmd;

    internal BlameService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<Blame>> GetBlameAsync(string path, string reference, string wd)
    {
        // An empty reference blames the working tree, i.e. including uncommitted lines
        var rev = reference == "" ? "" : $"{reference} ";
        var args = $"blame --porcelain {rev}-- \"{path}\"";

        var result = await cmd.RunAsync("git", args, wd, true);
        if (result.ErrorOutput.Contains(MissingIgnoreRevsError))
        {
            // The repo's 'blame.ignoreRevsFile' names a file that is not there, which git treats as
            // fatal rather than as 'nothing to ignore'. Honoring that config is right, since it is
            // what git blame and the hosting sites do, but a missing text file should not cost the
            // whole view, so retry once with the setting cleared (an empty value clears the list).
            Log.Warn($"blame.ignoreRevsFile could not be read, blaming without it: {result.ErrorOutput}");
            result = await cmd.RunAsync("git", $"-c blame.ignoreRevsFile= {args}", wd);
        }

        if (!Try(out var output, out var e, result))
            return e;

        // Wrap parsing in separate task thread, since a large file might be a lot of lines to parse
        return await Task.Run(() => Parse(output, path, reference));
    }

    static R<Blame> Parse(string output, string path, string reference)
    {
        var lines = output.Split('\n');
        var commits = new Dictionary<string, BlameCommit>();
        var blameLines = new List<BlameLine>();

        // Each blame line is a header line, an optional block of key/value lines (only the first
        // time a commit is seen) and then the content line, which is prefixed with a tab.
        var i = 0;
        while (i < lines.Length)
        {
            if (lines[i] == "")
            { // Trailing empty line of the output, nothing more to parse
                i++;
                continue;
            }

            if (!Try(out var header, out var e, ParseHeader(lines[i])))
                return e;
            i++;

            i = ParseCommitBlock(lines, i, header.Id, path, commits);

            // The content line is prefixed with a tab. Cmd trims the end of the whole output, so a
            // file whose last line is empty has no content line left at all, hence the length check.
            var text = "";
            if (i < lines.Length && lines[i].StartsWith('\t'))
            {
                text = Text(lines[i]);
                i++;
            }

            blameLines.Add(new BlameLine(header.Id, header.FinalLineNbr, header.OriginalLineNbr, text));
        }

        return new Blame(path, reference, blameLines, commits);
    }

    // A header line is '<40 char sha> <original line nbr> <final line nbr> [<lines in group>]'
    static R<(string Id, int OriginalLineNbr, int FinalLineNbr)> ParseHeader(string line)
    {
        var parts = line.Split(' ');
        if (parts.Length < 3 || parts[0].Length != 40)
            return R.Error($"Failed to parse blame header '{line}'");
        if (!int.TryParse(parts[1], out var originalLineNbr) || !int.TryParse(parts[2], out var finalLineNbr))
            return R.Error($"Failed to parse blame header line numbers '{line}'");

        return (parts[0], originalLineNbr, finalLineNbr);
    }

    // Parses the key/value block after a header line and adds the commit if not already known.
    // Returns the index of the first line after the block. A commit that has been seen before has
    // no block at all, but might still repeat 'previous' and 'filename', which are then skipped.
    static int ParseCommitBlock(string[] lines, int i, string id, string path, Dictionary<string, BlameCommit> commits)
    {
        var author = "";
        var authorMail = "";
        var authorTime = DateTime.MinValue;
        var subject = "";
        var isBoundary = false;
        var previousId = "";
        var previousPath = "";
        var filePath = path;

        // The block ends at the content line, or at the next header if the content line is missing.
        // A key is never 40 hex chars, so a header can never be mistaken for a key/value line.
        for (; i < lines.Length && !lines[i].StartsWith('\t') && !IsHeader(lines[i]); i++)
        {
            var line = lines[i];
            var (key, value) = SplitKeyValue(line);
            switch (key)
            {
                case "author":
                    author = value;
                    break;
                case "author-mail":
                    authorMail = value.Trim('<', '>');
                    break;
                case "author-time":
                    authorTime = ParseUnixTime(value);
                    break;
                case "summary":
                    subject = value;
                    break;
                case "boundary":
                    isBoundary = true;
                    break;
                case "previous":
                    // 'previous <sha> <path>', where the path is the name before a rename
                    var parts = value.Split(' ', 2);
                    previousId = parts[0];
                    previousPath = parts.Length > 1 ? parts[1] : path;
                    break;
                case "filename":
                    filePath = value;
                    break;
            }
        }

        if (!commits.ContainsKey(id))
        {
            var isUncommitted = id == UncommittedId;
            commits[id] = new BlameCommit(
                id,
                id.Sid(),
                author,
                authorMail,
                authorTime,
                subject,
                isUncommitted,
                isBoundary,
                previousId,
                previousPath,
                filePath
            );
        }

        return i;
    }

    static bool IsHeader(string line)
    {
        if (line.Length < 41 || line[40] != ' ')
            return false;

        for (var i = 0; i < 40; i++)
        {
            if (!Uri.IsHexDigit(line[i]))
                return false;
        }

        return true;
    }

    static (string key, string value) SplitKeyValue(string line)
    {
        var index = line.IndexOf(' ');
        return index == -1 ? (line, "") : (line[..index], line[(index + 1)..]);
    }

    // The porcelain times are unix seconds, so no locale or calendar is involved. The time zone the
    // commit was made in ('author-tz') is deliberately ignored, the view shows local time.
    static DateTime ParseUnixTime(string value) =>
        long.TryParse(value, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime
            : DateTime.MinValue;

    // Content lines are prefixed with a tab. Tabs within the line are kept verbatim, so a copy of
    // the blamed lines yields the file's own text; the view expands them for drawing.
    static string Text(string line) => line[1..];

    // Same all '0' sha as Server.Repo.UncommittedId, which is what git blame uses for lines that
    // are not committed yet. Repeated here since the git layer cannot reference the server layer.
    const string UncommittedId = "0000000000000000000000000000000000000000";

    const string MissingIgnoreRevsError = "could not open object name list";
}
