using System.Diagnostics;
using System.Text;

namespace gmd.Utils;


interface ICmd
{
    CmdResult RunCmd(string path, string args, string workingDirectory);
    Task<CmdResult> RunAsync(string path, string args, string workingDirectory);
}

class CmdResult : R<string>
{
    public CmdResult(int exitCode, string output, string errorOutput)
        : base(new Exception(errorOutput))
    {
        ExitCode = exitCode;
        Output = output;
        ErrorOutput = errorOutput;
    }

    public CmdResult(string output, string errorOutput)
        : base(output)
    {
        ExitCode = 0;
        Output = output;
        ErrorOutput = errorOutput;
    }

    public int ExitCode { get; }
    public string Output { get; }
    public string ErrorOutput { get; }
}


class Cmd : ICmd
{
    public Task<CmdResult> RunAsync(string path, string args, string workingDirectory) =>
        Task.Run(() => RunCmd(path, args, workingDirectory));

    public CmdResult RunCmd(string path, string args, string workingDirectory)
    {
        var t = Timing.Start;
        try
        {
            Log.Debug($"{path} {args} ({workingDirectory})");
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

                Log.Info($"{path} {args} ({workingDirectory}) {t}");

                if (process.ExitCode != 0)
                {
                    return new CmdResult(process.ExitCode, output, error);
                }

                return new CmdResult(output, error);
            }
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Error($"Failed: {path} {args} {t}\n{e.Message}");
            return new CmdResult(-1, "", e.Message);
        }
    }
}
