using System.Text;

namespace gmd.Git.Private;

interface IPatchService
{
    Task<Result<FilePatch>> GetUncommittedPatchAsync(string path, string wd);
    Task<Result> ApplyToIndexAsync(FilePatch patch, string indexPath, string wd);
    Task<Result> ApplyReversedAsync(FilePatch patch, string wd);
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
        "-c diff.suppressBlankEmpty=false diff --no-color --no-ext-diff --no-textconv --no-renames"
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
    // uncommitted changes has it: it is staged first, as that diff stages everything, into a copy of
    // the index (TempIndex). Git writes the patch to a file rather than to its output, which Cmd
    // decodes and trims, and the last line of a patch can end in spaces, or be one.
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
                    if (await cmd.RunAsync("git", $"add -A -- {Cmd.Quote(path)}", wd, environment: literal) is Error e)
                        return e;

                    var args = $"{PatchFormat} --output={Cmd.Quote(patchPath)} HEAD -- {Cmd.Quote(path)}";
                    var diff = await cmd.RunAsync("git", args, wd, environment: literal);
                    if (diff is Error noHead && noHead.Message.Contains("ambiguous argument 'HEAD': unknown revision"))
                    { // No commit yet, so the file is new, and what is staged is all of it
                        args = $"{PatchFormat} --staged --output={Cmd.Quote(patchPath)} -- {Cmd.Quote(path)}";
                        diff = await cmd.RunAsync("git", args, wd, environment: literal);
                    }
                    if (diff is Error diffError)
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
