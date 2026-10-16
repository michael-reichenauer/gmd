using System.Text;

namespace gmd.Git.Private;

interface IPatchService
{
    Task<Result<FilePatch>> GetUncommittedPatchAsync(string path, string wd);
    Task<Result> ApplyToIndexAsync(FilePatch patch, string indexPath, string wd);
    Task<Result> ApplyReversedAsync(FilePatch patch, string wd);
    Task<Result> CommitPatchesAsync(
        string message,
        bool isAmend,
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, FilePatch> patches,
        string wd
    );
}

// The patch of one file's uncommitted changes, and applying a patch, of some of its lines, to an
// index or in reverse to the working tree: what committing and discarding part of a file are made
// of (FilePatch). The diff the view shows cannot be used for it: its parser drops the line that
// says a file has no newline at its end, and an applied patch has to be what git wrote, byte for byte.
class PatchService : IPatchService
{
    // The format FilePatch is read in, whatever the user's config says, as DiffService pins its own,
    // and with no textconv either, which shows a file as a program has converted it, and no patch of
    // that applies to the file. Empty lines keep their ' ', which diff.suppressBlankEmpty drops.
    const string PatchFormat =
        "-c diff.suppressBlankEmpty=false diff --no-color --no-ext-diff --no-textconv"
        + " --src-prefix=a/ --dst-prefix=b/ --unified=3";

    // The bytes of a patch as text only where they are UTF-8: anything else would be changed by being
    // decoded and written back, and a line it added would go into the commit changed
    static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    readonly ICmd cmd;

    public PatchService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    // From HEAD to the working tree, a file git does not track yet as a new file, as the diff of the
    // uncommitted changes has it: everything is staged first, as that diff stages it, into a copy of
    // the index (TempIndex), which is also how a file moved is found to be renamed, and its patch is
    // then of both paths, a rename with the lines the diff shows, the old file's and the new one's.
    // Git writes the patch to a file rather than to its output, which Cmd decodes and trims, and the
    // last line of a patch can end in spaces, or be one.
    public async Task<Result<FilePatch>> GetUncommittedPatchAsync(string path, string wd)
    {
        var patchPath = Path.Join(Path.GetTempPath(), $"gmd-patch-{Guid.NewGuid():N}.diff");
        try
        {
            return await TempIndex.WithCopyAsync<FilePatch>(
                wd,
                async env =>
                {
                    var literal = new Dictionary<string, string>(env) { ["GIT_LITERAL_PATHSPECS"] = "1" };
                    if (await cmd.RunAsync("git", "add -A", wd, environment: literal) is Error e)
                        return e;

                    // With no commit yet there is no HEAD, the file is new, and what is staged is all of it.
                    // Asked first: git's word for it with a path is not the one it has without.
                    var hasHead = await HasHeadAsync(wd);
                    var since = hasHead ? "HEAD" : "--staged";
                    var renamedFrom = hasHead ? await RenamedFromAsync(path, literal, wd) : "";
                    var paths = renamedFrom == "" ? Cmd.Quote(path) : $"{Cmd.Quote(renamedFrom)} {Cmd.Quote(path)}";
                    var renames = renamedFrom == "" ? "--no-renames" : "--find-renames";
                    var args = $"{PatchFormat} {renames} --output={Cmd.Quote(patchPath)} {since} -- {paths}";
                    if (await cmd.RunAsync("git", args, wd, environment: literal) is Error diffError)
                        return diffError;

                    return ReadPatch(path, patchPath);
                }
            );
        }
        finally
        {
            Result.Catch(() => File.Delete(patchPath));
        }
    }

