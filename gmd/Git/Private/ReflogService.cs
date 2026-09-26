namespace gmd.Git.Private;

interface IReflogService
{
    Task<Result<IReadOnlyList<ReflogEntry>>> GetReflogAsync(string wd);
}

// The reflogs of the repository: every local branch's, HEAD's and every other worktree's HEAD's.
// They are local, and expire (by default after 90 days, 30 for commits no longer reachable), but
// while they last they are the one record of which branch a commit was made on and which branch a
// branch was started from, which is what the branch inference is otherwise left to guess.
class ReflogService : IReflogService
{
    readonly ICmd cmd;

    internal ReflogService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    // One line per entry: the commit, the entry's selector ('refs/heads/dev@{2}') and the message,
    // separated by NUL, since a message is a commit subject and can hold anything but a newline.
    // The entries of each ref come together, the latest first.
    public async Task<Result<IReadOnlyList<ReflogEntry>>> GetReflogAsync(string wd)
    {
        var result = await cmd.RunAsync("git", "reflog show --all --format=%H%x00%gD%x00%gs", wd, true);
        if (result is not string output)
            return result.Error;

        return Parse(output).ToList();
    }

    internal static IEnumerable<ReflogEntry> Parse(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\0');
            if (parts.Length != 3)
                continue;

            var selector = parts[1];
            var at = selector.LastIndexOf("@{");
            if (at < 1 || !selector.EndsWith('}') || !int.TryParse(selector[(at + 2)..^1], out var index))
                continue;

            yield return new ReflogEntry(parts[0], selector[..at], index, parts[2]);
        }
    }
}
