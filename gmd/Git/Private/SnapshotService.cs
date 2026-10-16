namespace gmd.Git.Private;

interface ISnapshotService
{
    Task<Result<string>> WriteWorkingTreeAsync(string wd);
    Task<Result<string>> WriteCommitAsync(string tree, IReadOnlyList<string> parents, string message, string wd);
    Task<Result> LogRefAsync(string refName, string id, string message, string wd);
    Task<Result<IReadOnlyList<RefLogRecord>>> GetRefLogAsync(string refName, string wd);
    Task<Result> ApplyDiffAsync(string fromId, string toId, string wd);
}

// The working tree kept as git keeps everything, in objects: its files as a tree, a commit of it,
// and a ref whose reflog lists such commits, which keeps them from git's garbage collection until
// the reflog expires them. Nothing of the working tree or the index is changed by any of it. Then
// the difference between two of them applied to the working tree, which is how what a discard threw
// away is put back.
class SnapshotService : ISnapshotService
{
    // The diff to apply, in the format git apply reads whatever the user's config says, as
    // DiffService pins its own, and with the bytes of binary files in it
    const string DiffFormat =
        "-c diff.suppressBlankEmpty=false diff --binary --no-color --no-ext-diff --no-textconv --no-renames"
        + " --src-prefix=a/ --dst-prefix=b/";

    readonly ICmd cmd;

    public SnapshotService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    // The files of the working tree as the uncommitted diff has them, staged into a copy of the index
    // (TempIndex): the changed ones and the new ones, and not the ones git ignores
    public Task<Result<string>> WriteWorkingTreeAsync(string wd) =>
        TempIndex.WithCopyAsync<string>(
            wd,
            async env =>
            {
                if (await cmd.RunAsync("git", "add -A", wd, environment: env) is Error addError)
                    return addError;
                return await cmd.RunAsync("git", "write-tree", wd, environment: env) switch
                {
                    string tree => tree.Trim(),
                    Error e => e,
                };
            }
        );

    // A commit of the tree, on no branch, with no hook run and no signing, as a commit for a record
    // should be made
    public async Task<Result<string>> WriteCommitAsync(
        string tree,
        IReadOnlyList<string> parents,
        string message,
        string wd
    )
    {
        var parentArgs = string.Concat(parents.Select(p => $" -p {p}"));
        return await cmd.RunAsync("git", $"commit-tree {tree}{parentArgs} -m {Cmd.Quote(message)}", wd) switch
        {
            string id => id.Trim(),
            Error e => e,
        };
    }

    // Points the ref at the commit, with an entry in the ref's reflog, which git keeps for refs/heads
    // alone unless asked
    public Task<Result> LogRefAsync(string refName, string id, string message, string wd) =>
        ToResult(cmd.RunAsync("git", $"update-ref --create-reflog -m {Cmd.Quote(message)} {refName} {id}", wd));

    // The ref's reflog, newest first: each entry's commit, the commit's time, and the entry's message.
    // None for a ref that is not there yet.
    public async Task<Result<IReadOnlyList<RefLogRecord>>> GetRefLogAsync(string refName, string wd)
    {
        var exists = await cmd.RunRawAsync("git", $"rev-parse --verify --quiet {refName}", wd, skipLogError: true);
        if (exists.ExitCode != 0)
            return new List<RefLogRecord>();

        var args = $"log -g --no-show-signature --format=%H%x00%ct%x00%gs {refName} --";
        if (await cmd.RunAsync("git", args, wd) is not string output)
            return new Error($"Failed to read the reflog of {refName}");

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('\0'))
            .Where(p => p.Length == 3 && long.TryParse(p[1], out _))
            .Select(p => new RefLogRecord(
                p[0],
                DateTimeOffset.FromUnixTimeSeconds(long.Parse(p[1])).LocalDateTime,
                p[2]
            ))
            .ToList();
    }

    // The difference from the one commit to the other, binary files too, applied to the working tree
    // alone, all of it or none of it, as git applies a patch. Git writes it to a file, since Cmd trims
    // its output, and the last line of a patch can end in spaces.
    public async Task<Result> ApplyDiffAsync(string fromId, string toId, string wd)
    {
        var patchPath = Path.Join(Path.GetTempPath(), $"gmd-patch-{Guid.NewGuid():N}.diff");
        try
        {
            var diff = $"{DiffFormat} --output={Cmd.Quote(patchPath)} {fromId} {toId}";
            if (await cmd.RunAsync("git", diff, wd) is Error diffError)
                return diffError;
            if (Result.Catch(() => new FileInfo(patchPath).Length) is not long length)
                return new Error("Failed to read the difference");
            if (length == 0)
                return Result.Ok;

            var apply = $"apply --binary --whitespace=nowarn {Cmd.Quote(patchPath)}";
            return await cmd.RunAsync("git", apply, wd) switch
            {
                string => Result.Ok,
                Error e => new Error("Git could not apply the changes", e),
            };
        }
        finally
        {
            Result.Catch(() => File.Delete(patchPath));
        }
    }

    static async Task<Result> ToResult(Task<Result<string>> run) =>
        await run switch
        {
            string => Result.Ok,
            Error e => e,
        };
}
