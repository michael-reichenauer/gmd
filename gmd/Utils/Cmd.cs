using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace gmd.Utils;

interface ICmd
{
    // The output of a command that exited 0, or a CmdError carrying everything it printed. The
    // caller info is the error's Origin, so that it names the service that ran the command rather
    // than this class, which every command failure would otherwise share. The environment is
    // variables set for this one command on top of gmd's own, e.g. GIT_INDEX_FILE.
    Result<string> Command(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false,
        IReadOnlyDictionary<string, string>? environment = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    );
    Task<Result<string>> RunAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false,
        IReadOnlyDictionary<string, string>? environment = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    );

    // What a command printed and how it exited, whatever the exit code, for the few commands whose
    // non-zero exit is an answer rather than a failure ('check-ignore', 'diff --check')
    Task<CmdResult> RunRawAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    );

    // Starts a program that may go on running after it has done what it was started for, e.g. a
    // browser: waits a moment for it to fail, but never for it to end, and never kills it
    Task<Result> StartAsync(
        string path,
        string args,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    );

    // Runs a command that gets its input on stdin, and waits for it to exit but never for its
    // output streams to close. See Cmd.CommandWithStdin for why that difference is the whole
    // reason this is not just Command().
    Result<string> CommandWithStdin(
        string path,
        string args,
        string stdinText,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    );
}

// What a command printed and how it exited
record CmdResult(string Cmd, int ExitCode, string Output, string ErrorOutput)
{
    public CmdResult(string cmd, string output, string errorOutput)
        : this(cmd, 0, output, errorOutput) { }

    public bool IsOk => ExitCode == 0;

    // A zero exit is its output, anything else a CmdError
    public Result<string> ToResult(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => ExitCode == 0 ? Output : new CmdError(this, memberName, sourceFilePath, sourceLineNumber);
}

// The failure case of a command. The message is what it printed on stderr plus the command line;
// the rest is for callers that recognize a particular failure by its output or exit code, e.g.
// 'CONFLICT' after a merge:
//
//   if (result is CmdError e && e.Output.Contains("CONFLICT")) ...
class CmdError : Error
{
    public CmdError(
        CmdResult result,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : base($"{result.ErrorOutput}\nCommand: {result.Cmd}", memberName, sourceFilePath, sourceLineNumber)
    {
        Result = result;
    }

    public CmdResult Result { get; }
    public string Cmd => Result.Cmd;
    public int ExitCode => Result.ExitCode;
    public string Output => Result.Output;
    public string ErrorOutput => Result.ErrorOutput;
}

class Cmd : ICmd
{
    // A safety net rather than a normal case: the clipboard tools this is used for exit within
    // milliseconds, but gmd must not be frozen by one that does not.
    const int StdinTimeoutMs = 2000;

    // How long to wait for the error text of a command that has already failed
    const int ErrorReadTimeoutMs = 200;

    // How long a started program has to fail in before it is taken to be running. Openers like
    // xdg-open hand the page to a browser and exit in well under this.
    const int StartWaitMs = 1500;

    static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

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
    )
    {
        return Task.Run(() =>
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
    }

    public Task<CmdResult> RunRawAsync(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false
    )
    {
        return Task.Run(() => CommandRaw(path, args, workingDirectory, skipLogError, skipLog));
    }

    public static Result<string> Run(string cmd, string workingDirectory = "")
    {
        var index = cmd.IndexOf(' ');
        if (index == -1)
            return new Cmd().Command(cmd, "", workingDirectory);

        var path = cmd[..index];
        var args = cmd[(index + 1)..];
        return new Cmd().Command(path, args, workingDirectory);
    }

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
    ) =>
        CommandRaw(path, args, workingDirectory, skipLogError, skipLog, environment)
            .ToResult(memberName, sourceFilePath, sourceLineNumber);

