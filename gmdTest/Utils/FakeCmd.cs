using System.Runtime.CompilerServices;

namespace gmdTest.Utils;

// Records one call made to the fake command runner. Stdin is what was piped to it, which is
// empty for everything but CommandWithStdin, and Environment the variables it was given.
record CmdCall(
    string Path,
    string Args,
    string WorkingDirectory,
    string Stdin = "",
    IReadOnlyDictionary<string, string>? Environment = null
);

// FakeCmd is a test double for ICmd, the seam between the git services and the git
// executable. It lets git output be canned so parsing can be tested without running git.
//
// Use like e.g.:
//     var cmd = new FakeCmd(gitLogOutput);
//     var log = new LogService(cmd);
//     var commits = AssertOk(await log.GetLogAsync(100, "/wd"));
//     Assert.AreEqual("git", cmd.Calls[0].Path);
class FakeCmd : ICmd
{
    readonly Func<string, string, string, CmdResult> respond;

    // Responds to every command with the given output
    public FakeCmd(string output)
        : this((_, _, _) => Ok(output)) { }

    // Responds based on (path, args, workingDirectory), for tests needing several commands
    public FakeCmd(Func<string, string, string, CmdResult> respond) => this.respond = respond;

    // All calls made, in order, so tests can assert which git commands were run
    public List<CmdCall> Calls { get; } = [];

    public static CmdResult Ok(string output) => new("fake-cmd", output, "");

    public static CmdResult Fail(string errorOutput, int exitCode = 1) => new("fake-cmd", exitCode, "", errorOutput);

    // A non-zero exit whose findings are on stdout and whose stderr is empty, which is what a
    // command that reports problems rather than failing looks like — 'git diff --check' is one.
    public static CmdResult Problems(string output, int exitCode = 2) => new("fake-cmd", exitCode, output, "");

    public Result<string> Command(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false,
        IReadOnlyDictionary<string, string>? environment = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => CommandRaw(path, args, workingDirectory, environment).ToResult(memberName, sourceFilePath, sourceLineNumber);

    public Task<Result<string>> RunAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false,
        IReadOnlyDictionary<string, string>? environment = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) =>
        Task.FromResult(
            Command(
                path,
                args,
                workingDirectory,
                skipLogError,
                skipLog,
                environment,
                memberName,
                sourceFilePath,
                sourceLineNumber
            )
        );

    public Task<CmdResult> RunRawAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    ) => Task.FromResult(CommandRaw(path, args, workingDirectory));

    public Result<string> CommandWithStdin(
        string path,
        string args,
        string stdinText,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Calls.Add(new CmdCall(path, args, "", stdinText));
        return respond(path, args, "").ToResult(memberName, sourceFilePath, sourceLineNumber);
    }

    CmdResult CommandRaw(
        string path,
        string args,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? env = null
    )
    {
        Calls.Add(new CmdCall(path, args, workingDirectory, "", env));
        return respond(path, args, workingDirectory);
    }
}
