using System.Diagnostics;
using System.Text;

namespace gmd.Utils;


interface ICmd
{
    CmdResult Command(string path, string args, string workingDirectory, bool skipLogError = false, bool skipLog = false);
    Task<CmdResult> RunAsync(string path, string args, string workingDirectory, bool skipLogError = false, bool skipLog = false);
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
    public Task<CmdResult> RunAsync(string path, string args, string workingDirectory,
        bool skipLogError = false, bool skipLog = false)
    {
        return Task.Run(() => Command(path, args, workingDirectory, skipLogError, skipLog));
    }

    public static CmdResult Run(string cmd, string workingDirectory = "")
    {
        var index = cmd.IndexOf(' ');
        if (index == -1) return new Cmd().Command(cmd, "", workingDirectory);

        var path = cmd.Substring(0, index);
        var args = cmd.Substring(index + 1);
        return new Cmd().Command(path, args, workingDirectory);
    }

    public CmdResult Command(string path, string args, string workingDirectory,
        bool skipLogError = false, bool skipLog = false)
    {
        var cmdText = $"{path} {args} ({workingDirectory})";
        var t = Timing.Start();
        try
        {
            Log.Debug($"Start: {path} {args} ({workingDirectory})");
            var outputLines = new List<string>();
            var errorLines = new List<string>();

            using (var process = new Process
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
                    StandardErrorEncoding = Encoding.UTF8
                }
            })
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
                    if (!skipLogError) Log.Warn($"Error: {cmdText} {t}\nExit Code: {process.ExitCode}, Error:\n{error}");
                    if (skipLogError) Log.Debug($"Error: {cmdText} {t}\nExit Code: {process.ExitCode}, Error:\n{error}");
                    return new CmdResult(cmdText, process.ExitCode, output, error);
                }

                if (!skipLog) Log.Info($"OK: {cmdText} {t}");
                if (skipLog) Log.Debug($"OK: {path} {args} ({workingDirectory}) {t}");
                return new CmdResult(cmdText, output, error);
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Error($"Failed: {path} {args} {t}\n{e.Message}");
            return new CmdResult(cmdText, -1, "", e.Message);
        }

        // //To work around https://github.com/dotnet/runtime/issues/27128
        // static bool DoubleWaitForExit(Process process, int timeout = 500)
        // {
        //     var result = process.WaitForExit(timeout);
        //     if (result)
        //     {
        //         process.WaitForExit();
        //     }
        //     return result;
        // }
    }
}