    public CmdResult CommandRaw(
        string path,
        string args,
        string workingDirectory,
        bool skipLogError = false,
        bool skipLog = false,
        IReadOnlyDictionary<string, string>? environment = null
    )
    {
        // The variables are part of the logged command, since they can change what it does, as
        // GIT_INDEX_FILE changes which index a 'git add .' writes to
        var envText = environment == null ? "" : string.Concat(environment.Select(v => $"{v.Key}={v.Value} "));
        var cmdText = $"{envText}{path} {args}   [{workingDirectory},";
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
                NeverOpenAnEditor(process.StartInfo);
                foreach (var (name, value) in environment ?? Empty)
                {
                    process.StartInfo.Environment[name] = value;
                }
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

    // Stops git from ever launching an editor in a child process.
    //
    // gmd owns the terminal, so an editor started behind it would hang the application outright with
    // nothing on screen to say why — 'git rebase --continue', 'cherry-pick --continue' and
    // 'revert --continue' all open one to confirm the commit message.
    //
    // It has to be the environment and not '-c core.editor=...' on the command line, because
    // GIT_EDITOR takes precedence over core.editor: a user whose GIT_EDITOR is set (VS Code sets it
    // to 'code --wait') would have gmd freeze however the command was configured. 'true' exits 0
    // without writing, which git reads as "the message is fine as it stands"; git runs the editor
    // through a shell, which is bundled on Windows, so it holds there too.
    static void NeverOpenAnEditor(ProcessStartInfo info)
    {
        info.Environment["GIT_EDITOR"] = "true";
        info.Environment["GIT_SEQUENCE_EDITOR"] = "true"; // The todo list of an interactive rebase
    }

    // Starts a program that may go on running, e.g. a browser, which a tool like xdg-open starts
    // for the page and which then outlives it, or which is itself what was started. Neither can be
    // waited for, since that is for as long as the browser is open, nor killed on a timeout, as
    // CommandWithStdin does, since that would close the browser.
    //
    // So this waits a moment for it to fail, and takes it still running after that as having
    // worked. Its output is read and dropped for as long as it runs, rather than not redirected,
    // which would draw it over the UI, or redirected and never read, which blocks a program once
    // it has written a pipe full. For the same reason the process is not disposed while it runs:
    // that closes the pipes, and writing to a closed one can end it.
    public Task<Result> StartAsync(
        string path,
        string args,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) =>
        Task.Run(() =>
        {
            var result = Start(path, args);
            return result.IsOk ? Result.Ok : new CmdError(result, memberName, sourceFilePath, sourceLineNumber);
        });

    CmdResult Start(string path, string args)
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
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };
            var errorLines = new System.Collections.Concurrent.ConcurrentQueue<string>();
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorLines.Enqueue(e.Data);
            };

            process.Start();
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(StartWaitMs))
            {
                process.Exited += (_, _) => process.Dispose();
                Log.Info($"Started: {cmdText} {t}, still running");
                return new CmdResult(cmdText, "", "");
            }

            using (process)
            {
                if (process.ExitCode != 0)
                {
                    var error = string.Join('\n', errorLines).Trim();
                    Log.Debug($"Error: {cmdText} {t}]\nExit Code: {process.ExitCode}, Error:\n{error}");
                    return new CmdResult(cmdText, process.ExitCode, "", error);
                }

                Log.Info($"Started: {cmdText} {t}");
                return new CmdResult(cmdText, "", "");
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            // A program that is not installed lands here, which is expected while looking for one
            Log.Debug($"Failed: {cmdText} {t}]\n{e.Message}");
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
    public Result<string> CommandWithStdin(
        string path,
        string args,
        string stdinText,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => CommandWithStdinRaw(path, args, stdinText).ToResult(memberName, sourceFilePath, sourceLineNumber);

    CmdResult CommandWithStdinRaw(string path, string args, string stdinText)
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
                if (Result.Catch(() => WriteStdin(process, stdinText)) is Error writeError)
                    Log.Debug($"Failed to write stdin of {cmdText}, {writeError}");

                if (!process.WaitForExit(StdinTimeoutMs))
                {
                    if (Result.Catch(() => process.Kill(true)) is Error killError)
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
