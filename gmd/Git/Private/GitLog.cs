using System.Globalization;

namespace gmd.Git.Private;

internal interface ILogService
{
    Task<Result<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd);
    Task<Result<IReadOnlyList<string>>> GetFileAsync(string reference, string wd);
    Task<Result<IReadOnlyList<Commit>>> GetStashListAsync(string wd);
    Task<Result<IReadOnlyList<Commit>>> GetMergeLogAsync(string reference, string wd);
    Task<Result<IReadOnlyList<string>>> GetIdsChangingFilesAsync(string pathText, int maxCount, string wd);
}

internal class LogService : ILogService
{
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

    Result<IReadOnlyList<Commit>> ParseLines(string output)
    {
        var rows = output.Split('\x00');
        var commits = new List<Commit>();

        foreach (var row in rows)
        {
            if (row.Trim() == "")
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

    Result<Commit> ParseRow(string row)
    {
        var rowParts = row.Split('|');
        if (rowParts.Length < 6)
        {
            return new Error($"failed to parse git commit {row}");
        }

        var id = rowParts[0];
        var sid = id.Sid();
        // Git emits the same date format regardless of the user's locale, so parse it culture
        // invariant. Cultures with a non-Gregorian calendar (e.g. ar-SA, th-TH, fa-IR) would
        // otherwise throw or silently parse the year hundreds of years off.
        var authorTime = DateTime.Parse(rowParts[1], CultureInfo.InvariantCulture);
        var commitTime = DateTime.Parse(rowParts[2], CultureInfo.InvariantCulture);
        var author = rowParts[3];
        var parentIDs = ParseParentIds(rowParts);
        var message = ParseMessage(rowParts);
        var subject = ParseSubject(message);

        return new Commit(id, sid, parentIDs, subject, message, author, authorTime, commitTime);
    }

    string[] ParseParentIds(string[] rowParts)
    {
        var ids = rowParts[4].Trim();
        if (ids == "")
        {
            // No parents, (root commit has no parent)
            return [];
        }

        return ids.Split(' ');
    }

    string ParseMessage(string[] rowParts)
    {
        // The message might contain one or more "|", if so rejoin these parts into original message
        var message = rowParts[5];
        if (rowParts.Length > 6)
        {
            message = string.Join('|', rowParts.Skip(5).ToArray());
        }

        // Skip leading empty lines
        var lines = message.Split('\n');
        message = string.Join('\n', lines.SkipWhile(l => l.Trim() == ""));

        return message.TrimEnd();
    }

    string ParseSubject(string message)
    {
        // Extract subject line from the first line of the message
        var lines = message.Split('\n');
        return lines[0].TrimEnd();
    }
}
