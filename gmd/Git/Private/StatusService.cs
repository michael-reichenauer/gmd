namespace gmd.Git.Private;

interface IStatusService
{
    Task<Result<Status>> GetStatusAsync(string wd);
    Task<Result<Status>> GetStatusWithoutLocksAsync(string wd);
}

class StatusService : IStatusService
{
    // '-z' gives every path as it is, where without it git quotes a path with a space, a '"' or a
    // character that is not ASCII, the last as octal escapes, which were kept: a file with an 'ä'
    // in its name could be neither committed by name, discarded, nor blamed.
    const string StatusArgs = "status --porcelain -z --untracked-files=all";

    readonly ICmd cmd;

    public StatusService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public Task<Result<Status>> GetStatusAsync(string wd) => GetStatusAsync("", wd);

    // For a worktree someone else is working in. A plain 'git status' refreshes the index and
    // writes it back, holding 'index.lock' while it does, and a 'git add' or 'commit' run there
    // at that moment fails on the lock. '--no-optional-locks' skips the write, so the read leaves
    // no trace in the other worktree.
    public Task<Result<Status>> GetStatusWithoutLocksAsync(string wd) => GetStatusAsync("--no-optional-locks ", wd);

    async Task<Result<Status>> GetStatusAsync(string options, string wd)
    {
        var result = await cmd.RunAsync("git", options + StatusArgs, wd);
        if (result is not string output)
            return result.Error;

        return Parse(output, wd);
    }

    // An entry per path, 'XY <path>', NUL separated, where X is what the index has and Y what the
    // working tree has. A rename or a copy is 'XY <path>' followed by an entry of the path it was
    // renamed or copied from.
    Result<Status> Parse(string statusText, string wd)
    {
        var entries = statusText.Split('\0');

        int conflicted = 0;
        List<ConflictedFile> conflicts = [];
        int added = 0;
        List<string> addedFiles = [];
        int deleted = 0;
        List<string> deletedFiles = [];
        int modified = 0;
        List<string> modifiedFiles = [];
        int renamed = 0;
        List<string> renamedSourceFiles = [];
        List<string> renamedTargetFiles = [];

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 4)
            {
                continue; // The empty one after the last NUL
            }
            var (x, y, path) = (entry[0], entry[1], entry[3..]);
            var from = IsRenameOrCopy(x) || IsRenameOrCopy(y) ? entries.ElementAtOrDefault(++i) ?? "" : "";

            // The seven unmerged codes. The kind is kept rather than discarded: it is the only
            // record of what git could not merge, and it decides what can be offered for the path
            // (a modify/delete has no text to merge, only a keep-or-delete choice).
            if (ToConflictKind(x, y) is ConflictKind kind)
            {
                conflicted++;
                conflicts.Add(new ConflictedFile(path, kind));
                continue;
            }

            // What the change is to the user is the index's code, or the working tree's when the
            // index has none
            switch (x != ' ' ? x : y)
            {
                // Untracked, or staged as new ('A ', 'AM', 'AD'), or intended to be (' A'): a new
                // file either way, which discarding deletes. A copy is a new file too.
                case '?':
                case 'A':
                case 'C':
                    added++;
                    addedFiles.Add(path);
                    break;
                case 'D':
                    deleted++;
                    deletedFiles.Add(path);
                    break;
                case 'R':
                    renamed++;
                    renamedSourceFiles.Add(from);
                    renamedTargetFiles.Add(path);
                    break;
                default:
                    modified++;
                    modifiedFiles.Add(path);
                    break;
            }
        }
        var operation = OperationState.Read(wd);

        return new Status(
            modified,
            added,
            deleted,
            conflicted,
            renamed,
            operation.Operation,
            operation.MergeMessage,
            operation.MergeHeadId,
            operation.BranchName,
            operation.Step,
            operation.Total,
            operation.IsFinishedByCommit,
            modifiedFiles.ToArray(),
            addedFiles.ToArray(),
            deletedFiles.ToArray(),
            conflicts.ToArray(),
            renamedSourceFiles.ToArray(),
            renamedTargetFiles.ToArray()
        );
    }

    static bool IsRenameOrCopy(char code) => code is 'R' or 'C';

    static ConflictKind? ToConflictKind(char x, char y) =>
        (x, y) switch
        {
            ('U', 'U') => ConflictKind.BothModified,
            ('A', 'A') => ConflictKind.BothAdded,
            ('D', 'D') => ConflictKind.BothDeleted,
            ('A', 'U') => ConflictKind.AddedByUs,
            ('U', 'A') => ConflictKind.AddedByThem,
            ('U', 'D') => ConflictKind.DeletedByThem,
            ('D', 'U') => ConflictKind.DeletedByUs,
            _ => null,
        };
}
