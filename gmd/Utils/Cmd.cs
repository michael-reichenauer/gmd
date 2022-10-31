using System.Diagnostics;
using System.Text;

namespace gmd.Utils;

internal interface ICmd
{
    CmdResult Run(string path, string args);
    Task<CmdResult> RunAsync(string path, string args);
    CmdResult Start(string path, string args);
}

internal record CmdResult(
    int ExitCode,
    IReadOnlyList<string> Output,
    IReadOnlyList<string> Error);


internal class Cmd : ICmd
{
    private static readonly IReadOnlyList<string> EmptyLines = new string[0];

    private string workingDirectory;

    internal Cmd(string workingDirectory = "")
    {
        this.workingDirectory = workingDirectory;
    }

    public CmdResult Run(string path, string args)
    {
        var t = Timing.StartNew();
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
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            if (workingDirectory != "")
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
            }

            process.Start();

            while (!process.StandardOutput.EndOfStream)
            {
                string? line = process.StandardOutput.ReadLine();
                if (line != null)
                {
                    lines.Add(line.TrimEnd('r'));
                }
            }

            process.WaitForExit();
            Log.Info($"{path} {args} ({workingDirectory}) {t}");

            return new CmdResult(process.ExitCode, lines, EmptyLines);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Error($"Failed: {path} {args} {t}\n{e.Message}");
            return new CmdResult(-1, EmptyLines, new[] { e.Message });
        }
    }

    public Task<CmdResult> RunAsync(string path, string args)
    {
        return Task.Run(() =>
        {
            return Run(path, args);
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
            return new CmdResult(0, EmptyLines, EmptyLines);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, $"Exception for {path} {args}");
            return new CmdResult(-1, EmptyLines, new[] { e.Message });
        }
    }
}
