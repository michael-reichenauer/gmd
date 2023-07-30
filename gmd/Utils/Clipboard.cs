using System.Diagnostics;
using System.Text;
using TerminalClipboard = Terminal.Gui.Clipboard;

namespace gmd.Utils;

// cSpell:ignore xsel
static class Clipboard
{
    public static R Set(string text)
    {
        // if (Build.IsMacOs) // Does it work ????
        // {
        //    return OsxClipboard.SetText(text);
        // }
        // else
        if (Build.IsLinux)
        {
            return LinuxClipboard.TrySetText(text);
        }
        else
        if (Build.IsWindows)
        {
            return WindowsClipboard.TrySetText(text);
        }

        return R.Error("Clipboard not supported on this platform");
    }
}


// https://github.com/CopyText/TextCopy/blob/main/src/TextCopy/LinuxClipboard_2.1.cs
static class LinuxClipboard
{
    static bool isWsl;
    static Cmd cmd = new Cmd();

    static LinuxClipboard()
    {
        isWsl = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") != null;
    }


    public static R TrySetText(string text)
    {
        var tempFileName = Path.GetTempFileName();
        if (!Try(out var e, () => File.WriteAllText(tempFileName, text))) return e;
        return InnerSetText(tempFileName);
    }

    static R InnerSetText(string tempFileName)
    {
        try
        {
            if (isWsl)
            {
                return Cmd.Run($"bash -c \"cat {tempFileName} | clip.exe \"");
            }
            else
            {
                return Cmd.Run($"bash -c \"cat {tempFileName} | xsel -i --clipboard \"");
            }
        }
        finally
        {
            if (File.Exists(tempFileName))
            {
                if (!Try(out var e, () => File.Delete(tempFileName))) Log.Warn($"{e}");
            }
        }
    }

    public static R<string> GetText()
    {
        var tempFileName = Path.GetTempFileName();
        try
        {
            if (!Try(out var e, InnerGetText(tempFileName))) return e;

            if (!Try(out string? text, out e, () => File.ReadAllText(tempFileName))) return e;

            return text;
        }
        finally
        {
            if (File.Exists(tempFileName)) File.Delete(tempFileName);
        }
    }

    static R InnerGetText(string tempFileName)
    {
        if (isWsl)
        {
            return Cmd.Run($"powershell.exe -NoProfile Get-Clipboard  > {tempFileName}");
        }
        else
        {
            return Cmd.Run($"xsel -o --clipboard  > {tempFileName}");
        }
    }
}


static class BashRunner
{
    public static string Run(string commandLine)
    {
        StringBuilder errorBuilder = new();
        StringBuilder outputBuilder = new();
        var arguments = $"-c \"{commandLine}\"";
        using Process process = new()
        {
            StartInfo = new()
            {
                FileName = "bash",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false,
            }
        };
        process.Start();
        process.OutputDataReceived += (_, args) => { outputBuilder.AppendLine(args.Data); };
        process.BeginOutputReadLine();
        process.ErrorDataReceived += (_, args) => { errorBuilder.AppendLine(args.Data); };
        process.BeginErrorReadLine();
        process.WaitForExit();
        // if (!process.DoubleWaitForExit())
        // {
        //     var timeoutError = $"Error: bash {arguments}\nProcess timeout: {errorBuilder}\nOutput: {outputBuilder}";
        //     throw new(timeoutError);
        // }
        if (process.ExitCode == 0)
        {
            return outputBuilder.ToString();
        }

        var error = $"Error: bash {arguments}\n{errorBuilder}\nOutput: {outputBuilder}";
        throw new(error);
    }

    // //To work around https://github.com/dotnet/runtime/issues/27128
    // static bool DoubleWaitForExit(this Process process)
    // {
    //     var result = process.WaitForExit(1500);
    //     if (result)
    //     {
    //         process.WaitForExit();
    //     }
    //     return result;
    // }
}

static class WindowsClipboard
{
    public static R TrySetText(string text)
    {
        if (!TerminalClipboard.TrySetClipboardData(text)) return R.Error("Failed to set clipboard");
        return R.Ok;
    }
}
