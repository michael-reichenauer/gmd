using System.Text;

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

    Task<R<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, string wd);
    Task<R> WriteAsync(ConflictFile file, string wd);
    Task<R> ResolveAsync(string path, ConflictKind kind, IReadOnlyList<HunkResolution> choices, string wd);
    Task<R> MarkResolvedAsync(string path, string wd);
    Task<R> UnresolveAsync(string path, string wd);
    Task<R> UseWholeFileAsync(string path, bool isOurs, string wd);
    Task<R> DeleteConflictedAsync(string path, string wd);
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

    // A pathspec, not a bare path: '--' stops options being parsed but does *not* stop globbing, so
    // a file really named 'a[1].txt' would otherwise match 'a1.txt' instead of itself. Note that
    // 'git checkout-index' is the one command that will not take this — it wants a plain path.
    static string Spec(string path) => $"-- \":(literal){path}\"";

    // Reads the working tree file and parses it. The working tree file rather than the index
    // stages, because it is the only artifact that holds the text *between* the conflicts, and it
    // is what git takes verbatim once the path is marked resolved — including any hand edits.
    public async Task<R<ConflictFile>> GetConflictFileAsync(string path, ConflictKind kind, string wd)
    {
        await Task.CompletedTask;
        var fullPath = System.IO.Path.Join(wd, path);

        // A delete conflict has no file on one side, and a binary one has nothing to merge as text.
        // Both are resolved whole file, so they are reported rather than parsed.
        if (kind is ConflictKind.BothDeleted or ConflictKind.DeletedByUs && !File.Exists(fullPath))
            return new ConflictFile(path, kind, false, false, []);

        if (!File.Exists(fullPath))
            return R.Error($"File does not exist: {path}");

        if (!Files.IsText(fullPath))
            return new ConflictFile(path, kind, true, false, []);

        if (!Try(out var bytes, out var e, () => File.ReadAllBytes(fullPath)))
            return R.Error($"Failed to read {path}", e);

        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

        // Throwing rather than replacing, so a file gmd cannot represent exactly is refused instead
        // of being silently rewritten with U+FFFD where it could not decode
        var encoding = new UTF8Encoding(false, true);
        if (!Try(out var text, out e, () => encoding.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0))))
            return R.Error($"{path} is not UTF-8 text, so it cannot be resolved here", e);

        return ConflictParser.Parse(path, kind, text, hasBom);
    }

    // Applies one decision per conflict, in order, and writes the file back.
    //
    // The file is re-read rather than passed back down, which is what makes the layer above able to
    // hold a narrowed model with no markers in it. It also makes a file that changed on disk while
    // the resolver was open a caught error rather than a silent mis-resolve: if it no longer has
    // the same number of conflicts, the decisions no longer line up with them.
    public async Task<R> ResolveAsync(string path, ConflictKind kind, IReadOnlyList<HunkResolution> choices, string wd)
    {
        if (!Try(out var file, out var e, await GetConflictFileAsync(path, kind, wd)))
            return e;

        if (file.Hunks.Count != choices.Count)
            return R.Error(
                $"{path} has changed on disk since it was opened "
                    + $"({file.Hunks.Count} conflicts now, {choices.Count} before).\n\n"
                    + "Close the resolver and open it again."
            );

        foreach (var choice in choices)
        {
            var manual = choice.Choice == HunkChoice.Manual ? ConflictParser.ToLines(choice.ManualText) : null;
            file = ConflictParser.SetChoice(file, choice.Index, choice.Choice, manual);
        }

        return await WriteAsync(file, wd);
    }

    // Writes the resolved text back and marks the path resolved. Nothing about line endings is
    // done here: every line carries the terminator it was read with, so what comes out is what went
    // in, and git's own check-in conversion then applies exactly as it would to a hand edit.
    public async Task<R> WriteAsync(ConflictFile file, string wd)
    {
        if (file.IsBinary)
            return R.Error($"{file.Path} is binary, so there is no text to write");

        var fullPath = System.IO.Path.Join(wd, file.Path);
        var text = ConflictParser.ToText(file);

        // File.WriteAllText defaults to UTF-8 without a BOM, so a file that had one would lose it
        if (!Try(out var e, () => File.WriteAllText(fullPath, text, new UTF8Encoding(file.HasBom))))
            return R.Error($"Failed to write {file.Path}", e);

        return await MarkResolvedAsync(file.Path, wd);
    }

    // Staging a path is what 'resolved' means to git, for a file it merged and for one it deleted
    public async Task<R> MarkResolvedAsync(string path, string wd) =>
        await cmd.RunAsync("git", $"add {Spec(path)}", wd);

    // Puts the conflict back, markers and all, discarding whatever was resolved. Git can do this
    // even after the path was staged, from the resolve-undo data it keeps in the index.
    public async Task<R> UnresolveAsync(string path, string wd) =>
        await cmd.RunAsync("git", $"checkout --merge {Spec(path)}", wd);

    // The whole file from one side, which is the only thing on offer for a binary conflict and the
    // quickest answer for a text one that is not worth reading through
    public async Task<R> UseWholeFileAsync(string path, bool isOurs, string wd)
    {
        var side = isOurs ? "--ours" : "--theirs";
        if (!Try(out var _, out var e, await cmd.RunAsync("git", $"checkout {side} {Spec(path)}", wd)))
            return e;

        return await MarkResolvedAsync(path, wd);
    }

    // Accepts the deletion of a path one side removed, which is the other half of a modify/delete
    public async Task<R> DeleteConflictedAsync(string path, string wd) =>
        await cmd.RunAsync("git", $"rm -f {Spec(path)}", wd);

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
