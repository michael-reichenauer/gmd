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
    Task<R<ConflictFile>> WithBaseAsync(ConflictFile file, string wd);
    Task<R> WriteAsync(ConflictFile file, string wd);
    Task<R> ResolveAsync(string path, ConflictKind kind, IReadOnlyList<HunkResolution> choices, string wd);
    Task<R> MarkResolvedAsync(string path, string wd);
    Task<R> UnresolveAsync(string path, string wd);
    Task<R> UseWholeFileAsync(string path, bool isOurs, string wd);
    Task<R> DeleteConflictedAsync(string path, string wd);
}

class ConflictService : IConflictService
{
    // '--continue' and '--skip' open an editor on the commit message, which would hang gmd behind
    // the terminal it owns. Nothing is passed here to prevent that: Cmd stops every git process it
    // starts from opening one at all, which is the only place it can be done reliably — GIT_EDITOR
    // beats '-c core.editor=...', so a command line flag is silently ineffective for any user who
    // has that set. See Cmd.NeverOpenAnEditor.
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

        return ToResult(await cmd.RunAsync("git", $"{verb} --continue", wd), verb);
    }

    public async Task<R> SkipOperationAsync(string wd)
    {
        var operation = StatusService.GetOperation(wd);

        // Only the two that replay a series of commits have anything to skip
        if (operation != GitOperation.Rebase && operation != GitOperation.Am)
            return R.Error($"{ToName(operation)} has no commit to skip.");

        if (!Try(out var verb, out var e, OperationVerb(wd)))
            return e;

        return ToResult(await cmd.RunAsync("git", $"{verb} --skip", wd), verb);
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
        var fullPath = System.IO.Path.Join(wd, path);

        // Which sides exist decides what can be offered for the file, so it is read for every one
        if (!Try(out var stages, out var e, await GetStagesAsync(path, wd)))
            return e;

        var hasOurs = stages.ContainsKey(2);
        var hasTheirs = stages.ContainsKey(3);

        // A file that is not there at all — both sides deleted it, or one did and this is that side
        if (!File.Exists(fullPath))
            return new ConflictFile(path, kind, false, false, [], hasOurs, hasTheirs);

        // Nothing to merge as text, so it is resolved whole file rather than parsed
        if (!Files.IsText(fullPath))
            return new ConflictFile(path, kind, true, false, [], hasOurs, hasTheirs);

        if (!Try(out var bytes, out e, () => File.ReadAllBytes(fullPath)))
            return R.Error($"Failed to read {path}", e);

        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

        // Throwing rather than replacing, so a file gmd cannot represent exactly is refused instead
        // of being silently rewritten with U+FFFD where it could not decode
        var encoding = new UTF8Encoding(false, true);
        if (!Try(out var text, out e, () => encoding.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0))))
            return R.Error($"{path} is not UTF-8 text, so it cannot be resolved here", e);

        var parsed = ConflictParser.Parse(path, kind, text, hasBom);
        return parsed with { HasOurs = hasOurs, HasTheirs = hasTheirs };
    }

    // Fills in the common ancestor of each conflict, i.e. what the base pane shows.
    //
    // The default 'merge' conflict style records no ancestor in the file, so it has to be recovered
    // by re-running the merge with '--diff3' over the three index stages and taking the base blocks
    // out of the result. It is display only: nothing here is ever written back to the file.
    //
    // Nothing touches the working tree. 'git unpack-file' writes its temp file into the *current
    // directory* and returns its name, so pointing that at a scratch folder inside the git dir keeps
    // the whole thing out of the user's sight — where 'git checkout-index --temp' would have been
    // the obvious tool but always writes to the worktree root, whatever its cwd, and those files
    // show up as untracked in the very status gmd is displaying.
    public async Task<R<ConflictFile>> WithBaseAsync(ConflictFile file, string wd)
    {
        // Already there: the user's conflict style is 'diff3' or 'zdiff3', so git wrote it into the
        // file itself and it is aligned by construction
        if (file.Hunks.Count == 0 || file.Hunks.Any(h => h.HasBase))
            return file with { HasBase = true };

        if (!Try(out var stages, out var e, await GetStagesAsync(file.Path, wd)))
            return e;

        // An add/add conflict has no stage 1: both sides created the file, so there is no ancestor.
        // This is the one place that can tell that apart from an ancestor that is merely empty
        // everywhere the file conflicts, which is why the answer is carried on the file.
        if (!stages.TryGetValue(1, out var baseSha) || !stages.ContainsKey(2) || !stages.ContainsKey(3))
            return file;

        var tempDir = System.IO.Path.Join(StatusService.GetGitDir(wd), $"gmd-conflict-{Guid.NewGuid():N}");
        if (!Try(out e, () => Directory.CreateDirectory(tempDir)))
            return R.Error("Failed to create a scratch folder", e);

        try
        {
            if (!Try(out var merged, out e, await MergeWithBaseAsync(stages, baseSha, tempDir)))
                return e;

            // Mapped by the lines each conflict covers rather than by position: '--diff3' groups
            // conflicts differently from the merge that wrote the file, so the two do not
            // necessarily have the same number of them. See SetBasesFrom.
            var withBases = ConflictParser.SetBasesFrom(file, ConflictParser.Parse(file.Path, file.Kind, merged));
            return withBases with { HasBase = true };
        }
        finally
        {
            Try(out var _, () => Directory.Delete(tempDir, true));
        }
    }

    // The blob of each stage of an unmerged path, keyed on stage number
    async Task<R<IReadOnlyDictionary<int, string>>> GetStagesAsync(string path, string wd)
    {
        if (!Try(out var output, out var e, await cmd.RunAsync("git", $"ls-files -u {Spec(path)}", wd)))
            return e;

        var stages = new Dictionary<int, string>();
        foreach (var line in output.Split('\n'))
        {
            // '<mode> <sha> <stage>\t<path>'
            var parts = line.Split('\t')[0].Split(' ');
            if (parts.Length == 3 && int.TryParse(parts[2], out var stage))
                stages[stage] = parts[1];
        }

        return stages;
    }

    // Re-runs the merge with the ancestor kept, and returns the result read straight off disk —
    // never through ICmd, whose stdout handling strips carriage returns and trailing newlines
    async Task<R<string>> MergeWithBaseAsync(IReadOnlyDictionary<int, string> stages, string baseSha, string tempDir)
    {
        if (!Try(out var baseFile, out var e, await UnpackAsync(baseSha, tempDir)))
            return e;
        if (!Try(out var oursFile, out e, await UnpackAsync(stages[2], tempDir)))
            return e;
        if (!Try(out var theirsFile, out e, await UnpackAsync(stages[3], tempDir)))
            return e;

        // '--diff3' and not '--zdiff3': zdiff3 hoists lines common to both sides out of the region,
        // so its conflicts would not line up with the ones git wrote into the working tree file and
        // every base would be paired with the wrong conflict — silently.
        //
        // merge-file writes its result into the *first* file, which is why 'ours' is unpacked to a
        // temp of its own. A non-zero exit is the conflict count, i.e. the normal case, so the
        // result is judged by what it produced rather than by the exit code.
        var args = $"merge-file --diff3 -L ours -L base -L theirs \"{oursFile}\" \"{baseFile}\" \"{theirsFile}\"";
        await cmd.RunAsync("git", args, tempDir, skipLogError: true);

        var mergedPath = System.IO.Path.Join(tempDir, oursFile);
        if (!Try(out var text, out e, () => File.ReadAllText(mergedPath)))
            return R.Error("Failed to read the merged file", e);

        return text;
    }

    // Writes the blob to a file in tempDir and returns its name, which git chooses
    async Task<R<string>> UnpackAsync(string sha, string tempDir)
    {
        if (!Try(out var name, out var e, await cmd.RunAsync("git", $"unpack-file {sha}", tempDir)))
            return e;

        return name.Trim();
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

        // The ancestor is not in the file in the default conflict style, and the read above does not
        // recover it because nearly no resolve needs it. One that takes the ancestor does, and it is
        // recovered here rather than sent down from the view: the model up there is narrowed, and a
        // resolve is applied to the file as it is on disk *now*, so the text has to come from the
        // same read the choices are lined up against.
        if (choices.Any(c => c.Choice == HunkChoice.Base) && !Try(out file, out e, await WithBaseAsync(file, wd)))
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
