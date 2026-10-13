using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace gmd.Utils.Logging;

// The log file, ~/gmd.log, which every start of gmd begins anew. Lines are queued by whichever
// thread logs and written in batches by a thread of their own, so that logging never waits for
// the disk. A file grown past MaxSize is moved aside to gmd.log.2.log, replacing the one before.
//
// A log that cannot be written must never stop gmd, since the log is where a failure is told. A
// file that cannot be emptied, e.g. one that a 'sudo gmd' left owned by root, is replaced, and
// when not even that works, gmd logs nowhere. A read-only gmd.log used to leave gmd hanging on a
// blank screen: this constructor asserted, the assert waited for the log to close, i.e. for the
// writing thread, and that thread waited for this constructor to finish.
static class LogFile
{
    const string FileName = "gmd.log";
    const string LevelInfo = "INFO ";
    const int MaxSize = 2000000;
    const int WriteAttempts = 10;
    const int WriteRetryDelayMs = 30;

    static readonly BlockingCollection<string> lines = [];
    static readonly TaskCompletionSource done = new();
    static readonly int rootPathLength = RootPathLength();
    static readonly bool isWritten;

    static LogFile()
    {
        FilePath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), FileName);
        isWritten = Open(FilePath) is Success;

        // Started last, and never waited for here: the thread waits for this constructor to finish
        // before it can read a field of the class
        Task.Factory.StartNew(WriteLines, TaskCreationOptions.LongRunning).RunInBackground();
    }

    // Where the log is written, ~/gmd.log, for what About and a crash tell the user
    public static string FilePath { get; }

    // Ends the log once the lines queued before it are written; what is logged after it is dropped
    public static Task CloseAsync()
    {
        lines.CompleteAdding();
        return done.Task;
    }

    internal static void Write(string level, string msg, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        foreach (var line in msg.Split('\n'))
        {
            Queue(ToLogLine(level, line, memberName, sourceFilePath, sourceLineNumber));
        }
    }

    // Empties the file, or else replaces it, which takes only the right to write the folder, so a
    // file that cannot be written, as one left by a 'sudo gmd' cannot, is no reason to log nowhere
    internal static Result Open(string path)
    {
        if (Result.Catch(() => File.WriteAllText(path, "")) is Success)
            return Result.Ok;

        return Result.Catch(() =>
        {
            File.Delete(path);
            File.WriteAllText(path, "");
        });
    }

    // A line: the time, the level and the message, padded to a column, and then where it was
    // logged, as a path from the root of the repository
    internal static string ToLogLine(string level, string msg, string memberName, string sourceFilePath, int lineNumber)
    {
        var file = rootPathLength < sourceFilePath.Length - 1 ? sourceFilePath[rootPathLength..] : sourceFilePath;
        var text = $"{DateTime.Now.IsoMs()} {level}:\"{msg}\"";

        return $"{text, -100} {{{memberName}() {file}:{lineNumber}}}";
    }

    // The writing thread: waits for a line, takes the ones queued behind it as well, and writes
    // them in one go, until the log is closed
    static void WriteLines()
    {
        try
        {
            foreach (var line in lines.GetConsumingEnumerable())
            {
                List<string> batch = [line];
                while (lines.TryTake(out var next))
                {
                    batch.Add(next);
                }

                Append(batch);
            }

            Append([ToLogLine(LevelInfo, "Logging done", nameof(WriteLines), ThisFile(), ThisLine())]);
        }
        finally
        {
            done.SetResult();
        }
    }

    static void Queue(string line)
    {
        try
        {
            lines.Add(line);
        }
        catch (InvalidOperationException)
        {
            // The log is closed
        }
    }

    // Tried again for a moment while another program, e.g. a virus scanner, holds the file, and
    // dropped if that does not help, rather than failing anything
    static void Append(IReadOnlyList<string> batch)
    {
        if (!isWritten)
            return;

        for (var attempt = 1; Result.Catch(() => File.AppendAllLines(FilePath, batch)) is Error e; attempt++)
        {
            if (attempt == WriteAttempts || e.Exception is DirectoryNotFoundException)
                return;
            Thread.Sleep(WriteRetryDelayMs);
        }

        Result.Catch(() =>
        {
            if (new FileInfo(FilePath).Length > MaxSize)
                File.Move(FilePath, $"{FilePath}.2.log", overwrite: true);
        });
    }

    // The length of the path to the root of the repository, which is cut off the source file of a
    // line, taken from where this file was built: '<root>/gmd/Utils/Logging/LogFile.cs'
    static int RootPathLength()
    {
        var root = Path.GetDirectoryName(
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())))
        );
        return (root ?? "").Length + 1;
    }

    static string ThisFile([CallerFilePath] string path = "") => path;

    static int ThisLine([CallerLineNumber] int line = 0) => line;
}
