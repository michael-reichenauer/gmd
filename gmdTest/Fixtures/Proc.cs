using System.Diagnostics;
using System.Text;

namespace gmdTest.Fixtures;

record ProcResult(string Cmd, int ExitCode, string Output, string Error)
{
    public bool IsOk => ExitCode == 0;

    public override string ToString() => $"'{Cmd}' exited {ExitCode}\nOutput:\n{Output}\nError:\n{Error}";
}

// A process runner for the fixtures, i.e. the three things gmd.Utils.Cmd is not: it takes
// environment variables, it has a timeout, and it writes nothing to the developer's ~/gmd.log.
//
// The timeout is the reason this exists rather than reusing ICmd. Cmd.Command calls
// WaitForExit() with no argument, so a wedged child hangs the whole test run with no output at
// all. Here it fails the test naming what was run.
//
// Arguments are passed as a list rather than one string, so a path containing a space cannot be
// split by the quoting rules of whatever platform the test runs on.
static class Proc
{
    public const int DefaultTimeoutMs = 30000;

    // Variables that must never leak from the developer's shell into a fixture process, since
    // each of them would silently redirect git somewhere other than the fixture repository
    static readonly string[] RemovedVariables =
    [
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_INDEX_FILE",
        "GIT_OBJECT_DIRECTORY",
        "GIT_COMMON_DIR",
        "GIT_NAMESPACE",
        "GIT_CONFIG_GLOBAL",
        "GIT_CONFIG_SYSTEM",
        "GIT_AUTHOR_DATE",
        "GIT_COMMITTER_DATE",
    ];

    public static ProcResult Run(
        string file,
        IReadOnlyList<string> args,
        string workingDirectory = "",
        IReadOnlyDictionary<string, string>? env = null,
        int timeoutMs = DefaultTimeoutMs
    )
    {
        var cmdText = $"{file} {string.Join(' ', args)}";
        var outputLines = new List<string>();
        var errorLines = new List<string>();

        var startInfo = new ProcessStartInfo
        {
            FileName = file,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            // The captured screen is full of '● ┣ ┅ Ϙ', so the pipes have to be UTF-8
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        args.ForEach(a => startInfo.ArgumentList.Add(a));
        if (workingDirectory != "")
            startInfo.WorkingDirectory = workingDirectory;

        RemovedVariables.ForEach(v => startInfo.Environment.Remove(v));
        env?.ForEach(p => startInfo.Environment[p.Key] = p.Value);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => outputLines.Add(e.Data ?? "");
        process.ErrorDataReceived += (_, e) => errorLines.Add(e.Data ?? "");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMs))
        {
            IgnoreErrors(() => process.Kill(true));
            Assert.Fail($"'{cmdText}' did not exit within {timeoutMs} ms");
        }

        // WaitForExit() with no argument after a successful timed wait flushes the output buffers
        process.WaitForExit();

        return new ProcResult(cmdText, process.ExitCode, Text(outputLines), Text(errorLines));
    }

    // Runs and asserts a zero exit code, for the commands a test cannot proceed without
    public static string Ok(
        string file,
        IReadOnlyList<string> args,
        string workingDirectory = "",
        IReadOnlyDictionary<string, string>? env = null,
        int timeoutMs = DefaultTimeoutMs
    )
    {
        var result = Run(file, args, workingDirectory, env, timeoutMs);
        Assert.IsTrue(result.IsOk, $"{result}");
        return result.Output;
    }

    // Whether the program can be run at all, used to tell a missing tool from a broken one
    public static bool CanRun(string file, params string[] args)
    {
        try
        {
            return Run(file, args, timeoutMs: 5000).IsOk;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static string Text(IEnumerable<string> lines) => string.Join('\n', lines).Replace("\r", "").TrimEnd();

    static void IgnoreErrors(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            // Best effort, e.g. killing a process that just exited on its own
        }
    }
}
