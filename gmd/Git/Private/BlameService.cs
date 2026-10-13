namespace gmd.Git.Private;

interface IBlameService
{
    Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd);
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

    public async Task<Result<Blame>> GetBlameAsync(string path, string reference, string wd)
    {
        // An empty reference blames the working tree, i.e. including uncommitted lines
        var rev = reference == "" ? "" : $"{reference} ";
        var args = $"blame --porcelain {rev}-- {Cmd.Quote(path)}";

        var result = await cmd.RunAsync("git", args, wd, true);
        if (result is CmdError cmdError && cmdError.ErrorOutput.Contains(MissingIgnoreRevsError))
        {
            // The repo's 'blame.ignoreRevsFile' names a file that is not there, which git treats as
            // fatal rather than as 'nothing to ignore'. Honoring that config is right, since it is
            // what git blame and the hosting sites do, but a missing text file should not cost the
            // whole view, so retry once with the setting cleared (an empty value clears the list).
            Log.Warn($"blame.ignoreRevsFile could not be read, blaming without it: {cmdError.ErrorOutput}");
            result = await cmd.RunAsync("git", $"-c blame.ignoreRevsFile= {args}", wd);
        }

        if (result is not string output)
            return result.Error;

        // Wrap parsing in separate task thread, since a large file might be a lot of lines to parse
        return await Task.Run(() => Parse(output, path, reference));
    }

    static Result<Blame> Parse(string output, string path, string reference)
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

            var parsed = ParseHeader(lines[i]);
            if (parsed is not BlameHeader header)
                return parsed.Error;
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

    record BlameHeader(string Id, int OriginalLineNbr, int FinalLineNbr);

    // A header line is '<sha> <original line nbr> <final line nbr> [<lines in group>]'. The sha of
    // the lines not committed yet is all zeros, of the length of the repository's ids, and is given
    // as UncommittedId whatever that length is, which is the id the layers above know them by.
    static Result<BlameHeader> ParseHeader(string line)
    {
        var parts = line.Split(' ');
        if (parts.Length < 3 || !ObjectId.Is(parts[0]))
            return new Error($"Failed to parse blame header '{line}'");
        if (!int.TryParse(parts[1], out var originalLineNbr) || !int.TryParse(parts[2], out var finalLineNbr))
            return new Error($"Failed to parse blame header line numbers '{line}'");

        var id = ObjectId.IsZero(parts[0]) ? UncommittedId : parts[0];
        return new BlameHeader(id, originalLineNbr, finalLineNbr);
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
        // A key is never an id, so a header can never be mistaken for a key/value line.
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
                    // 'previous <sha> <path>', where the path is the name before a rename. Both
                    // paths are quoted when they need it, as in a diff (QuotedPath).
                    var parts = value.Split(' ', 2);
                    previousId = parts[0];
                    previousPath = parts.Length > 1 ? QuotedPath.Unquote(parts[1]) : path;
                    break;
                case "filename":
                    filePath = QuotedPath.Unquote(value);
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
        var space = line.IndexOf(' ');
        return space > 0 && ObjectId.Is(line.AsSpan(0, space));
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
    // are not committed yet, in a repository of SHA-1 ids; one of SHA-256 ids has 64 zeros, which
    // ParseHeader gives as this. Repeated here since the git layer cannot reference the server layer.
    const string UncommittedId = "0000000000000000000000000000000000000000";

    const string MissingIgnoreRevsError = "could not open object name list";
}
