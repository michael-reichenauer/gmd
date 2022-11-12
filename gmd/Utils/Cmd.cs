using System.Diagnostics;
using System.Text;

namespace gmd.Utils;

internal interface ICmd
{
    CmdResult RunCmd(string path, string args, string workingDirectory);
    Task<CmdResult> RunAsync(string path, string args, string workingDirectory);
}

internal class CmdResult : R<string>
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


internal class Cmd : ICmd
{
    public Task<CmdResult> RunAsync(string path, string args, string workingDirectory) =>
        Task.Run(() => RunCmd(path, args, workingDirectory));

    public CmdResult RunCmd(string path, string args, string workingDirectory)
    {
        var t = Timing.Start;
        try
        {
            List<string> lines = new List<string>();
            // Log.Debug($"{path} {args} ({workingDirectory})");

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
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            if (workingDirectory != "")
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
            }

            process.Start();
            process.WaitForExit();

            var exitCode = process.ExitCode;
            var output = process.StandardOutput.ReadToEnd().Replace("\r", "").TrimEnd();
            var error = process.StandardError.ReadToEnd().Replace("\r", "").TrimEnd();

            Log.Info($"{path} {args} ({workingDirectory}) {t}");

            if (process.ExitCode != 0)
            {
                return new CmdResult(process.ExitCode, output, error);
            }

            return new CmdResult(output, error);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Error($"Failed: {path} {args} {t}\n{e.Message}");
            return new CmdResult(-1, "", e.Message);
        }
    }
}
