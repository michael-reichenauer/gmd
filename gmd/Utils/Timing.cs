using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace gmd.Utils;

// Measures the time since it was started, for the log: '{t}' in a message is the time so far, and
// disposing it logs how long the block it was started for took.
//
//   var t = Timing.Start();
//   ...
//   Log.Info($"Read the repo {t}");
//
//   using (Timing.Start("Pushed")) { ... }
public class Timing : IDisposable
{
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
    readonly string msg;
    readonly string memberName;
    readonly string sourceFilePath;
    readonly int sourceLineNumber;

    Timing(string msg, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        this.msg = msg;
        this.memberName = memberName;
        this.sourceFilePath = sourceFilePath;
        this.sourceLineNumber = sourceLineNumber;
    }

    public static Timing Start(
        string msg = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => new(msg, memberName, sourceFilePath, sourceLineNumber);

    // Logged under the message it was started with, or else the name of the member that started it
    public void Dispose() =>
        Log.Info($"{(msg != "" ? msg : memberName)} {this}", memberName, sourceFilePath, sourceLineNumber);

    public override string ToString() => $"({Text(stopwatch.Elapsed)})";

    internal static string Text(TimeSpan elapsed) =>
        elapsed.TotalMilliseconds < 1000 ? $"{(long)elapsed.TotalMilliseconds}ms"
        : elapsed.TotalMilliseconds < 60 * 1000 ? $"{elapsed.Seconds}s, {elapsed.Milliseconds}ms"
        : $"{elapsed.Hours}:{elapsed.Minutes}:{elapsed.Seconds}:{elapsed.Milliseconds}";
}
