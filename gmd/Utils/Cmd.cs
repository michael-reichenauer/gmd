using System.Diagnostics;
using System.Text;

namespace gmd.Utils;

internal interface ICmd
{
    string WorkingDirectory { get; }
    CmdResult RunCmd(string path, string args);
    Task<CmdResult> RunAsync(string path, string args);
    CmdResult Start(string path, string args);

}

internal class CmdResult
{
    public int ExitCode { get; }
    public string Output { get; }
    public string Error { get; }

    public CmdResult(int exitCode, string output, string error)
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
    }
}


internal class Cmd : ICmd
{
    private static readonly IReadOnlyList<string> EmptyLines = new string[0];

    public string WorkingDirectory { get; internal set; }

    internal Cmd(string workingDirectory = "")
    {
        this.WorkingDirectory = workingDirectory;
    }

    public CmdResult RunCmd(string path, string args)
    {
        var t = Timing.Start();
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

            if (WorkingDirectory != "")
            {
                process.StartInfo.WorkingDirectory = WorkingDirectory;
            }

            process.Start();
            process.WaitForExit();

            var exitCode = process.ExitCode;
            var output = process.StandardOutput.ReadToEnd().Replace("\r", "").TrimEnd();
            var error = process.StandardError.ReadToEnd().Replace("\r", "").TrimEnd();

            Log.Info($"{path} {args} ({WorkingDirectory}) {t}");

            return new CmdResult(process.ExitCode, output, error);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Error($"Failed: {path} {args} {t}\n{e.Message}");
            return new CmdResult(-1, "", e.Message);
        }
    }

    public Task<CmdResult> RunAsync(string path, string args)
    {
        return Task.Run(() =>
        {
            return RunCmd(path, args);
        });
    }

    public CmdResult Start(string path, string args)
    {
        ProcessStartInfo info = new ProcessStartInfo(path);
        info.Arguments = args;
        info.UseShellExecute = true;
        try
        {
            // Start process, but do not wait for it to complete
            Process.Start(info);
            return new CmdResult(0, "", "");
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, $"Exception for {path} {args}");
            return new CmdResult(-1, "", e.Message);
        }
    }
}
