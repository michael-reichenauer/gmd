using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace gmd.Utils;
public class Timing
{
    private readonly Stopwatch stopwatch;
    private TimeSpan lastTimeSpan = TimeSpan.Zero;
    private int count = 0;

    public Timing()
    {
        stopwatch = new Stopwatch();
        stopwatch.Start();
    }


    public static Timing Start => new Timing();

    public TimeSpan Stop()
    {
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }


    public TimeSpan Elapsed
    {
        get
        {
            lastTimeSpan = stopwatch.Elapsed;
            return lastTimeSpan;
        }
    }

    public long ElapsedMs => (long)Elapsed.TotalMilliseconds;

    public string ElapsedText => ElapsedMs < 1000
        ? $"{ElapsedMs}ms" : ElapsedMs < 60 * 1000
        ? $"{Elapsed.Seconds}s, {Elapsed.Milliseconds}ms"
        : $"{Elapsed.Hours}:{Elapsed.Minutes}:{Elapsed.Seconds}:{Elapsed.Milliseconds}";


    public TimeSpan Diff
    {
        get
        {
            TimeSpan previous = lastTimeSpan;
            return Elapsed - previous;
        }
    }

    public long DiffMs => (long)Diff.TotalMilliseconds;


    public void Log(
        string message,
        StopParameter stopParameter = default(StopParameter),
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        count++;

        Utils.Logging.Log.Debug(
            $"{count}: {message}: {this}", memberName, sourceFilePath, sourceLineNumber);
    }

    public void Log(
        StopParameter stopParameter = default(StopParameter),
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        count++;

        Utils.Logging.Log.Debug($"At {count}: {this}", memberName, sourceFilePath, sourceLineNumber);
    }


    public override string ToString() => count == 0 ? $"({ElapsedText})" : $"{DiffMs}ms ({ElapsedText})";

    public struct StopParameter
    {
    }
}
