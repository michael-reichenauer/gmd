namespace gmd.Git.Private;

// The operations git can stop part way through, and the three things that can then be done to one:
// abort it, carry on with it, or skip the commit it stopped on. Before this, gmd could start a
// merge, a rebase and a cherry-pick but finish none of them once they conflicted — a stopped rebase
// could only be escaped by leaving gmd for a console.
interface IConflictService
{
    Task<R> AbortOperationAsync(string wd);
    Task<R> ContinueOperationAsync(string wd);
    Task<R> SkipOperationAsync(string wd);
    Task<R<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd);
}

class ConflictService : IConflictService
{
    // 'git rebase --continue' and its siblings open an editor on the commit message, which would
    // hang gmd behind the terminal it owns. ICmd cannot pass environment variables, so GIT_EDITOR
    // is out; '-c core.editor=true' is the same thing said as config, and 'true' is a program that
    // exits 0 without writing, i.e. "accept the message as it stands".
    const string NoEditor = "-c core.editor=true";

    readonly ICmd cmd;

    public ConflictService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R> AbortOperationAsync(string wd)
    {
        if (!Try(out var verb, out var e, OperationVerb(wd)))
            return e;

        return await cmd.RunAsync("git", $"{verb} --abort", wd);
    }

    public async Task<R> ContinueOperationAsync(string wd)
    {
        var operation = StatusService.GetOperation(wd);

        // A merge has no --continue: committing is what finishes it, and that is a different
        // command with a dialog behind it, so the UI offers Commit there rather than this
        if (operation == GitOperation.Merge)
            return R.Error("A merge is finished by committing it, not by continuing.");

        if (!Try(out var verb, out var e, OperationVerb(wd)))
            return e;

        return ToResult(await cmd.RunAsync("git", $"{NoEditor} {verb} --continue", wd), verb);
    }

    public async Task<R> SkipOperationAsync(string wd)
    {
        var operation = StatusService.GetOperation(wd);

        // Only the two that replay a series of commits have anything to skip
        if (operation != GitOperation.Rebase && operation != GitOperation.Am)
            return R.Error($"{ToName(operation)} has no commit to skip.");

        if (!Try(out var verb, out var e, OperationVerb(wd)))
            return e;

        return ToResult(await cmd.RunAsync("git", $"{NoEditor} {verb} --skip", wd), verb);
    }

    const string MarkerNote = ": leftover conflict marker";

    // The staged files that still contain conflict markers. Marking a file resolved is just
    // 'git add', and git does not look at what it is staging — so a file staged with '<<<<<<<'
    // still in it commits the markers into history, which the guards on gmd's own staging cannot
    // catch because the user (or a merge tool that gave up) staged it deliberately.
    //
    // 'git diff --cached --check' is git's own answer to this. Note it reports whitespace problems
    // as well and exits non-zero for those too, so the *lines* have to be filtered rather than the
    // exit code trusted — gating on the exit code alone would refuse a commit over a trailing
    // space. Findings go to stdout; a real failure is what puts anything on stderr.
    public async Task<R<IReadOnlyList<string>>> GetLeftoverMarkerPathsAsync(string wd)
    {
        var result = await cmd.RunAsync("git", "diff --cached --check", wd, skipLogError: true);
        if (result.ErrorOutput != "")
            return R.Error($"Failed to check for conflict markers\n{result.ErrorOutput}");

        return result
            .Output.Split('\n')
            .Where(line => line.EndsWith(MarkerNote))
            .Select(ToPath)
            .Where(path => path != "")
            .Distinct()
            .ToList();
    }

    // 'some/file.txt:12: leftover conflict marker' -> 'some/file.txt'. Taken from the right, since
    // a path may itself contain a colon.
    static string ToPath(string line)
    {
        var text = line[..^MarkerNote.Length];
        var lineNbrAt = text.LastIndexOf(':');

        return lineNbrAt <= 0 ? "" : text[..lineNbrAt];
    }

    // The git sub command of whatever is in progress, i.e. what '--abort' and friends attach to
    static R<string> OperationVerb(string wd)
    {
        var operation = StatusService.GetOperation(wd);
        return operation switch
        {
            GitOperation.Merge => "merge",
            GitOperation.CherryPick => "cherry-pick",
            GitOperation.Revert => "revert",
            GitOperation.Rebase => "rebase",
            GitOperation.Am => "am",
            _ => R.Error("No merge, rebase, cherry pick or revert is in progress."),
        };
    }

    internal static string ToName(GitOperation operation) =>
        operation switch
        {
            GitOperation.Merge => "Merge",
            GitOperation.CherryPick => "Cherry Pick",
            GitOperation.Revert => "Revert",
            GitOperation.Rebase => "Rebase",
            GitOperation.Am => "Apply Patches",
            _ => "Operation",
        };

    // Carrying on can stop again on the next commit, which is not a failure of the command but the
    // normal shape of a rebase over several commits, and it can be refused because the conflict the
    // operation stopped on has not been resolved. Both come back as a non-zero exit, so they are
    // told apart by what git printed — the same sniffing BranchService does when starting one.
    static R ToResult(CmdResult result, string verb)
    {
        if (!result.IsResultError)
            return R.Ok;

        var output = $"{result.Output}\n{result.ErrorOutput}";
        if (output.Contains("CONFLICT"))
            return R.Error($"The {verb} stopped on more conflicts.\nResolve them and continue again.", result);

        if (output.Contains("needs merge") || output.Contains("edit all merge conflicts"))
            return R.Error(
                $"Cannot continue the {verb} while there are unresolved conflicts.\n\n"
                    + "Resolve each conflicted file and mark it resolved, then continue.",
                result
            );

        return result;
    }
}