    // The path the file was renamed from, as the diff of the uncommitted changes finds it, "" when it
    // was not. Read with -z, where paths are written as they are.
    async Task<string> RenamedFromAsync(string path, IReadOnlyDictionary<string, string> env, string wd)
    {
        var args = "diff --find-renames --name-status -z HEAD";
        if (await cmd.RunAsync("git", args, wd, environment: env) is not string output)
            return "";

        var parts = output.Split('\0');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith('R') && i + 2 < parts.Length)
            {
                if (parts[i + 2] == path)
                    return parts[i + 1];
                i += 2;
            }
            else
            {
                i++; // A status and its one path
            }
        }
        return "";
    }

    // Whether there is a commit yet, which a repository just made has not
    async Task<bool> HasHeadAsync(string wd) =>
        (await cmd.RunRawAsync("git", "rev-parse --verify --quiet HEAD", wd, skipLogError: true)).ExitCode == 0;

    static Result<FilePatch> ReadPatch(string path, string patchPath)
    {
        if (Result.Catch(() => File.ReadAllBytes(patchPath)) is not byte[] bytes)
            return new Error($"Failed to read the patch of {path}");
        if (bytes.Length == 0)
            return new Error($"{path} has no uncommitted changes");

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return new Error($"{path} is not UTF-8 text, so its changes can only be taken whole");
        }

        return PatchParser.Parse(text) switch
        {
            FilePatch patch => patch,
            Error e => new Error($"Failed to read the patch of {path}", e),
        };
    }

    // To the index, the repository's own for "", and to nothing else: the working tree stays as it
    // is. An empty patch is nothing to apply.
    public async Task<Result> ApplyToIndexAsync(FilePatch patch, string indexPath, string wd)
    {
        var env = indexPath == "" ? null : new Dictionary<string, string> { ["GIT_INDEX_FILE"] = indexPath };
        return await ApplyAsync(patch, "--cached", env, wd);
    }

    // Takes the changes of the patch back in the working tree, and nothing else: the index stays as
    // it is. Git does it as it would check the file out, with the line ends .gitattributes and
    // core.autocrlf give it.
    public Task<Result> ApplyReversedAsync(FilePatch patch, string wd) => ApplyAsync(patch, "-R", null, wd);

    // Commits the paths whole and the patches, each of some of a file's changes, and nothing else, as
    // 'git commit --only' commits paths: in an index of its own, made from HEAD, or empty in a
    // repository with no commit yet, which 'git commit' then commits, with its hooks and signing as
    // ever. The working tree is not written, and the repository's index is told of the commit after,
    // for the paths committed: left as it was, it would have their files as they were before it, and
    // show the commit's changes as staged to be taken back.
    public async Task<Result> CommitPatchesAsync(
        string message,
        bool isAmend,
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, FilePatch> patches,
        string wd
    )
    {
        var indexPath = Path.Join(Path.GetTempPath(), $"gmd-index-{Guid.NewGuid():N}");
        var pathsFile = Path.GetTempFileName();
        try
        {
            var env = new Dictionary<string, string>
            {
                ["GIT_INDEX_FILE"] = indexPath,
                ["GIT_LITERAL_PATHSPECS"] = "1",
            };
            var pathspec = $"--pathspec-from-file={Cmd.Quote(pathsFile)} --pathspec-file-nul";

            var readTree = await HasHeadAsync(wd) ? "read-tree HEAD" : "read-tree --empty";
            if (await cmd.RunAsync("git", readTree, wd, environment: env) is Error readError)
                return readError;

            if (paths.Count > 0)
            {
                if (Result.Catch(() => File.WriteAllText(pathsFile, string.Join('\0', paths))) is Error writeError)
                    return writeError;
                if (await cmd.RunAsync("git", $"add -A {pathspec}", wd, environment: env) is Error addError)
                    return addError;
            }
            foreach (var patch in patches.Values)
            {
                if (await ApplyToIndexAsync(patch, indexPath, wd) is Error applyError)
                    return applyError;
            }

            var amend = isAmend ? " --amend" : "";
            if (await cmd.RunAsync("git", $"commit{amend} -m {Cmd.Quote(message)}", wd, environment: env) is Error e)
                return e;

            // A renamed file's old path goes with its rename, as gone
            var committed = paths
                .Concat(patches.Keys)
                .Concat(patches.Values.Select(p => p.RenamedFrom).Where(p => p != ""));
            if (Result.Catch(() => File.WriteAllText(pathsFile, string.Join('\0', committed))) is Error pathsError)
                return pathsError;
            var literal = new Dictionary<string, string> { ["GIT_LITERAL_PATHSPECS"] = "1" };
            if (await cmd.RunAsync("git", $"reset -q {pathspec}", wd, environment: literal) is Error resetError)
                return new Error("Committed, but the index was not told of it: 'git reset' would", resetError);
            return Result.Ok;
        }
        finally
        {
            foreach (var path in new[] { indexPath, $"{indexPath}.lock", pathsFile })
                Result.Catch(() => File.Delete(path));
        }
    }

    // All of the patch or none of it, which is how git applies a patch. Whitespace is applied as it
    // is: the user's apply.whitespace could have git change it or refuse it.
    async Task<Result> ApplyAsync(FilePatch patch, string option, IReadOnlyDictionary<string, string>? env, string wd)
    {
        if (patch.IsEmpty)
            return Result.Ok;

        var patchPath = Path.Join(Path.GetTempPath(), $"gmd-patch-{Guid.NewGuid():N}.diff");
        try
        {
            if (Result.Catch(() => File.WriteAllBytes(patchPath, StrictUtf8.GetBytes(patch.ToText()))) is Error e)
                return new Error("Failed to write the patch", e);

            var args = $"apply {option} --whitespace=nowarn {Cmd.Quote(patchPath)}";
            return await cmd.RunAsync("git", args, wd, environment: env) switch
            {
                string => Result.Ok,
                Error applyError => new Error("Git could not apply the changes", applyError),
            };
        }
        finally
        {
            Result.Catch(() => File.Delete(patchPath));
        }
    }
}
