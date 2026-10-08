using System.Globalization;

namespace gmd.Git.Private;

internal interface ILogService
{
    Task<Result<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd);
    Task<Result<IReadOnlyList<string>>> GetFileAsync(string reference, string wd);
    Task<Result<IReadOnlyList<Commit>>> GetStashListAsync(string wd);
    Task<Result<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd);
    Task<Result<IReadOnlyList<string>>> GetIdsChangingFilesAsync(string pathText, int maxCount, string wd);
    Task<Result<IReadOnlyList<string>>> GetIdsChangingTextAsync(string text, int maxCount, string wd);
    Task<Result<IReadOnlyList<Commit>>> GetUnreachableCommitsAsync(
        IReadOnlyList<string> ids,
        IReadOnlyList<string> alsoReached,
        string wd
    );
    Task<Result<IReadOnlySet<string>>> GetExistingCommitIdsAsync(IReadOnlyList<string> ids, string wd);
}

internal class LogService : ILogService
{
    // The ids given on one command line: a Windows command line holds about 32K characters, and an
    // id is 41 of them with its space
    internal const int IdsPerCall = 400;
    const int MaxUnreachableCount = 2000;

    private readonly ICmd cmd;

    internal LogService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<Result<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd)
    {
        var args = $"log --all --date-order -z --pretty=\"%H|%ai|%ci|%an|%P|%B\" --max-count={maxCount}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        // Wrap parsing in separate task thread, since it might be a lot of commits to parse
        return await Task.Run(() => ParseLines(output));
    }

    // The ids of the commits that changed a file whose path contains the text, in any case, newest
    // first, for a search. A pathspec with no 'glob' magic lets '*' match across '/', so '*text*'
    // is anywhere in the path. --full-history, since git's default simplification hides a commit
    // on a side branch whose change the merge left out, and --no-merges, since with the full history
    // every merge bringing in such a change is listed as well, which would bury the commits made.
    //
    // The text is what the user typed: a '"' would end the argument, and git writes paths with '/'
    // on every platform.
    public async Task<Result<IReadOnlyList<string>>> GetIdsChangingFilesAsync(string pathText, int maxCount, string wd)
    {
        var text = pathText.Replace("\"", "").Replace('\\', '/');
        var args = $"log --all --full-history --no-merges --format=%H --max-count={maxCount} -- \":(icase)*{text}*\"";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
    }

    // The ids of the commits whose changes added or removed the text, in any case, newest first, for
    // a search of the changes: 'git log -S', the pickaxe, which counts the text in each file before
    // and after a commit, so a line that only moved is not a change of it. The text is what the user
    // typed, so a '"' would end the argument.
    public async Task<Result<IReadOnlyList<string>>> GetIdsChangingTextAsync(string text, int maxCount, string wd)
    {
        var args = $"log --all -i -S\"{text.Replace("\"", "")}\" --format=%H --max-count={maxCount}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
    }

    // The commits reachable from the given ones that no ref reaches, i.e. that git still has only
    // because a reflog mentions them: '--not --all' takes away everything a branch, a tag, a stash,
    // a remote branch or a worktree's HEAD reaches, and 'alsoReached' what else counts as kept, the
    // stashes older than the latest, which only the stash's own reflog holds. An id gc has removed
    // is skipped rather than failing the command. The ids are given in chunks, and a commit two
    // chunks both reach is listed once.
    public async Task<Result<IReadOnlyList<Commit>>> GetUnreachableCommitsAsync(
        IReadOnlyList<string> ids,
        IReadOnlyList<string> alsoReached,
        string wd
    )
    {
        List<Commit> commits = [];
        HashSet<string> listed = [];
        foreach (var chunk in ids.Chunk(IdsPerCall))
        {
            var args =
                $"log --ignore-missing -z --date-order --pretty=\"%H|%ai|%ci|%an|%P|%B\" --max-count={MaxUnreachableCount} "
                + $"{string.Join(' ', chunk)} --not --all {string.Join(' ', alsoReached)}";
            var result = await cmd.RunAsync("git", args.TrimEnd(), wd);
            if (result is not string output)
                return result.Error;
            var parsedResult = ParseLines(output);
            if (parsedResult is not IReadOnlyList<Commit> parsed)
                return parsedResult.Error;

            commits.AddRange(parsed.Where(c => listed.Add(c.Id)));
        }
        return commits;
    }

    // The ones of the ids that are commits git still has, since one no ref or reflog reaches is
    // pruned by a gc in time. 'rev-list --no-walk' lists just the commits given, and
    // '--ignore-missing' passes over an id git does not have rather than failing on it.
    public async Task<Result<IReadOnlySet<string>>> GetExistingCommitIdsAsync(IReadOnlyList<string> ids, string wd)
    {
        HashSet<string> existing = [];
        foreach (var chunk in ids.Chunk(IdsPerCall))
        {
            var args = $"rev-list --no-walk --ignore-missing {string.Join(' ', chunk)}";
            var result = await cmd.RunAsync("git", args, wd);
            if (result is not string output)
                return result.Error;
            existing.UnionWith(
                output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            );
        }
        return existing;
    }

    public async Task<Result<IReadOnlyList<Commit>>> GetStashListAsync(string wd)
    {
        var args = $"stash list -z --pretty=\"%H|%ai|%ci|%an|%P|%gd:%B\"";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        // Wrap parsing in separate task thread, since it might be a lot of commits to parse
        return await Task.Run(() => ParseLines(output));
    }

    public async Task<Result<IReadOnlyList<string>>> GetFileAsync(string reference, string wd)
    {
        var args = $"ls-tree -r {reference} --name-only";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        // Wrap parsing in separate task thread, since it might be a lot of commits to parse
        return output.Split('\n').ToList();
    }

    public async Task<Result<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd)
    {
        var args = $"log --date-order -z --pretty=\"%H|%ai|%ci|%an|%P|%B\" --max-count=100 HEAD..{reference}";
        var result = await cmd.RunAsync("git", args, wd);
        if (result is not string output)
            return result.Error;

        // Wrap parsing in separate task thread, since it might be a lot of commits to parse
        return await Task.Run(() => ParseLines(output));
    }

    // Parsed as spans of the output, so that only the fields kept become strings. The log of a large
    // repo is tens of MB, which splitting into rows, fields and lines copied over and over, about a
    // hundred milliseconds of a refresh.
    Result<IReadOnlyList<Commit>> ParseLines(string output)
    {
        var commits = new List<Commit>();

        var rest = output.AsSpan();
        while (!rest.IsEmpty)
        {
            // Rows are NUL separated, and one of white space only, e.g. after the last, is no commit
            var end = rest.IndexOf('\x00');
            var row = end == -1 ? rest : rest[..end];
            rest = end == -1 ? [] : rest[(end + 1)..];
            if (row.IsWhiteSpace())
            {
                continue;
            }

            var parsed = ParseRow(row);
            if (parsed is not Commit commit)
                return parsed.Error;

            commits.Add(commit);
        }

        return commits;
    }

    Result<Commit> ParseRow(ReadOnlySpan<char> row)
    {
        // The fields are '|' separated. The message is the last, and the last range is the rest of
        // the row, so a message containing '|' is kept whole.
        Span<Range> fields = stackalloc Range[6];
        if (row.Split(fields, '|') < 6)
        {
            return new Error($"failed to parse git commit {row}");
        }

        var id = row[fields[0]].ToString();
        var sid = id.Sid();
        // Git emits the same date format regardless of the user's locale, so parse it culture
        // invariant. Cultures with a non-Gregorian calendar (e.g. ar-SA, th-TH, fa-IR) would
        // otherwise throw or silently parse the year hundreds of years off.
        var authorTime = DateTime.Parse(row[fields[1]], CultureInfo.InvariantCulture);
        var commitTime = DateTime.Parse(row[fields[2]], CultureInfo.InvariantCulture);
        var author = row[fields[3]].ToString();
        var parentIDs = ParseParentIds(row[fields[4]]);
        var message = ParseMessage(row[fields[5]]);
        var subject = ParseSubject(message);

        return new Commit(id, sid, parentIDs, subject, message, author, authorTime, commitTime);
    }

    string[] ParseParentIds(ReadOnlySpan<char> field)
    {
        var ids = field.Trim();
        if (ids.IsEmpty)
        {
            // No parents, (root commit has no parent)
            return [];
        }

        return ids.ToString().Split(' ');
    }

    string ParseMessage(ReadOnlySpan<char> field)
    {
        // Skip leading empty lines, i.e. lines of white space only: the message starts at the line
        // of the first character that is not white space, or is empty if there is none
        var first = 0;
        while (first < field.Length && char.IsWhiteSpace(field[first]))
        {
            first++;
        }
        if (first == field.Length)
        {
            return "";
        }

        var lineStart = field[..first].LastIndexOf('\n') + 1;
        return field[lineStart..].TrimEnd().ToString();
    }

    string ParseSubject(string message)
    {
        // Extract subject line from the first line of the message
        var end = message.IndexOf('\n');
        return (end == -1 ? message.AsSpan() : message.AsSpan(0, end)).TrimEnd().ToString();
    }
}
