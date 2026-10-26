using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace gmd.Utils.Logging;

// The log file, ~/gmd.log, which every start of gmd goes on with, so that what went wrong in the
// run before is still there when gmd is started again to report it. Lines are queued by whichever
// thread logs and written in batches by a thread of their own, so that logging never waits for
// the disk.
//
// A file grown past MaxSize is rotated: moved aside to gmd.1.log, which moves to gmd.2.log, and the
// one there goes, so the logs take 3 MB at most. Rotated rather than trimmed to its last part, since
// trimming rewrites a file that another gmd, in another terminal, may be appending to at the same
// moment, which would lose its lines or cut one in half, where a rename is one step for the file
// system, and the other gmd's next line simply starts the new file. And a rotation keeps the run
// before whole, as a trim would not.
//
// A log that cannot be written must never stop gmd, since the log is where a failure is told. A
// file that cannot be appended to, e.g. one that a 'sudo gmd' left owned by root, is rotated
// aside, which takes only the right to write the folder, and when not even that works, gmd logs
// nowhere. A read-only gmd.log used to leave gmd hanging on a
// blank screen: this constructor asserted, the assert waited for the log to close, i.e. for the
// writing thread, and that thread waited for this constructor to finish.
static class LogFile
{
    const string FileName = "gmd.log";
    const string LevelInfo = "INFO ";
    const int MaxSize = 1024 * 1024;
    const int RotatedCount = 2;

    // What Copy Log copies at most: what GitHub takes in an issue's field, whose whole body takes
    // 65536 characters, and the terminal's copy, which takes some 75 KB (TerminalClipboard)
    internal const int MaxCopyLength = 60000;
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
        Queue(StartLine(DateTime.Now, Environment.ProcessId));

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

    // The log's lines as written so far, those of the file rotated last first, so that the run
    // before the current one is there too, for the log view (Help › View Log)
    public static IReadOnlyList<string> ReadLines() => [.. ReadLines(RotatedPath(FilePath, 1)), .. ReadLines(FilePath)];

    // The last whole lines that together take at most maxLength characters, as Copy Log copies them,
    // and how many they are
    internal static (string Text, int Count) Tail(IReadOnlyList<string> lines, int maxLength)
    {
        var length = 0;
        var count = 0;
        for (var i = lines.Count - 1; i >= 0 && length + lines[i].Length + 1 <= maxLength; i--)
        {
            length += lines[i].Length + 1;
            count++;
        }
        return (string.Join("\n", lines.Skip(lines.Count - count)), count);
    }

    // Goes on with the file, rotating it first when it is too large. A file that cannot be written,
    // as one left by a 'sudo gmd' cannot, is rotated aside, or else replaced, both of which take only
    // the right to write the folder, so it is no reason to log nowhere.
    internal static Result Open(string path)
    {
        // Where an older gmd moved its large log, which none reads any more
        Result.Catch(() => File.Delete($"{path}.2.log"));

        if (IsTooLarge(path))
            Rotate(path);
        if (Result.Catch(() => File.AppendAllText(path, "")) is Success)
            return Result.Ok;

        Rotate(path);
        if (Result.Catch(() => File.AppendAllText(path, "")) is Success)
            return Result.Ok;

        return Result.Catch(() =>
        {
            File.Delete(path);
            File.WriteAllText(path, "");
        });
    }

    // gmd.1.log for 1, the file rotated last, and gmd.2.log for 2, the one before it
    internal static string RotatedPath(string path, int number) => Path.ChangeExtension(path, $"{number}.log");

    // Moves each rotated file one up, the last one away, and the log to the first. Two gmds that find
    // the log too large at the same moment can both rotate it, the second moving the first's new and
    // nearly empty file aside, which costs the oldest rotated file and nothing more.
    internal static void Rotate(string path)
    {
        for (var number = RotatedCount; number > 1; number--)
        {
            var from = RotatedPath(path, number - 1);
            Result.Catch(() =>
            {
                if (File.Exists(from))
                    File.Move(from, RotatedPath(path, number), overwrite: true);
            });
        }
        Result.Catch(() => File.Move(path, RotatedPath(path, 1), overwrite: true));
    }

    static bool IsTooLarge(string path) =>
        Result.Catch(() => File.Exists(path) && new FileInfo(path).Length > MaxSize) is true;

    // Read while another gmd may be writing it, which it is allowed to go on with, so a line it is
    // writing may be read in part, and a file that is not there is no lines
    static IReadOnlyList<string> ReadLines(string path)
    {
        var lines = Result.Catch(() =>
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream);
            List<string> read = [];
            while (reader.ReadLine() is string line)
                read.Add(line);
            return (IReadOnlyList<string>)read;
        });
        return lines is IReadOnlyList<string> l ? l : [];
    }

    // The first line of each gmd process, after a blank one, which stands out in a log that goes on
    // from run to run, so that where a run starts is found at a glance, and with the process id,
    // which tells apart the lines of two gmds writing the log at once
    internal static string StartLine(DateTime time, int processId)
    {
        var text = $"######## gmd started {time.IsoMs()}, process {processId} ";
        return $"\n{text.PadRight(StartLineLength, '#')}";
    }

    internal const string StartLinePrefix = "######## gmd started";
    const int StartLineLength = 120;

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

        if (IsTooLarge(FilePath))
            Rotate(FilePath);
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
