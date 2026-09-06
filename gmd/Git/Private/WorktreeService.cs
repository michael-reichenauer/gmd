namespace gmd.Git.Private;

interface IWorktreeService
{
    Task<R<IReadOnlyList<Worktree>>> ListAsync(string wd);
    Task<R> AddAsync(string path, string branchName, string wd);
    Task<R> AddNewBranchAsync(string path, string newBranchName, string startPoint, string wd);
    Task<R> RemoveAsync(string path, bool isForce, string wd);
    Task<R> PruneAsync(string wd);
    Task<R<IReadOnlyList<string>>> GetIgnoredAsync(IReadOnlyList<string> paths, string wd);
}

class WorktreeService : IWorktreeService
{
    const string BranchRefPrefix = "refs/heads/";

    readonly ICmd cmd;

    public WorktreeService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    // '-z' ends every attribute with NUL and every record with a second one, and prints paths and
    // lock reasons verbatim — without it a reason holding a newline comes back C-quoted. Cmd joins
    // what it reads with newlines and trims the end, neither of which touches a NUL.
    public async Task<R<IReadOnlyList<Worktree>>> ListAsync(string wd)
    {
        if (!Try(out var output, out var e, await cmd.RunAsync("git", "worktree list --porcelain -z", wd)))
            return e;

        return Parse(output);
    }

    public async Task<R> AddAsync(string path, string branchName, string wd) =>
        await cmd.RunAsync("git", $"worktree add \"{path}\" {branchName}", wd);

    public async Task<R> AddNewBranchAsync(string path, string newBranchName, string startPoint, string wd)
    {
        var start = startPoint != "" ? $" {startPoint}" : "";
        return await cmd.RunAsync("git", $"worktree add -b {newBranchName} \"{path}\"{start}", wd);
    }

    // Git refuses to remove a worktree with uncommitted changes unless forced, and a locked one
    // even then (that takes a second --force, deliberately not offered here)
    public async Task<R> RemoveAsync(string path, bool isForce, string wd)
    {
        var force = isForce ? "--force " : "";
        return await cmd.RunAsync("git", $"worktree remove {force}\"{path}\"", wd);
    }

    // Forgets the worktrees whose folders are gone
    public async Task<R> PruneAsync(string wd) => await cmd.RunAsync("git", "worktree prune", wd);

    // Which of the folders git ignores, asked with a trailing separator: a folder-only pattern
    // ('x/') matches nothing for a folder that does not exist yet unless the path says it is one
    public async Task<R<IReadOnlyList<string>>> GetIgnoredAsync(IReadOnlyList<string> paths, string wd)
    {
        if (paths.Count == 0)
            return new List<string>();

        var args = "check-ignore -- " + string.Join(' ', paths.Select(p => $"\"{p.TrimSuffix("/")}/\""));
        var rsp = await cmd.RunAsync("git", args, wd, true, true);
        if (rsp.IsResultError)
        {
            // Exit code 1 is git's answer that none of them is ignored
            if (rsp.ExitCode == 1)
                return new List<string>();
            return R.Error("Failed to check ignored paths", rsp);
        }

        return rsp
            .Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimSuffix("/"))
            .ToList();
    }

    // Each record is a run of 'attribute[ value]' strings ended by an empty one. The first record
    // is the main worktree. Attributes not known here are ignored, so a newer git adding one does
    // not break the list.
    internal static List<Worktree> Parse(string output)
    {
        List<Worktree> worktrees = [];
        var path = "";
        var head = "";
        var branch = "";
        var isBare = false;
        var isDetached = false;
        var isLocked = false;
        var lockReason = "";
        var isPrunable = false;
        var pruneReason = "";

        void EndRecord()
        {
            if (path != "")
            {
                worktrees.Add(
                    new Worktree(
                        Path.GetFullPath(path),
                        head,
                        branch,
                        IsMain: worktrees.Count == 0,
                        isBare,
                        isDetached,
                        isLocked,
                        lockReason,
                        isPrunable,
                        pruneReason
                    )
                );
            }
            (path, head, branch) = ("", "", "");
            (isBare, isDetached, isLocked, lockReason, isPrunable, pruneReason) = (false, false, false, "", false, "");
        }

        foreach (var attribute in output.Split('\0'))
        {
            if (attribute == "")
            {
                EndRecord();
                continue;
            }

            var i = attribute.IndexOf(' ');
            var key = i < 0 ? attribute : attribute[..i];
            var value = i < 0 ? "" : attribute[(i + 1)..];
            switch (key)
            {
                case "worktree":
                    path = value;
                    break;
                case "HEAD":
                    head = value;
                    break;
                case "branch":
                    branch = value.TrimPrefix(BranchRefPrefix);
                    break;
                case "bare":
                    isBare = true;
                    break;
                case "detached":
                    isDetached = true;
                    break;
                case "locked":
                    isLocked = true;
                    lockReason = value;
                    break;
                case "prunable":
                    isPrunable = true;
                    pruneReason = value;
                    break;
            }
        }
        EndRecord();

        return worktrees;
    }
}
