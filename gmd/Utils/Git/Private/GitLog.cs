using gmd.Utils;

namespace gmd.Utils.Git.Private;

internal interface IGitLog
{
    Task<R<IReadOnlyList<Commit>>> GetLog(int maxCount);
}

internal class GitLog : IGitLog
{
    private readonly ICmd cmd;

    internal GitLog(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<IReadOnlyList<Commit>>> GetLog(int maxCount = 30000)
    {
        var args = $"log --all --date-order -z --pretty=%H|%ai|%ci|%an|%P|%B --max-count={maxCount}";
        R<CmdResult> r = await cmd.RunAsync("git", args);
        if (r.IsFaulted)
        {
            return R.Error(r.Exception);
        }

        return ParseLines(r.Value.Output);
    }

    private R<IReadOnlyList<Commit>> ParseLines(IReadOnlyList<string> lines)
    {
        var commits = new List<Commit>();
        foreach (var line in lines)
        {
            var rows = line.Split('\x00');
            foreach (var row in rows)
            {
                if (row.Trim() == "")
                {
                    continue;
                }

                var r = ParseRow(row);
                if (r.IsFaulted)
                {
                    return R.Error(r.Exception);
                }
                commits.Add(r.Value);
            }
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
            message = string.Join('|', rowParts.Skip(4).ToArray());
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

