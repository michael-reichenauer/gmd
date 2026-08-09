namespace gmdTest.Utils;

// Records one call made to the fake command runner. Stdin is what was piped to it, which is
// empty for everything but CommandWithStdin.
record CmdCall(string Path, string Args, string WorkingDirectory, string Stdin = "");

// FakeCmd is a test double for ICmd, the seam between the git services and the git
// executable. It lets git output be canned so parsing can be tested without running git.
//
// Use like e.g.:
//     var cmd = new FakeCmd(gitLogOutput);
//     var log = new LogService(cmd);
//     Assert.IsTrue(Try(out var commits, await log.GetLogAsync(100, "/wd")));
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

    public static CmdResult Ok(string output) => new CmdResult("fake-cmd", output, "");

    public static CmdResult Fail(string errorOutput, int exitCode = 1) =>
        new CmdResult("fake-cmd", exitCode, "", errorOutput);

    // A non-zero exit whose findings are on stdout and whose stderr is empty, which is what a
    // command that reports problems rather than failing looks like — 'git diff --check' is one.
    public static CmdResult Problems(string output, int exitCode = 2) =>
        new CmdResult("fake-cmd", exitCode, output, "");

    public CmdResult Command(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    )
    {
        Calls.Add(new CmdCall(path, args, workingDirectory));
        return respond(path, args, workingDirectory);
    }

    public Task<CmdResult> RunAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    ) => Task.FromResult(Command(path, args, workingDirectory, skipLogError, skipLog));

    public CmdResult CommandWithStdin(string path, string args, string stdinText)
    {
        Calls.Add(new CmdCall(path, args, "", stdinText));
        return respond(path, args, "");
    }
}
