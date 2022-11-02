using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Environment;

namespace gmd.Utils.Logging;

internal static class Log
{
    private static readonly int MaxLogFileSize = 2000000;
    private static readonly BlockingCollection<string> logTexts = new BlockingCollection<string>();

    private static readonly object syncRoot = new object();

    private static readonly int ProcessID = Process.GetCurrentProcess().Id;
    private static readonly string LevelUsage = "USAGE";
    private static readonly string LevelDebug = "DEBUG";
    private static readonly string LevelInfo = "INFO ";
    private static readonly string LevelWarn = "WARN ";
    private static readonly string LevelError = "ERROR";

    private static int prefixLength = 0;
    private static string LogPath = "gmd.log";

    static TaskCompletionSource doneTask = new TaskCompletionSource();

    static Log()
    {
        Task.Factory.StartNew(SendBufferedLogRows, TaskCreationOptions.LongRunning)
            .RunInBackground();

        Init($"{Environment.GetFolderPath(SpecialFolder.UserProfile)}/gmd.log");
    }


    public static void Init(string logFilePath, [CallerFilePath] string sourceFilePath = "")
    {
        LogPath = logFilePath;
        string rootPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
            Path.GetDirectoryName(sourceFilePath)))) ?? "";
        prefixLength = rootPath.Length + 1;
        File.WriteAllText(LogPath, "");
    }


    private static void SendBufferedLogRows()
    {
        try
        {
            while (!logTexts.IsCompleted)
            {
                List<string> batchedTexts = new List<string>();
                // Wait for texts to log
                string filePrefix = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}";
                string? logText = logTexts.Take();
                // Native.OutputDebugString(logText);
                batchedTexts.Add($"{filePrefix} {logText}");

                // Check if there might be more buffered log texts, if so add them in batch
                while (logTexts.TryTake(out logText))
                {
                    // Native.OutputDebugString(logText);
                    batchedTexts.Add($"{filePrefix} {logText}");
                }

                try
                {
                    WriteToFile(batchedTexts);
                }
                catch (ThreadAbortException)
                {
                    // The process or app-domain is closing,
                    // Thread.ResetAbort();
                    return;
                }
                catch (Exception e) when (e.IsNotFatal())
                {
                    // Native.OutputDebugString("ERROR Failed to log to file, " + e);
                }
            }
        }
        finally
        {
            doneTask.SetResult();
        }
    }

    public static Task CloseAsync()
    {
        try
        {
            logTexts.CompleteAdding();
        }
        catch
        {
            // buffer might already be closed in case of crashing
        }

        return doneTask.Task;
    }

    public static void Usage(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelUsage, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Debug(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelDebug, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelInfo, msg, memberName, sourceFilePath, sourceLineNumber);
    }


    public static void Warn(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelWarn, msg, memberName, sourceFilePath, sourceLineNumber);
    }


    public static void Error(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelError, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Exception(
        Exception e,
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelError, $"{msg}\n{e}", memberName, sourceFilePath, sourceLineNumber);
    }


    public static void Exception(
        Exception e,
        Timing.StopParameter stop = default(Timing.StopParameter),
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelError, $"{e}", memberName, sourceFilePath, sourceLineNumber);
    }


    private static void Write(
        string level,
        string msg,
        string memberName,
        string filePath,
        int lineNumber)
    {
        filePath = filePath.Substring(prefixLength);
        var lines = msg.Split('\n');
        foreach (var line in lines)
        {
            string text = $"{level} {filePath}:{lineNumber} {memberName}: {line}";

            try
            {
                SendLog(text);
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                SendLog("ERROR Failed to log " + e);
            }
        }
    }


    private static void SendLog(string text)
    {
        try
        {
            logTexts.Add(text);
        }
        catch
        {
            // Failed to add, the buffer has been closed
        }
    }


    private static void WriteToFile(IReadOnlyCollection<string> text)
    {
        if (LogPath == null)
        {
            return;
        }

        Exception? error = null;
        lock (syncRoot)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    File.AppendAllLines(LogPath, text);

                    long length = new FileInfo(LogPath).Length;

                    if (length > MaxLogFileSize)
                    {
                        MoveLargeLogFile();
                    }

                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    // Ignore error since folder has been deleted during uninstallation
                    return;
                }
                catch (ThreadAbortException)
                {
                    // Process or app-domain is closing
                    // Thread.ResetAbort();
                    return;
                }
                catch (Exception e)
                {
                    Thread.Sleep(30);
                    error = e;
                }
            }
        }

        if (error != null)
        {
            throw error;
        }
    }


    private static void MoveLargeLogFile()
    {
        try
        {
            string tempPath = LogPath + "." + Guid.NewGuid();
            File.Move(LogPath, tempPath);

            Task.Run(() =>
            {
                try
                {
                    string secondLogFile = LogPath + ".2.log";
                    if (File.Exists(secondLogFile))
                    {
                        File.Delete(secondLogFile);
                    }

                    File.Move(tempPath, secondLogFile);
                }
                catch (Exception e)
                {
                    SendLog("ERROR Failed to move temp to second log file: " + e);
                }

            }).RunInBackground();
        }
        catch (Exception e)
        {
            SendLog("ERROR Failed to move large log file: " + e);
        }
    }

    // private static class Native
    // {
    //     [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    //     public static extern void OutputDebugString(string message);
    // }
}
