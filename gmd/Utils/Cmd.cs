using System.Diagnostics;
using System.Text;

namespace gmd.Utils;

interface ICmd
{
    CmdResult Command(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    );
    Task<CmdResult> RunAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    );

    // Runs a command that gets its input on stdin, and waits for it to exit but never for its
    // output streams to close. See Cmd.CommandWithStdin for why that difference is the whole
    // reason this is not just Command().
    CmdResult CommandWithStdin(string path, string args, string stdinText);
}

class CmdResult : R<string>
{
    public CmdResult(string cmd, int exitCode, string output, string errorOutput)
        : base(new Exception($"{errorOutput}\nCommand: {cmd}"))
    {
        Cmd = cmd;
        ExitCode = exitCode;
        Output = output;
        ErrorOutput = errorOutput;
    }

    public CmdResult(string cmd, string output, string errorOutput)
        : base(output)
    {
        ExitCode = 0;
        Cmd = cmd;
        Output = output;
        ErrorOutput = errorOutput;
    }

    public string Cmd { get; }
    public int ExitCode { get; }
    public string Output { get; }
    public string ErrorOutput { get; }
}

class Cmd : ICmd
{
    // A safety net rather than a normal case: the clipboard tools this is used for exit within
    // milliseconds, but gmd must not be frozen by one that does not.
    const int StdinTimeoutMs = 2000;

    // How long to wait for the error text of a command that has already failed
    const int ErrorReadTimeoutMs = 200;

    public Task<CmdResult> RunAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    )
    {
        return Task.Run(() => Command(path, args, workingDirectory, skipLogError, skipLog));
    }

    public static CmdResult Run(string cmd, string workingDirectory = "")
    {
        var index = cmd.IndexOf(' ');
        if (index == -1)
            return new Cmd().Command(cmd, "", workingDirectory);

        var path = cmd[..index];
        var args = cmd[(index + 1)..];
        return new Cmd().Command(path, args, workingDirectory);
    }

    public CmdResult Command(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    )
    {
        var cmdText = $"{path} {args}   [{workingDirectory},";
        var t = Timing.Start();
        try
        {
            Log.Debug($"Start: {cmdText} (0ms)] ...");
            var outputLines = new List<string>();
            var errorLines = new List<string>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
            };

            using (process)
            {
                if (workingDirectory != "")
                {
                    process.StartInfo.WorkingDirectory = workingDirectory;
                }
                process.OutputDataReceived += (sender, args) => outputLines.Add(args.Data ?? "");
                process.ErrorDataReceived += (sender, args) => errorLines.Add(args.Data ?? "");

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit(); //you need this in order to flush the output buffer

                var exitCode = process.ExitCode;
                var output = string.Join('\n', outputLines).Replace("\r", "").TrimEnd();
                var error = string.Join('\n', errorLines).Replace("\r", "").TrimEnd();

                if (process.ExitCode != 0)
                {
                    if (!skipLogError)
                        Log.Warn($"Error: {cmdText} {t}]\nExit Code: {process.ExitCode}, Error:\n{error}");
                    if (skipLogError)
                        Log.Debug($"Error: {cmdText} {t}]\nExit Code: {process.ExitCode}, Error:\n{error}");
                    return new CmdResult(cmdText, process.ExitCode, output, error);
                }

                if (!skipLog)
                    Log.Info($"{cmdText} {t}]");
                if (skipLog)
                    Log.Debug($"OK: {cmdText} {t}]");
                return new CmdResult(cmdText, output, error);
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Error($"Failed: {cmdText} {t}]\n{e.Message}");
            return new CmdResult(cmdText, -1, "", e.Message);
        }
    }

    // Runs a command that gets its input on stdin, e.g. a clipboard tool, and waits for it to
    // exit, but never for its output streams to close.
    //
    // That difference is the whole reason this is not just Command(). An X or Wayland selection
    // is owned by a live process, so xclip, xsel and wl-copy fork a background process to hold
    // the text, and that process inherits the redirected output pipes of its parent. Command()
    // calls WaitForExit() with no timeout, which also waits for those pipes to reach end of file,
    // and that does not happen while the helper lives — i.e. it would block for as long as the
    // clipboard holds the text (dotnet/runtime#27128). Here the output is never read and the wait
    // is bounded, so a helper that detaches cannot freeze the UI.
    public CmdResult CommandWithStdin(string path, string args, string stdinText)
    {
        var cmdText = $"{path} {args}";
        var t = Timing.Start();
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    // Redirected so that a tool writing to the terminal cannot corrupt the drawn
                    // UI, but deliberately never read while it may still be running, see above.
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardInputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = Encoding.UTF8,
                },
            };

            using (process)
            {
                process.Start();

                // Closing stdin is what tells the tool that the text has ended. A tool that
                // rejects its arguments can exit before this, which shows up as a broken pipe —
                // its exit code and error output below say what actually went wrong, so a failed
                // write is not the error to report.
                if (!Try(out var writeError, () => WriteStdin(process, stdinText)))
                    Log.Debug($"Failed to write stdin of {cmdText}, {writeError}");

                if (!process.WaitForExit(StdinTimeoutMs))
                {
                    if (!Try(out var killError, () => process.Kill(true)))
                        Log.Debug($"Failed to kill {cmdText}, {killError}");

                    Log.Debug($"Timeout: {cmdText} {t}]");
                    return new CmdResult(cmdText, -1, "", $"Timeout after {StdinTimeoutMs} ms");
                }

                if (process.ExitCode != 0)
                {
                    var error = ErrorOf(process);
                    Log.Debug($"Error: {cmdText} {t}]\nExit Code: {process.ExitCode}, Error:\n{error}");
                    return new CmdResult(cmdText, process.ExitCode, "", error);
                }

                Log.Info($"{cmdText} {t}]");
                return new CmdResult(cmdText, "", "");
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            // A tool that is not installed lands here, which is expected while probing for one
            Log.Debug($"Failed: {cmdText} {t}]\n{e.Message}");
            return new CmdResult(cmdText, -1, "", e.Message);
        }
    }

    // The text, and then end of input, which is what tells the tool that there is no more
    static void WriteStdin(Process process, string text)
    {
        using var stdin = process.StandardInput;
        stdin.Write(text);
    }

    // The error output of a command that has already failed. Bounded and read on a thread of its
    // own, since the pipe can still be held open by a helper the command forked before failing,
    // and since the caller is usually the UI thread, where waiting on an async read would
    // deadlock against Terminal.Gui's synchronization context.
    static string ErrorOf(Process process)
    {
        var read = Task.Run(() => process.StandardError.ReadToEnd());
        return read.Wait(ErrorReadTimeoutMs) ? read.Result.Replace("\r", "").Trim() : "";
    }
}
