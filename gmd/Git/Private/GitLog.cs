namespace gmd.Git.Private;

internal interface ILogService
{
    Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd);
    Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd);
    Task<R<IReadOnlyList<Commit>>> GetStashListAsync(string wd);
}

internal class LogService : ILogService
{
    private readonly ICmd cmd;

    internal LogService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<IReadOnlyList<Commit>>> GetLogAsync(int maxCount, string wd)
    {
        var args = $"log --all --date-order -z --pretty=\"%H|%ai|%ci|%an|%P|%B\" --max-count={maxCount}";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        // Wrap parsing in separate task thread, since it might be a lot of commits to parse
        return await Task.Run(() => ParseLines(output));
    }

    public async Task<R<IReadOnlyList<Commit>>> GetStashListAsync(string wd)
    {
        var args = $"stash list -z --pretty=\"%H|%ai|%ci|%an|%P|%gd:%B\"";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        // Wrap parsing in separate task thread, since it might be a lot of commits to parse
        return await Task.Run(() => ParseLines(output));
    }

    public async Task<R<IReadOnlyList<string>>> GetFileAsync(string reference, string wd)
    {
        var args = $"ls-tree -r {reference} --name-only";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd))) return e;

        // Wrap parsing in separate task thread, since it might be a lot of commits to parse
        return output.Split('\n').ToList();
    }


    private R<IReadOnlyList<Commit>> ParseLines(string output)
    {
        var rows = output.Split('\x00');
        var commits = new List<Commit>();

        foreach (var row in rows)
        {
            if (row.Trim() == "")
            {
                continue;
            }

            if (!Try(out var commit, out var e, ParseRow(row))) return e;

            commits.Add(commit);
        }


        return commits;
    }

    private R<Commit> ParseRow(string row)
    {
        var rowParts = row.Split('|');
        if (rowParts.Length < 6)
        {
            return R.Error($"failed to parse git commit {row}");
        }

        var id = rowParts[0];
        var sid = id.Substring(0, 6);
        var authorTime = DateTime.Parse(rowParts[1]);
        var commitTime = DateTime.Parse(rowParts[2]);
        var author = rowParts[3];
        var parentIDs = ParseParentIds(rowParts);
        var message = ParseMessage(rowParts);
        var subject = ParseSubject(message);

        return new Commit(id, sid, parentIDs, subject, message, author, authorTime, commitTime);
    }

    private string[] ParseParentIds(string[] rowParts)
    {
        var ids = rowParts[4].Trim();
        if (ids == "")
        {
            // No parents, (root commit has no parent)
            return new string[] { };
        }

        return ids.Split(' ');
    }

    private string ParseMessage(string[] rowParts)
    {
        // The message might contain one or more "|", if so rejoin these parts into original message
        var message = rowParts[5];
        if (rowParts.Length > 6)
        {
            message = string.Join('|', rowParts.Skip(5).ToArray());
        }

        return message.TrimEnd();
    }

    private string ParseSubject(string message)
    {
        // Extract subject line from the first line of the message
        var lines = message.Split('\n');
        return lines[0].TrimEnd();
    }
}

